using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.Options;
using TariffCalculator.Api.Endpoints;
using TariffCalculator.Core;

namespace TariffCalculator.Api.Ai;

public class ClaudeBillAssistant : IBillAssistant
{
    private const string ExplainPrompt = """
        You explain residential electricity bills to customers in Malaysia. Amounts are in Malaysian ringgit (RM).

        You will receive the tariff rates and a bill breakdown produced by the billing system. Those numbers are
        authoritative: quote them as given and never recalculate or invent figures.

        Write for someone with no energy background. Explain what each charge is and why the total is what it is,
        including whether the retail charge applied (it is waived when usage is at or below the waiver limit).
        Then give two or three specific, practical ways this household could lower the bill.

        Plain text, no markdown headings, under 200 words.
        """;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IOptions<AnthropicOptions> _options;
    private readonly ILogger<ClaudeBillAssistant> _logger;

    // created on first use so the app still starts (and the non-AI endpoints still work) without a key
    private readonly Lazy<AnthropicClient> _client;

    public ClaudeBillAssistant(IOptions<AnthropicOptions> options, ILogger<ClaudeBillAssistant> logger)
    {
        _options = options;
        _logger = logger;
        _client = new(() => new AnthropicClient { ApiKey = options.Value.ApiKey });
    }

    public async Task<string> ExplainAsync(Tariff tariff, int kWh, BillBreakdown bill, CancellationToken ct = default)
    {
        var facts = JsonSerializer.Serialize(new { tariff = TariffResponse.From(tariff), kWh, bill = Rounded(bill) }, Json);

        var response = await SendAsync(new MessageCreateParams
        {
            Model = _options.Value.Model,
            MaxTokens = 16000,
            System = ExplainPrompt,
            Messages = [new BetaUserMessageParam { Content = $"Explain this bill:\n{facts}" }],
            OutputConfig = new BetaOutputConfig { Effort = Effort.Low },   // simple task, no deep thinking needed
        }, ct);

        return TextOf(response);
    }

    private async Task<BetaMessage> SendAsync(MessageCreateParams request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.Value.ApiKey))
            throw new AssistantUnavailableException("AI features are not configured. Set Anthropic:ApiKey (see README).");

        // if a safety classifier declines, let the API retry on its recommended fallback model
        request = request with
        {
            Fallbacks = new Default(),
            Betas = ["server-side-fallback-2026-07-01"],
        };

        BetaMessage response;
        try
        {
            response = await _client.Value.Beta.Messages.Create(request, cancellationToken: ct);
        }
        catch (AnthropicException ex)
        {
            _logger.LogError(ex, "Claude API call failed");
            throw new AssistantUnavailableException("The AI service is unavailable right now, try again later.", ex);
        }

        if (response.StopReason == BetaStopReason.Refusal)
            throw new AssistantRefusedException();

        return response;
    }

    private static string TextOf(BetaMessage response) =>
        string.Join("\n", response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text)).Trim();

    private static BillBreakdown Rounded(BillBreakdown b) => new(
        Math.Round(b.Energy, 2, MidpointRounding.AwayFromZero),
        Math.Round(b.Capacity, 2, MidpointRounding.AwayFromZero),
        Math.Round(b.Network, 2, MidpointRounding.AwayFromZero),
        b.Retail, b.Total);
}

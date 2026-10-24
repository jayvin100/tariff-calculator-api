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

    private const string AskPrompt = """
        You answer customers' questions about their electricity bills in Malaysia, using the tariffs configured
        in this billing system. Amounts are in Malaysian ringgit (RM).

        Use the calculate_bill tool for every bill amount you mention, including comparisons and questions like
        "how much can I use before ...". Call it several times when you need to compare usages. Never do bill
        arithmetic yourself, because the tool is the source of truth. Use list_tariffs when you need to know
        which tariffs exist or how one is structured. If the customer doesn't name a tariff, use DOMESTIC.

        If the question isn't about electricity bills or usage, say briefly that you can only help with bills.

        Plain text, no markdown headings, under 150 words.
        """;

    // the most tool round trips per question, a safety net against a model stuck in a loop
    private const int MaxToolRounds = 8;

    private static readonly List<BetaToolUnion> Tools =
    [
        new BetaTool
        {
            Name = BillTools.ListTariffs,
            Description = "List every tariff in the billing system with its rates, retail charge, retail waiver limit and maximum supported usage.",
            InputSchema = new() { Properties = new Dictionary<string, JsonElement>() },
        },
        new BetaTool
        {
            Name = BillTools.CalculateBill,
            Description = "Calculate the exact monthly bill in RM for a usage in kWh under a tariff. Returns the energy, capacity, network and retail charges and the total.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["kwh"] = JsonSerializer.SerializeToElement(new { type = "integer", minimum = 0, description = "Monthly usage in kWh" }),
                    ["tariff_code"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Tariff code, e.g. DOMESTIC. Defaults to DOMESTIC." }),
                },
                Required = ["kwh"],
            },
        },
    ];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IOptions<AnthropicOptions> _options;
    private readonly ILogger<ClaudeBillAssistant> _logger;
    private readonly BillCalculator _calculator;

    // created on first use so the app still starts (and the non-AI endpoints still work) without a key
    private readonly Lazy<AnthropicClient> _client;

    public ClaudeBillAssistant(IOptions<AnthropicOptions> options, ILogger<ClaudeBillAssistant> logger, BillCalculator calculator)
    {
        _calculator = calculator;
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

    public async Task<AskResult> AskAsync(string question, IReadOnlyList<Tariff> tariffs, CancellationToken ct = default)
    {
        var tools = new BillTools(tariffs, _calculator);
        List<BetaMessageParam> messages = [new BetaUserMessageParam { Content = question }];

        // the tool use loop: Claude asks for a tool, we run it and send the result back, until it answers
        for (var round = 0; round <= MaxToolRounds; round++)
        {
            var response = await SendAsync(new MessageCreateParams
            {
                Model = _options.Value.Model,
                MaxTokens = 16000,
                System = AskPrompt,
                Messages = messages,
                Tools = Tools,
                OutputConfig = new BetaOutputConfig { Effort = Effort.Medium },
            }, ct);

            if (response.StopReason != BetaStopReason.ToolUse)
                return new AskResult(TextOf(response), tools.Calculations);

            List<BetaContentBlockParam> assistantContent = [];
            List<BetaContentBlockParam> toolResults = [];
            foreach (var block in response.Content)
            {
                // echo the whole assistant turn back unchanged (thinking blocks included, the API checks them)
                if (block.TryPickText(out var text))
                    assistantContent.Add(new BetaTextBlockParam { Text = text.Text });
                else if (block.TryPickThinking(out var thinking))
                    assistantContent.Add(new BetaThinkingBlockParam { Thinking = thinking.Thinking, Signature = thinking.Signature });
                else if (block.TryPickRedactedThinking(out var redacted))
                    assistantContent.Add(new BetaRedactedThinkingBlockParam { Data = redacted.Data });
                else if (block.TryPickFallback(out var fallback))
                    assistantContent.Add(BetaFallbackBlockParam.FromRawUnchecked(fallback.RawData));   // marks a model switch, echo as-is
                else if (block.TryPickToolUse(out var toolUse))
                {
                    assistantContent.Add(new BetaToolUseBlockParam { ID = toolUse.ID, Name = toolUse.Name, Input = toolUse.Input });

                    // every tool_use needs a matching tool_result, errors included, so Claude can correct itself
                    var result = tools.Execute(toolUse.Name, toolUse.Input);
                    toolResults.Add(new BetaToolResultBlockParam { ToolUseID = toolUse.ID, Content = result.Content, IsError = result.IsError });
                }
            }

            messages.Add(new BetaAssistantMessageParam { Content = assistantContent });
            messages.Add(new BetaUserMessageParam { Content = toolResults });   // all results in ONE user message
        }

        _logger.LogWarning("Gave up after {Rounds} tool rounds for question {Question}", MaxToolRounds, question);
        throw new AssistantUnavailableException("The assistant couldn't finish answering, try rephrasing the question.");
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

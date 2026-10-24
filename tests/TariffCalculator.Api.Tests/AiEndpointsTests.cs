using System.Net;
using System.Net.Http.Json;
using TariffCalculator.Api.Ai;
using TariffCalculator.Api.Endpoints;

namespace TariffCalculator.Api.Tests;

[Collection(ApiCollection.Name)]
public class AiEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Explain_ReturnsBillAndExplanation()
    {
        var response = await _client.PostAsJsonAsync("/api/bills/explain", new ExplainRequest(601, null));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ExplainResponse>();
        Assert.Equal("DOMESTIC", body!.Tariff);
        Assert.Equal(277.02m, body.Bill.Total);
        Assert.Contains("601 kWh", body.Explanation);
    }

    [Fact]
    public async Task Explain_UsageOutOfRange_Returns400WithoutCallingAi()
    {
        factory.Assistant.ThrowOnNextCall = new InvalidOperationException("should not be called");

        var response = await _client.PostAsJsonAsync("/api/bills/explain", new ExplainRequest(99_999, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        factory.Assistant.ThrowOnNextCall = null;
    }

    [Fact]
    public async Task Explain_AiUnavailable_Returns503()
    {
        factory.Assistant.ThrowOnNextCall = new AssistantUnavailableException("down");

        var response = await _client.PostAsJsonAsync("/api/bills/explain", new ExplainRequest(100, null));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Explain_AiRefused_Returns422()
    {
        factory.Assistant.ThrowOnNextCall = new AssistantRefusedException();

        var response = await _client.PostAsJsonAsync("/api/bills/explain", new ExplainRequest(100, null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Ask_ReturnsAnswerAndCalculations()
    {
        var response = await _client.PostAsJsonAsync("/api/bills/ask", new AskRequest("What is my bill for 601 kWh?"));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AskResult>();
        Assert.Contains("601 kWh", body!.Answer);
        var calculation = Assert.Single(body.Calculations);
        Assert.Equal(277.02m, calculation.Bill.Total);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hi")]
    public async Task Ask_TooShortQuestion_Returns400(string question)
    {
        var response = await _client.PostAsJsonAsync("/api/bills/ask", new AskRequest(question));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ask_AiUnavailable_Returns503()
    {
        factory.Assistant.ThrowOnNextCall = new AssistantUnavailableException("down");

        var response = await _client.PostAsJsonAsync("/api/bills/ask", new AskRequest("What is my bill for 100 kWh?"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}

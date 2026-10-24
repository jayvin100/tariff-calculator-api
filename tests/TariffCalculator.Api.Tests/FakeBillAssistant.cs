using System.Text.Json;
using TariffCalculator.Api.Ai;
using TariffCalculator.Core;

namespace TariffCalculator.Api.Tests;

// stands in for Claude so the tests are free, fast and deterministic
public class FakeBillAssistant : IBillAssistant
{
    public Exception? ThrowOnNextCall { get; set; }

    public Task<string> ExplainAsync(Tariff tariff, int kWh, BillBreakdown bill, CancellationToken ct = default)
    {
        ThrowIfRequested();
        return Task.FromResult($"Fake explanation for {kWh} kWh on {tariff.Code}: total RM{bill.Total}.");
    }

    // pretends Claude called calculate_bill once, through the real BillTools
    public Task<AskResult> AskAsync(string question, IReadOnlyList<Tariff> tariffs, CancellationToken ct = default)
    {
        ThrowIfRequested();
        var tools = new BillTools(tariffs, new BillCalculator());
        tools.Execute(BillTools.CalculateBill, new Dictionary<string, JsonElement>
        {
            ["kwh"] = JsonSerializer.SerializeToElement(601),
        });
        return Task.FromResult(new AskResult($"Fake answer to: {question}", tools.Calculations));
    }

    private void ThrowIfRequested()
    {
        var ex = ThrowOnNextCall;
        ThrowOnNextCall = null;
        if (ex is not null)
            throw ex;
    }
}

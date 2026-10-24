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

    private void ThrowIfRequested()
    {
        var ex = ThrowOnNextCall;
        ThrowOnNextCall = null;
        if (ex is not null)
            throw ex;
    }
}

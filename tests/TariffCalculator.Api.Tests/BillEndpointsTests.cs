using System.Net;
using System.Net.Http.Json;
using TariffCalculator.Api.Endpoints;
using TariffCalculator.Core;

namespace TariffCalculator.Api.Tests;

[Collection(ApiCollection.Name)]
public class BillEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Calculate_DefaultsToDomesticTariff()
    {
        var bill = await _client.GetFromJsonAsync<BillBreakdown>("/api/bills/calculate?kWh=601");

        Assert.Equal(277.02m, bill!.Total);
        Assert.Equal(10.00m, bill.Retail);
    }

    [Fact]
    public async Task Calculate_UsesRequestedTariff()
    {
        await _client.PutAsJsonAsync("/api/tariffs/bill-test", new TariffRequest(
            "Bill test", EnergyRate: 0.10m, CapacityRate: 0m, NetworkRate: 0m,
            RetailCharge: 0m, RetailWaiverLimitKwh: 0, MaxSupportedKwh: 5000));

        var bill = await _client.GetFromJsonAsync<BillBreakdown>("/api/bills/calculate?kWh=3000&tariff=bill-test");

        Assert.Equal(300.00m, bill!.Total);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1501)]
    public async Task Calculate_UsageOutOfRange_Returns400(int kWh)
    {
        var response = await _client.GetAsync($"/api/bills/calculate?kWh={kWh}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Calculate_UnknownTariff_Returns404()
    {
        var response = await _client.GetAsync("/api/bills/calculate?kWh=100&tariff=NOPE");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Health_ReportsHealthyWhenDatabaseIsUp()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

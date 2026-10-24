using System.Net;
using System.Net.Http.Json;
using TariffCalculator.Api.Endpoints;

namespace TariffCalculator.Api.Tests;

[Collection(ApiCollection.Name)]
public class TariffEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static TariffRequest ValidRequest(string name = "Test tariff") =>
        new(name, EnergyRate: 0.30m, CapacityRate: 0.05m, NetworkRate: 0.10m,
            RetailCharge: 5m, RetailWaiverLimitKwh: 300, MaxSupportedKwh: 2000);

    [Fact]
    public async Task GetAll_IncludesSeededDomesticTariff()
    {
        var tariffs = await _client.GetFromJsonAsync<List<TariffResponse>>("/api/tariffs");

        var domestic = Assert.Single(tariffs!, t => t.Code == "DOMESTIC");
        Assert.Equal(0.2703m, domestic.EnergyRate);
    }

    [Fact]
    public async Task GetByCode_IsCaseInsensitive()
    {
        var response = await _client.GetAsync("/api/tariffs/domestic");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetByCode_Unknown_Returns404()
    {
        var response = await _client.GetAsync("/api/tariffs/DOES-NOT-EXIST");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_NewCode_CreatesThenUpdates()
    {
        var created = await _client.PutAsJsonAsync("/api/tariffs/put-test", ValidRequest());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var updated = await _client.PutAsJsonAsync("/api/tariffs/put-test", ValidRequest("Renamed"));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var tariff = await _client.GetFromJsonAsync<TariffResponse>("/api/tariffs/PUT-TEST");
        Assert.Equal("Renamed", tariff!.Name);
    }

    [Fact]
    public async Task Put_InvalidRates_Returns400()
    {
        var request = ValidRequest() with { EnergyRate = -1m, MaxSupportedKwh = 0 };

        var response = await _client.PutAsJsonAsync("/api/tariffs/invalid-test", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_InvalidCode_Returns400()
    {
        var response = await _client.PutAsJsonAsync("/api/tariffs/has%20space", ValidRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

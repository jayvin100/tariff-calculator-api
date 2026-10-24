using System.Text.Json;
using TariffCalculator.Api.Ai;
using TariffCalculator.Core;

namespace TariffCalculator.Api.Tests;

// plain unit tests, no database or Claude needed
public class BillToolsTests
{
    private static readonly Tariff Domestic = new()
    {
        Code = "DOMESTIC",
        Name = "Domestic",
        EnergyRate = 0.2703m,
        CapacityRate = 0.0455m,
        NetworkRate = 0.1285m,
        RetailCharge = 10.00m,
        RetailWaiverLimitKwh = 600,
        MaxSupportedKwh = 1500,
    };

    private readonly BillTools _tools = new([Domestic], new BillCalculator());

    private static Dictionary<string, JsonElement> Input(object values) =>
        JsonSerializer.SerializeToElement(values).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());

    [Fact]
    public void CalculateBill_ReturnsTotalAndRecordsCalculation()
    {
        var result = _tools.Execute(BillTools.CalculateBill, Input(new { kwh = 601 }));

        Assert.False(result.IsError);
        Assert.Equal(277.02m, JsonDocument.Parse(result.Content).RootElement.GetProperty("total").GetDecimal());
        var calculation = Assert.Single(_tools.Calculations);
        Assert.Equal(("DOMESTIC", 601), (calculation.Tariff, calculation.KWh));
    }

    [Fact]
    public void CalculateBill_TariffCodeIsCaseInsensitive()
    {
        var result = _tools.Execute(BillTools.CalculateBill, Input(new { kwh = 100, tariff_code = "domestic" }));

        Assert.False(result.IsError);
    }

    [Theory]
    [InlineData("{}")]                                  // missing kwh
    [InlineData("""{"kwh":"lots"}""")]                  // wrong type
    [InlineData("""{"kwh":12.5}""")]                    // not a whole number
    [InlineData("""{"kwh":2000}""")]                    // above max supported
    [InlineData("""{"kwh":100,"tariff_code":"NOPE"}""")] // unknown tariff
    public void CalculateBill_BadInput_ReturnsErrorForClaude(string json)
    {
        var input = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

        var result = _tools.Execute(BillTools.CalculateBill, input);

        Assert.True(result.IsError);
        Assert.Empty(_tools.Calculations);
    }

    [Fact]
    public void ListTariffs_ReturnsConfiguredTariffs()
    {
        var result = _tools.Execute(BillTools.ListTariffs, new Dictionary<string, JsonElement>());

        var tariffs = JsonDocument.Parse(result.Content).RootElement;
        Assert.Equal("DOMESTIC", tariffs[0].GetProperty("code").GetString());
    }

    [Fact]
    public void UnknownTool_ReturnsError()
    {
        Assert.True(_tools.Execute("delete_everything", new Dictionary<string, JsonElement>()).IsError);
    }
}

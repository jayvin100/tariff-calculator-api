using System.Globalization;
using TariffCalculator.Core;

namespace TariffCalculator.Tests;

public class BillCalculatorTests
{
    private readonly BillCalculator _calculator = new();

    // same rates as the seeded DOMESTIC tariff, pinned here so the expected totals don't drift
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

    // [Theory] = one test, run once per [InlineData] row
    // expected totals are strings because attributes can't hold decimals
    [Theory]
    [InlineData(0, "0.00")]
    [InlineData(200, "88.86")]
    [InlineData(600, "266.58")]   // last kWh with no retail charge
    [InlineData(601, "277.02")]   // first kWh WITH the RM10 charge
    [InlineData(1500, "676.45")]  // max supported usage
    public void Calculate_ReturnsExpectedTotal(int kWh, string expected)
    {
        // Arrange is done above, Act
        var result = _calculator.Calculate(Domestic, kWh);

        // Assert
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), result.Total);
    }

    // [Fact] = a single test with no parameters
    [Fact]
    public void Calculate_ChargesRetailOnlyAboveWaiverLimit()
    {
        Assert.Equal(0m, _calculator.Calculate(Domestic, 600).Retail);
        Assert.Equal(10.00m, _calculator.Calculate(Domestic, 601).Retail);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(1501)]
    public void Calculate_UsageOutOfRange_Throws(int kWh)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Calculate(Domestic, kWh));
    }
}

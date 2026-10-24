using System.Globalization;
using TariffCalculator.Core;

namespace TariffCalculator.Tests;

public class BillCalculatorTests
{
    private readonly BillCalculator _calculator = new();

    // [Theory] = one test, run once per [InlineData] row
    // expected totals are strings because attributes can't hold decimals
    [Theory]
    [InlineData(0, "0.00")]
    [InlineData(200, "88.86")]
    [InlineData(600, "266.58")]   // last kWh with no retail charge
    [InlineData(601, "277.02")]   // first kWh WITH the RM10 charge
    public void Calculate_ReturnsExpectedTotal(int kWh, string expected)
    {
        // Arrange is done above, Act
        var result = _calculator.Calculate(kWh);

        // Assert
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), result.Total);
    }

    // [Fact] = a single test with no parameters
    [Fact]
    public void Calculate_NegativeUsage_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Calculate(-5));
    }
}
namespace TariffCalculator.Core;

// a record is a small immutable data class: perfect for a result
public record BillBreakdown(decimal Energy, decimal Capacity, decimal Network, decimal Retail, decimal Total);

public class BillCalculator
{
    // named constants instead of magic numbers "m" means decimal
    private const decimal EnergyRate = 0.2703m;   // RM per kWh
    private const decimal CapacityRate = 0.0455m;
    private const decimal NetworkRate = 0.1285m;
    private const decimal RetailCharge = 10.00m;
    private const int RetailWaiverLimitKwh = 600;
    private const int MaxSupportedKwh = 1500;

    public BillBreakdown Calculate(int kWh)
    {
        // Guard clause: reject bad input before doing any maths just in case.
        if (kWh < 0 || kWh > MaxSupportedKwh)
            throw new ArgumentOutOfRangeException(nameof(kWh), kWh,
                $"Usage must be between 0 and {MaxSupportedKwh} kWh.");

        decimal energy = kWh * EnergyRate;
        decimal capacity = kWh * CapacityRate;
        decimal network = kWh * NetworkRate;
        decimal retail = kWh > RetailWaiverLimitKwh ? RetailCharge : 0m;

        // round only once, at the end, so small errors don't pile up.
        decimal total = Math.Round(energy + capacity + network + retail, 2, MidpointRounding.AwayFromZero);

        return new BillBreakdown(energy, capacity, network, retail, total);
    }
}
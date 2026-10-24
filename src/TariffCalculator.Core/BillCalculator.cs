namespace TariffCalculator.Core;

// a record is a small immutable data class: perfect for a result
public record BillBreakdown(decimal Energy, decimal Capacity, decimal Network, decimal Retail, decimal Total);

public class BillCalculator
{
    public BillBreakdown Calculate(Tariff tariff, int kWh)
    {
        // Guard clause: reject bad input before doing any maths just in case.
        if (kWh < 0 || kWh > tariff.MaxSupportedKwh)
            throw new ArgumentOutOfRangeException(nameof(kWh), kWh,
                $"Usage must be between 0 and {tariff.MaxSupportedKwh} kWh.");

        decimal energy = kWh * tariff.EnergyRate;
        decimal capacity = kWh * tariff.CapacityRate;
        decimal network = kWh * tariff.NetworkRate;
        decimal retail = kWh > tariff.RetailWaiverLimitKwh ? tariff.RetailCharge : 0m;

        // round only once, at the end, so small errors don't pile up.
        decimal total = Math.Round(energy + capacity + network + retail, 2, MidpointRounding.AwayFromZero);

        return new BillBreakdown(energy, capacity, network, retail, total);
    }
}

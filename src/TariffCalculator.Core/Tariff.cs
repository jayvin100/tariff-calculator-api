namespace TariffCalculator.Core;

// One row per tariff. Rates live in the database now, so they can change without a redeploy.
public class Tariff
{
    public int Id { get; set; }
    public required string Code { get; set; }        // e.g. "DOMESTIC", used in URLs
    public required string Name { get; set; }
    public decimal EnergyRate { get; set; }          // RM per kWh
    public decimal CapacityRate { get; set; }        // RM per kWh
    public decimal NetworkRate { get; set; }         // RM per kWh
    public decimal RetailCharge { get; set; }        // flat RM per month
    public int RetailWaiverLimitKwh { get; set; }    // retail charge is waived up to this usage
    public int MaxSupportedKwh { get; set; }
}

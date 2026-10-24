using Microsoft.EntityFrameworkCore;
using TariffCalculator.Core;

namespace TariffCalculator.Api.Data;

public class TariffDbContext(DbContextOptions<TariffDbContext> options) : DbContext(options)
{
    public DbSet<Tariff> Tariffs => Set<Tariff>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var tariff = modelBuilder.Entity<Tariff>();

        tariff.HasIndex(t => t.Code).IsUnique();
        tariff.Property(t => t.Code).HasMaxLength(32);
        tariff.Property(t => t.Name).HasMaxLength(100);

        // SQL Server needs an explicit precision for decimals, otherwise EF warns and uses (18,2)
        // which would cut 0.2703 down to 0.27
        tariff.Property(t => t.EnergyRate).HasPrecision(10, 4);
        tariff.Property(t => t.CapacityRate).HasPrecision(10, 4);
        tariff.Property(t => t.NetworkRate).HasPrecision(10, 4);
        tariff.Property(t => t.RetailCharge).HasPrecision(10, 2);

        // seed data goes into the migration, so a fresh database starts with the domestic tariff
        tariff.HasData(new Tariff
        {
            Id = 1,
            Code = "DOMESTIC",
            Name = "Domestic",
            EnergyRate = 0.2703m,
            CapacityRate = 0.0455m,
            NetworkRate = 0.1285m,
            RetailCharge = 10.00m,
            RetailWaiverLimitKwh = 600,
            MaxSupportedKwh = 1500,
        });
    }
}

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using TariffCalculator.Api.Data;
using TariffCalculator.Core;

namespace TariffCalculator.Api.Endpoints;

public static class BillEndpoints
{
    public const string DefaultTariff = "DOMESTIC";

    public static void MapBillEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/bills").WithTags("Bills");

        // GET /api/bills/calculate?kWh=601&tariff=DOMESTIC
        group.MapGet("/calculate", async Task<Results<Ok<BillBreakdown>, ProblemHttpResult, ValidationProblem>> (
            int kWh, string? tariff, TariffDbContext db, BillCalculator calculator) =>
        {
            var code = (tariff ?? DefaultTariff).ToUpperInvariant();
            var found = await db.Tariffs.AsNoTracking().SingleOrDefaultAsync(t => t.Code == code);
            if (found is null)
                return TariffEndpoints.TariffNotFound(code);

            if (kWh < 0 || kWh > found.MaxSupportedKwh)
                return UsageOutOfRange(found);

            return TypedResults.Ok(calculator.Calculate(found, kWh));    // 200 + JSON breakdown
        });
    }

    public static ValidationProblem UsageOutOfRange(Tariff tariff) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["kWh"] = [$"Usage must be between 0 and {tariff.MaxSupportedKwh} kWh."],
        });
}

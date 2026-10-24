using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using TariffCalculator.Api.Data;
using TariffCalculator.Core;

namespace TariffCalculator.Api.Endpoints;

public record TariffResponse(
    string Code,
    string Name,
    decimal EnergyRate,
    decimal CapacityRate,
    decimal NetworkRate,
    decimal RetailCharge,
    int RetailWaiverLimitKwh,
    int MaxSupportedKwh)
{
    public static TariffResponse From(Tariff t) => new(
        t.Code, t.Name, t.EnergyRate, t.CapacityRate, t.NetworkRate,
        t.RetailCharge, t.RetailWaiverLimitKwh, t.MaxSupportedKwh);
}

// validation attributes are checked automatically by AddValidation() before the handler runs
public record TariffRequest(
    [property: Required, StringLength(100)] string Name,
    [property: Range(0.0, 10.0)] decimal EnergyRate,
    [property: Range(0.0, 10.0)] decimal CapacityRate,
    [property: Range(0.0, 10.0)] decimal NetworkRate,
    [property: Range(0.0, 1000.0)] decimal RetailCharge,
    [property: Range(0, 100_000)] int RetailWaiverLimitKwh,
    [property: Range(1, 100_000)] int MaxSupportedKwh);

public static class TariffEndpoints
{
    public static void MapTariffEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tariffs").WithTags("Tariffs");

        // GET /api/tariffs
        group.MapGet("/", async (TariffDbContext db) =>
        {
            var tariffs = await db.Tariffs.AsNoTracking().OrderBy(t => t.Code).ToListAsync();
            return TypedResults.Ok(tariffs.Select(TariffResponse.From));
        });

        // GET /api/tariffs/DOMESTIC
        group.MapGet("/{code}", async Task<Results<Ok<TariffResponse>, ProblemHttpResult>> (string code, TariffDbContext db) =>
        {
            var tariff = await db.Tariffs.AsNoTracking().SingleOrDefaultAsync(t => t.Code == code.ToUpperInvariant());
            return tariff is null
                ? TariffNotFound(code)
                : TypedResults.Ok(TariffResponse.From(tariff));
        });

        // PUT /api/tariffs/DOMESTIC: updates the tariff, or creates it if the code is new
        group.MapPut("/{code}", async Task<Results<Ok<TariffResponse>, Created<TariffResponse>, ValidationProblem>> (
            string code, TariffRequest request, TariffDbContext db) =>
        {
            code = code.ToUpperInvariant();
            if (code.Length > 32 || !code.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["code"] = ["Code must be up to 32 letters, digits, '_' or '-'."],
                });

            var tariff = await db.Tariffs.SingleOrDefaultAsync(t => t.Code == code);
            var isNew = tariff is null;
            tariff ??= db.Tariffs.Add(new Tariff { Code = code, Name = request.Name }).Entity;

            tariff.Name = request.Name;
            tariff.EnergyRate = request.EnergyRate;
            tariff.CapacityRate = request.CapacityRate;
            tariff.NetworkRate = request.NetworkRate;
            tariff.RetailCharge = request.RetailCharge;
            tariff.RetailWaiverLimitKwh = request.RetailWaiverLimitKwh;
            tariff.MaxSupportedKwh = request.MaxSupportedKwh;
            await db.SaveChangesAsync();

            var response = TariffResponse.From(tariff);
            return isNew
                ? TypedResults.Created($"/api/tariffs/{code}", response)
                : TypedResults.Ok(response);
        });
    }

    public static ProblemHttpResult TariffNotFound(string code) =>
        TypedResults.Problem(statusCode: StatusCodes.Status404NotFound,
            title: "Tariff not found", detail: $"Tariff '{code.ToUpperInvariant()}' does not exist.");
}

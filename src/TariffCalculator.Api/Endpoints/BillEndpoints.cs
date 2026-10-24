using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using TariffCalculator.Api.Ai;
using TariffCalculator.Api.Data;
using TariffCalculator.Core;

namespace TariffCalculator.Api.Endpoints;

public record ExplainRequest(int KWh, [property: StringLength(32)] string? Tariff);

public record ExplainResponse(string Tariff, int KWh, BillBreakdown Bill, string Explanation);

public static class BillEndpoints
{
    public const string DefaultTariff = "DOMESTIC";

    public static void MapBillEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/bills").WithTags("Bills");

        // GET /api/bills/calculate?kWh=601&tariff=DOMESTIC
        group.MapGet("/calculate", async Task<Results<Ok<BillBreakdown>, ProblemHttpResult>> (
            int kWh, string? tariff, TariffDbContext db, BillCalculator calculator) =>
        {
            var (found, error) = await FindTariffForUsage(db, tariff, kWh);
            if (error is not null)
                return error;

            return TypedResults.Ok(calculator.Calculate(found!, kWh));   // 200 + JSON breakdown
        });

        // POST /api/bills/explain  { "kWh": 601, "tariff": "DOMESTIC" }
        // calculates the bill as usual, then asks Claude to explain it in plain English
        group.MapPost("/explain", async Task<Results<Ok<ExplainResponse>, ProblemHttpResult>> (
            ExplainRequest request, TariffDbContext db, BillCalculator calculator, IBillAssistant assistant, CancellationToken ct) =>
        {
            var (found, error) = await FindTariffForUsage(db, request.Tariff, request.KWh);
            if (error is not null)
                return error;

            var bill = calculator.Calculate(found!, request.KWh);
            try
            {
                var explanation = await assistant.ExplainAsync(found!, request.KWh, bill, ct);
                return TypedResults.Ok(new ExplainResponse(found!.Code, request.KWh, bill, explanation));
            }
            catch (Exception ex) when (ex is AssistantUnavailableException or AssistantRefusedException)
            {
                return AssistantProblem(ex);
            }
        });
    }

    // shared lookup + range check, returns either the tariff or the error response to send back
    private static async Task<(Tariff? Tariff, ProblemHttpResult? Error)> FindTariffForUsage(TariffDbContext db, string? tariff, int kWh)
    {
        var code = (tariff ?? DefaultTariff).ToUpperInvariant();
        var found = await db.Tariffs.AsNoTracking().SingleOrDefaultAsync(t => t.Code == code);
        if (found is null)
            return (null, TariffEndpoints.TariffNotFound(code));

        if (kWh < 0 || kWh > found.MaxSupportedKwh)
            return (null, TypedResults.Problem(new HttpValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["kWh"] = [$"Usage must be between 0 and {found.MaxSupportedKwh} kWh."],
            }) { Status = StatusCodes.Status400BadRequest }));

        return (found, null);
    }

    public static ProblemHttpResult AssistantProblem(Exception ex) => ex is AssistantRefusedException
        ? TypedResults.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Request declined", detail: ex.Message)
        : TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "AI unavailable", detail: ex.Message);
}

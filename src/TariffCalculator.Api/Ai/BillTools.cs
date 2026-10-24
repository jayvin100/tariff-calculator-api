using System.Text.Json;
using TariffCalculator.Api.Endpoints;
using TariffCalculator.Core;

namespace TariffCalculator.Api.Ai;

public record BillCalculation(string Tariff, int KWh, BillBreakdown Bill);

public record ToolResult(string Content, bool IsError);

// The tools Claude can call while answering a question. Plain C#, no SDK types, so it's easy to unit test.
// One instance per question: it also records every calculation so the API can show its working.
public class BillTools(IReadOnlyList<Tariff> tariffs, BillCalculator calculator)
{
    public const string ListTariffs = "list_tariffs";
    public const string CalculateBill = "calculate_bill";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public List<BillCalculation> Calculations { get; } = [];

    public ToolResult Execute(string name, IReadOnlyDictionary<string, JsonElement> input) => name switch
    {
        ListTariffs => Ok(tariffs.Select(TariffResponse.From)),
        CalculateBill => Calculate(input),
        _ => Error($"Unknown tool '{name}'."),
    };

    private ToolResult Calculate(IReadOnlyDictionary<string, JsonElement> input)
    {
        // tool input comes from the model, so validate it like any other user input
        if (!input.TryGetValue("kwh", out var kwhJson) || kwhJson.ValueKind != JsonValueKind.Number || !kwhJson.TryGetInt32(out var kWh))
            return Error("'kwh' must be a whole number.");

        var code = BillEndpoints.DefaultTariff;
        if (input.TryGetValue("tariff_code", out var codeJson) && codeJson.ValueKind == JsonValueKind.String)
            code = codeJson.GetString()!.ToUpperInvariant();

        var tariff = tariffs.SingleOrDefault(t => t.Code == code);
        if (tariff is null)
            return Error($"Tariff '{code}' does not exist. Call {ListTariffs} to see the available tariffs.");

        if (kWh < 0 || kWh > tariff.MaxSupportedKwh)
            return Error($"Usage must be between 0 and {tariff.MaxSupportedKwh} kWh for tariff {code}.");

        var bill = calculator.Calculate(tariff, kWh);
        Calculations.Add(new BillCalculation(code, kWh, bill));

        return Ok(new
        {
            tariff = code,
            kWh,
            energy = Round(bill.Energy),
            capacity = Round(bill.Capacity),
            network = Round(bill.Network),
            retail = bill.Retail,
            total = bill.Total,
        });
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static ToolResult Ok(object value) => new(JsonSerializer.Serialize(value, Json), IsError: false);

    private static ToolResult Error(string message) => new(message, IsError: true);
}

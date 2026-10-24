using TariffCalculator.Core;

namespace TariffCalculator.Api.Ai;

// The AI only ever talks about numbers that BillCalculator produced, it never does the maths itself.
public interface IBillAssistant
{
    Task<string> ExplainAsync(Tariff tariff, int kWh, BillBreakdown bill, CancellationToken ct = default);
}

// AI isn't configured, or the Claude API is down / rate limited
public class AssistantUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

// Claude declined to answer (stop_reason "refusal")
public class AssistantRefusedException() : Exception("The assistant declined to answer this request.");

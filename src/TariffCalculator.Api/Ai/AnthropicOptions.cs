namespace TariffCalculator.Api.Ai;

// bound from the "Anthropic" config section, the key comes from user-secrets or an env var, never appsettings
public class AnthropicOptions
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "claude-opus-5-5";
}

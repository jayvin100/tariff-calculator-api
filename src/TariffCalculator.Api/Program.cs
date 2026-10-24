using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using TariffCalculator.Api.Ai;
using TariffCalculator.Api.Data;
using TariffCalculator.Api.Endpoints;
using TariffCalculator.Core;

var builder = WebApplication.CreateBuilder(args);

// DI: register the calculator once, and ASP.NET hands it to any endpoint that asks for it
builder.Services.AddSingleton<BillCalculator>();

// DbContext is scoped: one per request
builder.Services.AddDbContext<TariffDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Claude: key from user-secrets / Anthropic__ApiKey, or the standard ANTHROPIC_API_KEY env var
builder.Services.Configure<AnthropicOptions>(builder.Configuration.GetSection("Anthropic"));
builder.Services.PostConfigure<AnthropicOptions>(o => o.ApiKey ??= Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));
builder.Services.AddSingleton<IBillAssistant, ClaudeBillAssistant>();

builder.Services.AddHealthChecks().AddDbContextCheck<TariffDbContext>();
builder.Services.AddProblemDetails();
builder.Services.AddValidation();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// apply pending migrations on startup (dev + docker only, controlled by config)
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<TariffDbContext>().Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();   // interactive API docs at /scalar
}

// no UseHttpsRedirection: in Docker the API only listens on HTTP, TLS belongs to whatever proxy sits in front

app.MapHealthChecks("/health");
app.MapTariffEndpoints();
app.MapBillEndpoints();

app.Run();

// lets the integration tests reference Program through WebApplicationFactory<Program>
public partial class Program;

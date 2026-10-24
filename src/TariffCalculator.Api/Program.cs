using Microsoft.EntityFrameworkCore;
using TariffCalculator.Api.Data;
using TariffCalculator.Core;

var builder = WebApplication.CreateBuilder(args);

// DI: register the calculator once, and ASP.NET hands it to any endpoint that asks for it
builder.Services.AddSingleton<BillCalculator>();

// DbContext is scoped: one per request
builder.Services.AddDbContext<TariffDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Add services to the container.
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
}

app.UseHttpsRedirection();

// GET /api/bills/calculate?kWh=601&tariff=DOMESTIC
app.MapGet("/api/bills/calculate", async (int kWh, string? tariff, TariffDbContext db, BillCalculator calculator) =>
{
    var code = (tariff ?? "DOMESTIC").ToUpperInvariant();
    var found = await db.Tariffs.AsNoTracking().SingleOrDefaultAsync(t => t.Code == code);
    if (found is null)
        return Results.NotFound(new { error = $"Tariff '{code}' not found." });

    try
    {
        return Results.Ok(calculator.Calculate(found, kWh));     // 200 + JSON breakdown
    }
    catch (ArgumentOutOfRangeException ex)
    {
        return Results.BadRequest(new { error = ex.Message });   // 400 for bad input
    }
});

app.Run();

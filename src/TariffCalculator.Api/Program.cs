using TariffCalculator.Core;

var builder = WebApplication.CreateBuilder(args);

// DI: register the calculator once, and ASP.NET hands it to any endpoint that asks for it
builder.Services.AddSingleton<BillCalculator>();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// GET /api/bills/calculate?kWh=601
app.MapGet("/api/bills/calculate", (int kWh, BillCalculator calculator) =>
{
    try
    {
        return Results.Ok(calculator.Calculate(kWh));            // 200 + JSON breakdown
    }
    catch (ArgumentOutOfRangeException ex)
    {
        return Results.BadRequest(new { error = ex.Message });   // 400 for bad input
    }
});

app.Run();

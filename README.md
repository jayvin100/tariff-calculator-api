# tariff-calculator-api

![CI](https://github.com/jayvin100/tariff-calculator-api/actions/workflows/ci.yml/badge.svg)

A small ASP.NET Core API that calculates monthly electricity bills from tariffs stored in SQL Server, and uses Claude to explain bills and answer questions about them in plain English.

The seeded `DOMESTIC` tariff is based on Malaysia's 2025 residential tariff: energy, capacity and network charges per kWh, plus a RM10 retail charge that's waived at 600 kWh or less.

**Stack:** .NET 10, minimal APIs, EF Core 10 + SQL Server, xUnit + Testcontainers, Docker Compose, GitHub Actions, Anthropic C# SDK

## Endpoints

| Method | Route | What it does |
| --- | --- | --- |
| `GET` | `/api/bills/calculate?kWh=601&tariff=DOMESTIC` | Bill breakdown (tariff defaults to `DOMESTIC`) |
| `POST` | `/api/bills/explain` | Calculates the bill, then Claude explains it and suggests ways to save |
| `POST` | `/api/bills/ask` | Ask a question in plain English, Claude answers using the calculator as a tool |
| `GET` | `/api/tariffs` | List tariffs |
| `GET` | `/api/tariffs/{code}` | Get one tariff |
| `PUT` | `/api/tariffs/{code}` | Create or update a tariff |
| `GET` | `/health` | Health check (includes the database) |

Interactive docs are at `/scalar` in Development.

```bash
curl "http://localhost:8080/api/bills/calculate?kWh=601"
# {"energy":162.4503,"capacity":27.3455,"network":77.2285,"retail":10.00,"total":277.02}

curl -X POST http://localhost:8080/api/bills/ask \
  -H "Content-Type: application/json" \
  -d '{"question":"I used 650 kWh last month. What is my bill, and how much would I save if I got under the RM10 retail charge?"}'
```

The `ask` response includes the answer plus every calculation Claude ran to get there:

```json
{
  "answer": "Your 650 kWh last month on the Domestic tariff comes to RM298.80 ...",
  "calculations": [
    { "tariff": "DOMESTIC", "kWh": 650, "bill": { "total": 298.80, "...": "..." } },
    { "tariff": "DOMESTIC", "kWh": 600, "bill": { "total": 266.58, "...": "..." } }
  ]
}
```

## How the AI part works

The main rule: **Claude never does the bill maths.** LLMs aren't reliable calculators, and these are money amounts.

- **Explain** (`/api/bills/explain`): `BillCalculator` works out the bill first. Claude only gets the finished numbers and is told to quote them, not recalculate.
- **Ask** (`/api/bills/ask`): Claude gets two tools, `calculate_bill` and `list_tariffs`. When it needs a number it calls `calculate_bill`, and the API runs the real `BillCalculator` and sends the result back. This loops until Claude has an answer, up to 8 rounds. See `Ai/ClaudeBillAssistant.cs`.
- Tool input from the model is validated like any user input (`Ai/BillTools.cs`). Bad input goes back to Claude as an error so it can fix the call itself.
- If Claude isn't configured or the API is down, the AI endpoints return `503`, and everything else keeps working.
- Tests use a fake assistant, so CI never calls the real API and needs no key.

## Running it

### Docker (easiest)

```bash
export ANTHROPIC_API_KEY=sk-ant-...   # optional, only needed for /explain and /ask
docker compose up --build
```

The API runs at http://localhost:8080 (docs at http://localhost:8080/scalar) and SQL Server at `localhost:1433`. Migrations run automatically on startup.

> On Apple Silicon, SQL Server only ships an x64 image, so Docker needs Rosetta emulation turned on (Docker Desktop: *Use Rosetta for x86/amd64 emulation*; Colima: `colima start --vz-rosetta`).

### Locally with `dotnet run`

```bash
docker compose up -d db                       # just the database
dotnet tool restore                           # dotnet-ef
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..." --project src/TariffCalculator.Api
dotnet run --project src/TariffCalculator.Api # http://localhost:5094
```

`src/TariffCalculator.Api/TariffCalculator.Api.http` has ready-made requests for every endpoint.

### Configuration

| Key | Default | Notes |
| --- | --- | --- |
| `ConnectionStrings:Default` | local dev SQL Server | |
| `Database:MigrateOnStartup` | `true` in Development | Applies EF migrations at startup |
| `Anthropic:ApiKey` | - | Falls back to the `ANTHROPIC_API_KEY` env var. Keep it in user-secrets or env, never in appsettings |
| `Anthropic:Model` | `claude-opus-5-5` | Any Claude model id |

The SQL password in `appsettings.Development.json` and `docker-compose.yml` is a local dev password only. Override it with `MSSQL_SA_PASSWORD`.

## Tests

```bash
dotnet test
```

- `TariffCalculator.Tests`: unit tests for `BillCalculator`, including the edge cases around the 600 kWh retail waiver
- `TariffCalculator.Api.Tests`: integration tests that boot the real API with `WebApplicationFactory` against a real SQL Server started by Testcontainers (needs Docker), plus unit tests for the AI tools

CI runs the build, all tests and a Docker image build on every push and pull request.

## Project layout

```
src/
  TariffCalculator.Core/     BillCalculator + Tariff model, no dependencies
  TariffCalculator.Api/
    Ai/                      Claude integration (explain + tool use)
    Data/                    EF Core DbContext + migrations
    Endpoints/               minimal API endpoint groups
tests/
  TariffCalculator.Tests/
  TariffCalculator.Api.Tests/
```

## Migrations

```bash
dotnet ef migrations add <Name> -p src/TariffCalculator.Api -o Data/Migrations
```

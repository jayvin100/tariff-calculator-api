using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TariffCalculator.Api.Ai;
using Testcontainers.MsSql;

namespace TariffCalculator.Api.Tests;

// Boots the real API in memory, pointed at a throwaway SQL Server running in Docker.
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public FakeBillAssistant Assistant { get; } = new();

    public Task InitializeAsync() => _sql.StartAsync();

    public new Task DisposeAsync() => _sql.DisposeAsync().AsTask();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _sql.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");

        // never call the real Claude API from tests
        builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IBillAssistant>(Assistant)));
    }
}

// all test classes in this collection share one container, starting SQL Server is the slow part
[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}

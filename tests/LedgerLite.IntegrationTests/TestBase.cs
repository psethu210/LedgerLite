using LedgerLite.Infrastructure.Data;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Testcontainers.MsSql;

namespace LedgerLite.IntegrationTests;

/// <summary>
/// Base class for integration tests using Testcontainers for SQL Server.
/// </summary>
public class TestBase : IAsyncLifetime
{
    protected readonly MsSqlContainer _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("LedgerLite_Test1!")
        .WithCleanUp(true)
        .Build();

    protected WebApplicationFactory<Program> _factory = null!;
    protected HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        // Start SQL Server container
        await _sqlContainer.StartAsync();

        // Create factory with test configuration
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove the existing DbContext registration
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<LedgerContext>));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    // Add test database context
                    services.AddDbContext<LedgerContext>(options =>
                    {
                        options.UseSqlServer(_sqlContainer.GetConnectionString());
                        options.EnableSensitiveDataLogging();
                    });
                });

                // Use test environment
                builder.UseEnvironment("Testing");
            });

        _client = _factory.CreateClient();

        // Apply migrations to the test database
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<LedgerContext>();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory != null)
            await _factory.DisposeAsync();
        await _sqlContainer.DisposeAsync();
    }

    /// <summary>
    /// Creates a fresh database context for direct database operations in tests.
    /// </summary>
    protected LedgerContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<LedgerContext>()
            .UseSqlServer(_sqlContainer.GetConnectionString())
            .EnableSensitiveDataLogging()
            .Options;

        return new LedgerContext(options);
    }

    /// <summary>
    /// Seeds test data and returns the created entities.
    /// </summary>
    protected async Task<TestData> SeedTestDataAsync()
    {
        using var context = CreateDbContext();

        var usdAccount = LedgerLite.Domain.Account.CreateCustomerAccount("test-user", LedgerLite.Domain.Currency.USD);
        var cadAccount = LedgerLite.Domain.Account.CreateCustomerAccount("test-user", LedgerLite.Domain.Currency.CAD);

        context.Accounts.AddRange(usdAccount, cadAccount);
        await context.SaveChangesAsync();

        return new TestData
        {
            UsdAccountId = usdAccount.Id,
            CadAccountId = cadAccount.Id
        };
    }

    /// <summary>
    /// Makes an HTTP request with the required API key header.
    /// </summary>
    protected Task<HttpResponseMessage> SendWithApiKeyAsync(HttpRequestMessage request, string apiKey = "demo-key-1")
    {
        request.Headers.Add("X-Api-Key", apiKey);
        return _client.SendAsync(request);
    }

    /// <summary>
    /// Makes an HTTP request with API key and idempotency key headers.
    /// </summary>
    protected Task<HttpResponseMessage> SendWithHeadersAsync(HttpRequestMessage request, string idempotencyKey, string apiKey = "demo-key-1")
    {
        request.Headers.Add("X-Api-Key", apiKey);
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return _client.SendAsync(request);
    }
}

public class TestData
{
    public Guid UsdAccountId { get; set; }
    public Guid CadAccountId { get; set; }
}
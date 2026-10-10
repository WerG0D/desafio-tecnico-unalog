using System.Net;
using System.Net.Http.Json;
using DriverRegistration.Application;
using DriverRegistration.Domain;
using DriverRegistration.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DriverRegistration.IntegrationTests;

public sealed class ConcurrencyAndDurabilityTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Repository_ActiveWriterLock_PropagatesBusyInsteadOfRegistrationConflict()
    {
        using var database = new TemporaryDatabase(timeout: 1);
        await using var provider = CreateProvider(database.ConnectionString);
        await MigrateAsync(provider);
        await using var writer = new SqliteConnection(database.ConnectionString);
        await writer.OpenAsync(Token);
        // A transação imediata adquire o bloqueio de escrita antes da segunda conexão.
        await using var transaction = writer.BeginTransaction(deferred: false);

        var attempt = Task.Run(async () =>
        {
            await using var scope = provider.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<DriverDbContext>().Database.SetCommandTimeout(1);
            return await scope.ServiceProvider.GetRequiredService<IDriverRepository>().AddAsync(CreateDriver(), Token);
        }, Token);
        var failure = await Assert.ThrowsAsync<DbUpdateException>(async () =>
            await attempt.WaitAsync(TimeSpan.FromSeconds(10), Token));

        var sqlite = Assert.IsType<SqliteException>(failure.InnerException);
        Assert.Contains(sqlite.SqliteErrorCode, new[] { 5, 6 });
        await transaction.RollbackAsync(Token);
        await using var verification = provider.CreateAsyncScope();
        Assert.Empty(await verification.ServiceProvider.GetRequiredService<DriverDbContext>().Drivers.ToListAsync(Token));
    }

    [Fact]
    public async Task Post_ConcurrentDuplicateRequests_ReturnsCreatedAndConflictWithOnePersistedDriver()
    {
        using var database = new TemporaryDatabase(timeout: 10);
        await using var factory = new FileDatabaseApiFactory(database.ConnectionString);
        await MigrateAsync(factory.Services);
        using var client = factory.CreateClient();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = SendAsync();
        var second = SendAsync();

        start.SetResult();
        var responses = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(20), Token);
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            await using var scope = factory.Services.CreateAsyncScope();
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<DriverDbContext>().Drivers.CountAsync(Token));
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }

        Task<HttpResponseMessage> SendAsync() => Task.Run(async () =>
        {
            await start.Task.WaitAsync(Token);
            return await client.PostAsJsonAsync("/api/v1/drivers", DriverApiTests.ValidPayload(), Token);
        }, Token);
    }

    [Fact]
    public async Task Repository_AfterAllConnectionsClose_NewProviderReadsPersistedRegistration()
    {
        using var database = new TemporaryDatabase(timeout: 10);
        var driver = CreateDriver();
        await using (var original = CreateProvider(database.ConnectionString))
        {
            await MigrateAsync(original);
            await using var scope = original.CreateAsyncScope();
            Assert.True(await scope.ServiceProvider.GetRequiredService<IDriverRepository>().AddAsync(driver, Token));
        }

        await using var reopened = CreateProvider(database.ConnectionString);
        await using var verification = reopened.CreateAsyncScope();
        var persisted = await verification.ServiceProvider.GetRequiredService<DriverDbContext>()
            .Drivers.AsNoTracking().SingleAsync(Token);

        Assert.Equal(driver.Id, persisted.Id);
        Assert.Equal(driver.Name, persisted.Name);
        Assert.Equal(driver.Cpf, persisted.Cpf);
        Assert.Equal(driver.Phone, persisted.Phone);
        Assert.Equal(driver.Email, persisted.Email);
        Assert.Equal(driver.CnhNumber, persisted.CnhNumber);
        Assert.Equal(driver.CnhCategory, persisted.CnhCategory);
        Assert.Equal(driver.CnhExpirationDate, persisted.CnhExpirationDate);
    }

    private static Driver CreateDriver() => Driver.Create("Ana Silva", "52998224725", "31999999999",
        "ana@example.com", "12345678901", "D", new DateOnly(2026, 10, 11), new DateOnly(2026, 10, 10));

    private static ServiceProvider CreateProvider(string connectionString) =>
        new ServiceCollection().AddInfrastructure(connectionString).BuildServiceProvider();

    private static async Task MigrateAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DriverDbContext>().Database.MigrateAsync(Token);
    }

    private sealed class TemporaryDatabase(int timeout) : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"driver-registration-qa-{Guid.NewGuid():N}.db");
        public string ConnectionString => new SqliteConnectionStringBuilder
        {
            DataSource = _path, Pooling = false, DefaultTimeout = timeout
        }.ToString();

        public void Dispose()
        {
            File.Delete(_path);
            File.Delete(_path + "-wal");
            File.Delete(_path + "-shm");
            File.Delete(_path + "-journal");
        }
    }

    private sealed class FileDatabaseApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Database:ApplyMigrations"] = "false" }));
            builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DriverDbContext>();
                services.RemoveAll<DbContextOptions<DriverDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<DriverDbContext>>();
                services.AddInfrastructure(connectionString);
                services.RemoveAll<IRegistrationDateProvider>();
                services.AddSingleton<IRegistrationDateProvider, FixedDateProvider>();
            });
        }
    }

    private sealed class FixedDateProvider : IRegistrationDateProvider
    {
        public DateOnly Today => new(2026, 10, 10);
    }
}

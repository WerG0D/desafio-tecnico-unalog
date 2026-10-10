using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using DriverRegistration.Application;
using DriverRegistration.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DriverRegistration.IntegrationTests;

public sealed class RealPersistenceLoggingTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false, "database_error", 1)]
    [InlineData(true, "integrity_constraint", 19)]
    public async Task RealDatabaseFailures_LogSafeCodesAndCategory(bool createFailingTrigger, string category, int code)
    {
        using var logs = new CapturingLogs();
        await using var factory = new DriverApiFactory();
        await using var monitored = factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        if (createFailingTrigger)
        {
            await using var scope = monitored.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<DriverDbContext>();
            await db.Database.MigrateAsync(Token);
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER ForceCheckFailure BEFORE INSERT ON Drivers BEGIN SELECT RAISE(ABORT, 'private-database-sentinel'); END;", Token);
        }
        using var client = monitored.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/drivers", DriverApiTests.ValidPayload(), Token);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var diagnostic = Assert.Single(logs.Events, item => item.Properties.ContainsKey("SqliteErrorCode"));
        Assert.Equal(code, diagnostic.Properties["SqliteErrorCode"]);
        Assert.Equal(category, diagnostic.Properties["FailureCategory"]);
        Assert.NotNull(diagnostic.Properties["SqliteExtendedErrorCode"]);
        Assert.False(string.IsNullOrWhiteSpace(diagnostic.Properties["TraceId"]?.ToString()));
        AssertSafe(logs, await response.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task DuplicateRegistration_WithRealEfRepository_DoesNotExposePersonalDataInLogs()
    {
        using var logs = new CapturingLogs();
        await using var factory = new DriverApiFactory();
        await using var monitored = factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        await using (var scope = monitored.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<DriverDbContext>().Database.MigrateAsync(Token);
        using var client = monitored.CreateClient();
        using var first = await client.PostAsJsonAsync("/api/v1/drivers", DriverApiTests.ValidPayload(), Token);
        using var duplicate = await client.PostAsJsonAsync("/api/v1/drivers", DriverApiTests.ValidPayload(), Token);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        AssertSafe(logs, await duplicate.Content.ReadAsStringAsync(Token));
        Assert.DoesNotContain(logs.Events, item => item.Properties.ContainsKey("SqliteErrorCode"));
    }

    [Fact]
    public async Task PrimaryKeyCollision_IsNotReportedAsRegistrationConflict()
    {
        await using var factory = new DriverApiFactory();
        await factory.MigrateAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DriverDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
        var today = new DateOnly(2026, 10, 10);
        var first = DriverRegistration.Domain.Driver.Create("QA Example", "52998224725", "31999999999",
            "first@example.com", "12345678901", "D", today.AddDays(1), today);
        Assert.True(await repository.AddAsync(first, Token));
        db.ChangeTracker.Clear();
        var second = DriverRegistration.Domain.Driver.Create("QA Example", "11144477735", "31999999999",
            "second@example.com", "98765432100", "D", today.AddDays(1), today);
        db.Entry(second).Property(item => item.Id).CurrentValue = first.Id;

        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => repository.AddAsync(second, Token));
        Assert.Equal(1555, Assert.IsType<Microsoft.Data.Sqlite.SqliteException>(failure.InnerException).SqliteExtendedErrorCode);
    }

    [Fact]
    public async Task Migration_HasOnlyTheRegistrationUniqueIndexesAssumedByConflictClassification()
    {
        await using var factory = new DriverApiFactory();
        await factory.MigrateAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DriverDbContext>();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT il.name, ii.name FROM pragma_index_list('Drivers') il " +
            "JOIN pragma_index_info(il.name) ii WHERE il.\"unique\" = 1 AND il.origin <> 'pk' ORDER BY ii.name;";
        await using var reader = await command.ExecuteReaderAsync(Token);
        var columns = new List<string>();
        while (await reader.ReadAsync(Token)) columns.Add(reader.GetString(1));
        Assert.Equal(new[] { "CnhNumber", "Cpf", "Email" }, columns);
    }

    private static void AssertSafe(CapturingLogs logs, string response)
    {
        var sensitiveValues = new[] { "52998224725", "529.982.247-25", "12345678901", "ANA@EXAMPLE.COM",
            "ana@example.com", "31999999999", "+55 (31) 99999-9999", "Ana", "private-database-sentinel" };
        Assert.All(logs.Events, entry =>
        {
            Assert.Null(entry.Exception);
            foreach (var value in sensitiveValues) Assert.DoesNotContain(value, entry.Message);
        });
        foreach (var value in sensitiveValues) Assert.DoesNotContain(value, response);
    }

    private sealed record CapturedLog(string Message, Exception? Exception, Dictionary<string, object?> Properties);

    private sealed class CapturingLogs : ILoggerProvider
    {
        public ConcurrentQueue<CapturedLog> Events { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(Events);
        public void Dispose() { }

        private sealed class CaptureLogger(ConcurrentQueue<CapturedLog> events) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var properties = state is IEnumerable<KeyValuePair<string, object?>> structured
                    ? structured.ToDictionary(pair => pair.Key, pair => pair.Value) : new Dictionary<string, object?>();
                events.Enqueue(new(formatter(state, exception), exception, properties));
            }
        }
    }
}

using DriverRegistration.Application;
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

internal sealed class DriverApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public DriverApiFactory() => _connection.Open();

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
            services.AddDbContext<DriverDbContext>(options => options.UseSqlite(_connection));
            services.RemoveAll<IRegistrationDateProvider>();
            services.AddSingleton<IRegistrationDateProvider, FixedDateProvider>();
        });
    }

    public async Task MigrateAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DriverDbContext>().Database
            .MigrateAsync(TestContext.Current.CancellationToken);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private sealed class FixedDateProvider : IRegistrationDateProvider
    {
        public DateOnly Today => new(2026, 10, 10);
    }
}

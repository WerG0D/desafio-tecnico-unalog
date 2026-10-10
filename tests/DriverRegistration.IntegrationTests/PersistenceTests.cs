using DriverRegistration.Application;
using DriverRegistration.Domain;
using DriverRegistration.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DriverRegistration.IntegrationTests;

public sealed class PersistenceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static Driver Create(string cpf = "52998224725", string email = "ana@example.com", string cnh = "12345678901") =>
        Driver.Create("Ana Silva", cpf, "31999999999", email, cnh, "D", new DateOnly(2026, 10, 11), new DateOnly(2026, 10, 10));

    [Theory]
    [InlineData("cpf")]
    [InlineData("email")]
    [InlineData("cnh")]
    public async Task UniqueIndexes_RejectIndependentDuplicatesWithoutRepository(string field)
    {
        await using var factory = new DriverApiFactory();
        await factory.MigrateAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DriverDbContext>();
        context.Add(Create());
        await context.SaveChangesAsync(Token);
        context.Add(Create(field == "cpf" ? "52998224725" : "11144477735",
            field == "email" ? "ana@example.com" : "outra@example.com",
            field == "cnh" ? "12345678901" : "98765432100"));

        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Token));

        Assert.Equal(2067, Assert.IsType<SqliteException>(failure.InnerException).SqliteExtendedErrorCode);
    }

    [Fact]
    public async Task Repository_CheckConstraintFailure_PropagatesInsteadOfConflict()
    {
        await using var factory = new DriverApiFactory();
        await factory.MigrateAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DriverDbContext>();
        var driver = Create();
        context.Entry(driver).Property(item => item.Name).CurrentValue = "";
        var repository = scope.ServiceProvider.GetRequiredService<IDriverRepository>();

        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => repository.AddAsync(driver, Token));

        Assert.Equal(275, Assert.IsType<SqliteException>(failure.InnerException).SqliteExtendedErrorCode);
    }

    [Fact]
    public async Task Repository_Conflict_DoesNotPoisonNextInsertInSameScope()
    {
        await using var factory = new DriverApiFactory();
        await factory.MigrateAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
        Assert.True(await repository.AddAsync(Create(), Token));
        Assert.False(await repository.AddAsync(Create(), Token));
        Assert.True(await repository.AddAsync(Create("11144477735", "outra@example.com", "98765432100"), Token));
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<DriverDbContext>().Drivers.CountAsync(Token));
    }

    [Fact]
    public async Task ConcurrentInsert_SameRegistration_PersistsExactlyOne()
    {
        var path = Path.Combine(Path.GetTempPath(), $"driver-registration-tests-{Guid.NewGuid():N}.db");
        var connection = $"Data Source={path};Pooling=False;Default Timeout=10";
        var services = new ServiceCollection();
        services.AddInfrastructure(connection);
        await using var provider = services.BuildServiceProvider();
        try
        {
            await using (var scope = provider.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<DriverDbContext>().Database.MigrateAsync(Token);
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = InsertAsync();
            var second = InsertAsync();

            gate.SetResult();
            var results = await Task.WhenAll(first, second);

            Assert.Single(results, inserted => inserted);
            Assert.Single(results, inserted => !inserted);
            await using var verification = provider.CreateAsyncScope();
            Assert.Equal(1, await verification.ServiceProvider.GetRequiredService<DriverDbContext>().Drivers.CountAsync(Token));

            async Task<bool> InsertAsync()
            {
                await using var scope = provider.CreateAsyncScope();
                await gate.Task;
                return await scope.ServiceProvider.GetRequiredService<IDriverRepository>().AddAsync(Create(), Token);
            }
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + "-shm");
            File.Delete(path + "-wal");
        }
    }
}

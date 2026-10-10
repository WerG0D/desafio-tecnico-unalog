using DriverRegistration.Application;
using DriverRegistration.Domain;

namespace DriverRegistration.UnitTests;

public sealed class RegisterDriverTests
{
    private static RegisterDriverCommand ValidCommand => new("Ana Silva", "529.982.247-25", "31999999999",
        "ANA@example.com", "12345678901", "D", new DateOnly(2026, 10, 11));

    [Fact]
    public async Task Execute_ValidCommand_PersistsNormalizedDriverAndReturnsId()
    {
        var repository = new RecordingRepository();
        var service = new RegisterDriver(repository, new FixedDateProvider());
        using var cancellation = new CancellationTokenSource();

        var result = await service.ExecuteAsync(ValidCommand, cancellation.Token);

        Assert.NotNull(repository.Driver);
        Assert.Equal(repository.Driver.Id, result.DriverId);
        Assert.Equal("52998224725", repository.Driver.Cpf);
        Assert.Equal("ana@example.com", repository.Driver.Email);
        Assert.Equal(cancellation.Token, repository.CancellationToken);
        Assert.Null(result.Errors);
        Assert.False(result.IsConflict);
    }

    [Fact]
    public async Task Execute_InvalidCommand_DoesNotPersist()
    {
        var repository = new RecordingRepository();
        var result = await new RegisterDriver(repository, new FixedDateProvider())
            .ExecuteAsync(ValidCommand with { Cpf = "invalid" }, TestContext.Current.CancellationToken);

        Assert.NotNull(result.Errors);
        Assert.Contains("cpf", result.Errors.Keys);
        Assert.Null(result.DriverId);
        Assert.False(result.IsConflict);
        Assert.Null(repository.Driver);
    }

    [Fact]
    public async Task Execute_Duplicate_ReturnsConflictWithoutId()
    {
        var repository = new RecordingRepository { Inserted = false };
        var result = await new RegisterDriver(repository, new FixedDateProvider())
            .ExecuteAsync(ValidCommand, TestContext.Current.CancellationToken);

        Assert.True(result.IsConflict);
        Assert.Null(result.DriverId);
        Assert.Null(result.Errors);
    }

    [Fact]
    public async Task Execute_Cancelled_DoesNotPersist()
    {
        var repository = new RecordingRepository();
        var service = new RegisterDriver(repository, new FixedDateProvider());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExecuteAsync(ValidCommand, cancellation.Token));
        Assert.Null(repository.Driver);
    }

    [Fact]
    public async Task Execute_UnexpectedPersistenceFailure_Propagates()
    {
        var failure = new InvalidOperationException("Falha simulada.");
        var service = new RegisterDriver(new RecordingRepository { Failure = failure }, new FixedDateProvider());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExecuteAsync(ValidCommand, TestContext.Current.CancellationToken));
        Assert.Same(failure, exception);
    }

    private sealed class FixedDateProvider : IRegistrationDateProvider
    {
        public DateOnly Today => new(2026, 10, 10);
    }

    private sealed class RecordingRepository : IDriverRepository
    {
        public Driver? Driver { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public bool Inserted { get; init; } = true;
        public Exception? Failure { get; init; }

        public Task<bool> AddAsync(Driver driver, CancellationToken cancellationToken)
        {
            if (Failure is not null) return Task.FromException<bool>(Failure);
            Driver = driver;
            CancellationToken = cancellationToken;
            return Task.FromResult(Inserted);
        }
    }
}

using DriverRegistration.Api;

namespace DriverRegistration.IntegrationTests;

public sealed class RegistrationDateProviderTests
{
    [Theory]
    [InlineData(2, 59, 9)]
    [InlineData(3, 0, 10)]
    public void Today_UsesSaoPauloCivilDate(int utcHour, int utcMinute, int day)
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 10, utcHour, utcMinute, 0, TimeSpan.Zero));
        Assert.Equal(new DateOnly(2026, 10, day), new RegistrationDateProvider(clock).Today);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

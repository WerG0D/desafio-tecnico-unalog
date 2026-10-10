using DriverRegistration.Application;

namespace DriverRegistration.Api;

public sealed class RegistrationDateProvider(TimeProvider timeProvider) : IRegistrationDateProvider
{
    private static readonly TimeZoneInfo RegistrationTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public DateOnly Today => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), RegistrationTimeZone).DateTime);
}

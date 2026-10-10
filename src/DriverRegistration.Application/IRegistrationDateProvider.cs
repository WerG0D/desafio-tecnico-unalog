namespace DriverRegistration.Application;

public interface IRegistrationDateProvider
{
    DateOnly Today { get; }
}

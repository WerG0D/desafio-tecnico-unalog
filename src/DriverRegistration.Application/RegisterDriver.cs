using DriverRegistration.Domain;

namespace DriverRegistration.Application;

public sealed class RegisterDriver(IDriverRepository repository, IRegistrationDateProvider dateProvider)
{
    public async Task<RegisterDriverResult> ExecuteAsync(
        RegisterDriverCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Driver driver;
        try
        {
            driver = Driver.Create(command.Name, command.Cpf, command.Phone, command.Email,
                command.CnhNumber, command.CnhCategory, command.CnhExpirationDate, dateProvider.Today);
        }
        catch (DomainValidationException exception)
        {
            return new(null, exception.Errors, false);
        }

        var inserted = await repository.AddAsync(driver, cancellationToken).ConfigureAwait(false);
        return inserted ? new(driver.Id, null, false) : new(null, null, true);
    }
}

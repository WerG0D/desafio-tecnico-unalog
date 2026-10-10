using DriverRegistration.Domain;

namespace DriverRegistration.Application;

public interface IDriverRepository
{
    Task<bool> AddAsync(Driver driver, CancellationToken cancellationToken);
}

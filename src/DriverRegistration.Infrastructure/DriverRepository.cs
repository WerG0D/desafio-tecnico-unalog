using DriverRegistration.Application;
using DriverRegistration.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DriverRegistration.Infrastructure;

internal sealed class DriverRepository(DriverDbContext context) : IDriverRepository
{
    public async Task<bool> AddAsync(Driver driver, CancellationToken cancellationToken)
    {
        context.Drivers.Add(driver);
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException exception) when (IsRegistrationConflict(exception))
        {
            context.Entry(driver).State = EntityState.Detached;
            return false;
        }
    }

    private static bool IsRegistrationConflict(DbUpdateException exception)
    {
        if (exception.InnerException is not SqliteException { SqliteExtendedErrorCode: 2067 } sqlite)
            return false;

        return sqlite.Message is
            "SQLite Error 19: 'UNIQUE constraint failed: Drivers.Cpf'." or
            "SQLite Error 19: 'UNIQUE constraint failed: Drivers.Email'." or
            "SQLite Error 19: 'UNIQUE constraint failed: Drivers.CnhNumber'.";
    }
}

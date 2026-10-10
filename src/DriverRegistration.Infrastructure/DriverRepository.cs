using System.Diagnostics;
using DriverRegistration.Application;
using DriverRegistration.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DriverRegistration.Infrastructure;

internal sealed class DriverRepository(DriverDbContext context, ILogger<DriverRepository> logger) : IDriverRepository
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
        catch (Exception exception) when (exception is DbUpdateException or SqliteException)
        {
            var sqlite = exception as SqliteException ?? exception.InnerException as SqliteException;
            logger.LogError("Falha de persistência. Categoria: {FailureCategory}. Código: {SqliteErrorCode}. " +
                "Código estendido: {SqliteExtendedErrorCode}. Rastreio: {TraceId}.",
                SqliteFailureCategory(sqlite?.SqliteErrorCode), sqlite?.SqliteErrorCode,
                sqlite?.SqliteExtendedErrorCode, Activity.Current?.TraceId.ToString());
            throw;
        }
    }

    // Os únicos índices UNIQUE de Drivers são CPF, e-mail e CNH; a chave primária usa 1555.
    private static bool IsRegistrationConflict(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 }
        && exception.Entries.Count == 1 && exception.Entries[0].Entity is Driver;

    private static string SqliteFailureCategory(int? errorCode) => errorCode switch
    {
        5 or 6 => "database_busy",
        19 => "integrity_constraint",
        14 => "connection_failure",
        8 => "read_only",
        11 or 26 => "invalid_database",
        _ => "database_error"
    };
}

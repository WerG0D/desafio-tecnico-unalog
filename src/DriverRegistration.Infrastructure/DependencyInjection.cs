using DriverRegistration.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace DriverRegistration.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddLogging();
        services.AddDbContext<DriverDbContext>(options => options
            .UseSqlite(connectionString)
            .EnableSensitiveDataLogging(false)
            .ConfigureWarnings(warnings => warnings.Ignore(
                CoreEventId.SaveChangesFailed, RelationalEventId.CommandError)));
        services.AddScoped<IDriverRepository, DriverRepository>();
        return services;
    }
}

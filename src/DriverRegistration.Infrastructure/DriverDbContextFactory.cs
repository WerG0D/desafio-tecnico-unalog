using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DriverRegistration.Infrastructure;

public sealed class DriverDbContextFactory : IDesignTimeDbContextFactory<DriverDbContext>
{
    public DriverDbContext CreateDbContext(string[] args)
    {
        var connectionString = args.FirstOrDefault()
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__Drivers")
            ?? "Data Source=:memory:";

        return new DriverDbContext(new DbContextOptionsBuilder<DriverDbContext>()
            .UseSqlite(connectionString)
            .Options);
    }
}

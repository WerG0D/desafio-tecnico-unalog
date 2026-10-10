using DriverRegistration.Domain;
using Microsoft.EntityFrameworkCore;

namespace DriverRegistration.Infrastructure;

public sealed class DriverDbContext(DbContextOptions<DriverDbContext> options) : DbContext(options)
{
    public DbSet<Driver> Drivers => Set<Driver>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var driver = modelBuilder.Entity<Driver>();
        driver.ToTable("Drivers", table =>
        {
            table.HasCheckConstraint("CK_Drivers_Name", "length(Name) BETWEEN 2 AND 200");
            table.HasCheckConstraint("CK_Drivers_Cpf", "length(Cpf) = 11 AND Cpf NOT GLOB '*[^0-9]*'");
            table.HasCheckConstraint("CK_Drivers_Phone", "length(Phone) BETWEEN 10 AND 11 AND Phone NOT GLOB '*[^0-9]*'");
            table.HasCheckConstraint("CK_Drivers_Email", "length(Email) BETWEEN 3 AND 254");
            table.HasCheckConstraint("CK_Drivers_CnhNumber", "length(CnhNumber) = 11 AND CnhNumber NOT GLOB '*[^0-9]*'");
            table.HasCheckConstraint("CK_Drivers_CnhCategory", "CnhCategory IN ('A', 'B', 'C', 'D', 'E', 'AB', 'AC', 'AD', 'AE')");
        });
        driver.HasKey(item => item.Id);
        driver.Property(item => item.Id).ValueGeneratedNever();
        driver.Property(item => item.Name).HasMaxLength(200).IsRequired();
        driver.Property(item => item.Cpf).HasMaxLength(11).IsRequired();
        driver.Property(item => item.Phone).HasMaxLength(11).IsRequired();
        driver.Property(item => item.Email).HasMaxLength(254).IsRequired();
        driver.Property(item => item.CnhNumber).HasMaxLength(11).IsRequired();
        driver.Property(item => item.CnhCategory).HasMaxLength(2).IsRequired();
        driver.Property(item => item.CnhExpirationDate).IsRequired();
        driver.HasIndex(item => item.Cpf).IsUnique();
        driver.HasIndex(item => item.Email).IsUnique();
        driver.HasIndex(item => item.CnhNumber).IsUnique();
    }
}

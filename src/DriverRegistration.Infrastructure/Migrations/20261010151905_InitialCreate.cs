using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverRegistration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Drivers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Cpf = table.Column<string>(type: "TEXT", maxLength: 11, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 11, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    CnhNumber = table.Column<string>(type: "TEXT", maxLength: 11, nullable: false),
                    CnhCategory = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    CnhExpirationDate = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drivers", x => x.Id);
                    table.CheckConstraint("CK_Drivers_CnhCategory", "CnhCategory IN ('A', 'B', 'C', 'D', 'E', 'AB', 'AC', 'AD', 'AE')");
                    table.CheckConstraint("CK_Drivers_CnhNumber", "length(CnhNumber) = 11 AND CnhNumber NOT GLOB '*[^0-9]*'");
                    table.CheckConstraint("CK_Drivers_Cpf", "length(Cpf) = 11 AND Cpf NOT GLOB '*[^0-9]*'");
                    table.CheckConstraint("CK_Drivers_Email", "length(Email) BETWEEN 3 AND 254");
                    table.CheckConstraint("CK_Drivers_Name", "length(Name) BETWEEN 2 AND 200");
                    table.CheckConstraint("CK_Drivers_Phone", "length(Phone) BETWEEN 10 AND 11 AND Phone NOT GLOB '*[^0-9]*'");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_CnhNumber",
                table: "Drivers",
                column: "CnhNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_Cpf",
                table: "Drivers",
                column: "Cpf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_Email",
                table: "Drivers",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Drivers");
        }
    }
}

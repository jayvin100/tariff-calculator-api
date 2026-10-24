using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TariffCalculator.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tariffs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EnergyRate = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    CapacityRate = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    NetworkRate = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    RetailCharge = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    RetailWaiverLimitKwh = table.Column<int>(type: "int", nullable: false),
                    MaxSupportedKwh = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tariffs", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Tariffs",
                columns: new[] { "Id", "CapacityRate", "Code", "EnergyRate", "MaxSupportedKwh", "Name", "NetworkRate", "RetailCharge", "RetailWaiverLimitKwh" },
                values: new object[] { 1, 0.0455m, "DOMESTIC", 0.2703m, 1500, "Domestic", 0.1285m, 10.00m, 600 });

            migrationBuilder.CreateIndex(
                name: "IX_Tariffs_Code",
                table: "Tariffs",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tariffs");
        }
    }
}

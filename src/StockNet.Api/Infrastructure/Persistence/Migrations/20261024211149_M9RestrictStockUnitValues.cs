using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockNet.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M9RestrictStockUnitValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Stocks_Unit_Allowed",
                table: "Stocks",
                sql: "\"Unit\" IN ('Tons', 'Kilograms', 'Pieces', 'Units', 'CubicMeters', 'Liters')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Stocks_Unit_Allowed",
                table: "Stocks");
        }
    }
}

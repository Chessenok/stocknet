using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockNet.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M8IndustrialEquipmentCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Description", "IsActive", "Name", "ParentId", "RequiredAttributeKeys", "Slug", "SortOrder" },
                values: new object[] { 10, "Equipment and discrete industrial goods", true, "Industrial Equipment", 8, "[]", "industrial-equipment", 10 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 10);
        }
    }
}

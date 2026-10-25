using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockNet.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M10AttributeSchemasAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RequiredAttributeKeys",
                table: "Categories",
                newName: "AttributeRules");

            migrationBuilder.Sql("""
                UPDATE "Categories" AS category
                SET "AttributeRules" = COALESCE((
                    SELECT jsonb_agg(jsonb_build_object(
                        'Key', attribute_key,
                        'Type', 'String',
                        'IsRequired', true,
                        'Min', NULL,
                        'Max', NULL,
                        'AllowedValues', '[]'::jsonb))
                    FROM jsonb_array_elements_text(category."AttributeRules") AS keys(attribute_key)
                ), '[]'::jsonb);
                """);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1,
                column: "AttributeRules",
                value: "[{\"Key\":\"steel_grade\",\"Type\":\"String\",\"IsRequired\":true,\"Min\":null,\"Max\":null,\"AllowedValues\":[\"a36\",\"s235\",\"s355\"]}]");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 9,
                column: "AttributeRules",
                value: "[{\"Key\":\"moisture\",\"Type\":\"Number\",\"IsRequired\":true,\"Min\":0,\"Max\":100,\"AllowedValues\":[]},{\"Key\":\"germination_rate\",\"Type\":\"Number\",\"IsRequired\":true,\"Min\":0,\"Max\":100,\"AllowedValues\":[]}]");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 10,
                column: "AttributeRules",
                value: "[{\"Key\":\"equipment_type\",\"Type\":\"String\",\"IsRequired\":false,\"Min\":null,\"Max\":null,\"AllowedValues\":[\"compressor\",\"motor\",\"pump\"]}]");

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_Attributes",
                table: "Stocks",
                column: "Attributes")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_WarehouseId_CreatedAt",
                table: "Stocks",
                columns: new[] { "WarehouseId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Stocks_Attributes",
                table: "Stocks");

            migrationBuilder.DropIndex(
                name: "IX_Stocks_WarehouseId_CreatedAt",
                table: "Stocks");

            migrationBuilder.Sql("""
                UPDATE "Categories" AS category
                SET "AttributeRules" = COALESCE((
                    SELECT jsonb_agg(rule->'Key')
                    FROM jsonb_array_elements(category."AttributeRules") AS rules(rule)
                ), '[]'::jsonb);
                """);

            migrationBuilder.RenameColumn(
                name: "AttributeRules",
                table: "Categories",
                newName: "RequiredAttributeKeys");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1,
                column: "RequiredAttributeKeys",
                value: "[\"steel_grade\"]");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 9,
                column: "RequiredAttributeKeys",
                value: "[\"moisture\",\"germination_rate\"]");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 10,
                column: "RequiredAttributeKeys",
                value: "[]");
        }
    }
}

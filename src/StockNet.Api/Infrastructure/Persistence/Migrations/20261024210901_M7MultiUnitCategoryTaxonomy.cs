using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockNet.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M7MultiUnitCategoryTaxonomy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Stocks_MinOrderQuantityTons_Valid",
                table: "Stocks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Stocks_PricePerTon_NonNegative",
                table: "Stocks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Stocks_StepQuantityTons_Positive",
                table: "Stocks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Stocks_TotalQuantityTons_Positive",
                table: "Stocks");

            migrationBuilder.RenameColumn(
                name: "TotalQuantityTons",
                table: "Stocks",
                newName: "TotalQuantity");

            migrationBuilder.RenameColumn(
                name: "StepQuantityTons",
                table: "Stocks",
                newName: "StepQuantity");

            migrationBuilder.RenameColumn(
                name: "PricePerTon",
                table: "Stocks",
                newName: "PricePerUnit");

            migrationBuilder.RenameColumn(
                name: "MinOrderQuantityTons",
                table: "Stocks",
                newName: "MinOrderQuantity");

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "Stocks",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Tons");

            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "Categories",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredAttributeKeys",
                table: "Categories",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ParentId", "RequiredAttributeKeys" },
                values: new object[] { null, "[\"steel_grade\"]" });

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "ParentId", "RequiredAttributeKeys" },
                values: new object[] { null, "[]" });

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "ParentId", "RequiredAttributeKeys" },
                values: new object[] { null, "[]" });

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "ParentId", "RequiredAttributeKeys" },
                values: new object[] { null, "[]" });

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "ParentId", "RequiredAttributeKeys" },
                values: new object[] { null, "[]" });

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "ParentId", "RequiredAttributeKeys" },
                values: new object[] { null, "[]" });

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "ParentId", "RequiredAttributeKeys" },
                values: new object[] { null, "[]" });

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "ParentId", "RequiredAttributeKeys" },
                values: new object[] { null, "[]" });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Description", "IsActive", "Name", "ParentId", "RequiredAttributeKeys", "Slug", "SortOrder" },
                values: new object[] { 9, "Seeds and planting material", true, "Seeds", 7, "[\"moisture\",\"germination_rate\"]", "seeds", 10 });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stocks_DiscreteUnits_IntegralQuantities",
                table: "Stocks",
                sql: "\"Unit\" NOT IN ('Pieces', 'Units') OR (\"TotalQuantity\" = trunc(\"TotalQuantity\") AND \"MinOrderQuantity\" = trunc(\"MinOrderQuantity\") AND \"StepQuantity\" = trunc(\"StepQuantity\") AND \"StepQuantity\" >= 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stocks_MinOrderQuantity_Valid",
                table: "Stocks",
                sql: "\"MinOrderQuantity\" > 0 AND \"MinOrderQuantity\" <= \"TotalQuantity\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stocks_PricePerUnit_Positive",
                table: "Stocks",
                sql: "\"PricePerUnit\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stocks_StepQuantity_Positive",
                table: "Stocks",
                sql: "\"StepQuantity\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stocks_TotalQuantity_Positive",
                table: "Stocks",
                sql: "\"TotalQuantity\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentId",
                table: "Categories",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Categories_ParentId",
                table: "Categories",
                column: "ParentId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Categories_ParentId",
                table: "Categories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Stocks_DiscreteUnits_IntegralQuantities",
                table: "Stocks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Stocks_MinOrderQuantity_Valid",
                table: "Stocks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Stocks_PricePerUnit_Positive",
                table: "Stocks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Stocks_StepQuantity_Positive",
                table: "Stocks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Stocks_TotalQuantity_Positive",
                table: "Stocks");

            migrationBuilder.DropIndex(
                name: "IX_Categories_ParentId",
                table: "Categories");

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "Stocks");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "RequiredAttributeKeys",
                table: "Categories");

            migrationBuilder.RenameColumn(
                name: "TotalQuantity",
                table: "Stocks",
                newName: "TotalQuantityTons");

            migrationBuilder.RenameColumn(
                name: "StepQuantity",
                table: "Stocks",
                newName: "StepQuantityTons");

            migrationBuilder.RenameColumn(
                name: "PricePerUnit",
                table: "Stocks",
                newName: "PricePerTon");

            migrationBuilder.RenameColumn(
                name: "MinOrderQuantity",
                table: "Stocks",
                newName: "MinOrderQuantityTons");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stocks_MinOrderQuantityTons_Valid",
                table: "Stocks",
                sql: "\"MinOrderQuantityTons\" > 0 AND \"MinOrderQuantityTons\" <= \"TotalQuantityTons\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stocks_PricePerTon_NonNegative",
                table: "Stocks",
                sql: "\"PricePerTon\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stocks_StepQuantityTons_Positive",
                table: "Stocks",
                sql: "\"StepQuantityTons\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stocks_TotalQuantityTons_Positive",
                table: "Stocks",
                sql: "\"TotalQuantityTons\" > 0");
        }
    }
}

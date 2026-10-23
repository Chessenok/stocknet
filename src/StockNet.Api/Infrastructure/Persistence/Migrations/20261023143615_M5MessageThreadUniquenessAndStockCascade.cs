using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockNet.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M5MessageThreadUniquenessAndStockCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MessageThreads_Stocks_StockId",
                table: "MessageThreads");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreads_StockId_BuyerCompanyId",
                table: "MessageThreads",
                columns: new[] { "StockId", "BuyerCompanyId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MessageThreads_Stocks_StockId",
                table: "MessageThreads",
                column: "StockId",
                principalTable: "Stocks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MessageThreads_Stocks_StockId",
                table: "MessageThreads");

            migrationBuilder.DropIndex(
                name: "IX_MessageThreads_StockId_BuyerCompanyId",
                table: "MessageThreads");

            migrationBuilder.AddForeignKey(
                name: "FK_MessageThreads_Stocks_StockId",
                table: "MessageThreads",
                column: "StockId",
                principalTable: "Stocks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockNet.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M6ProcessedStripeEventsAndSubscriptionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StripeSubscriptionId",
                table: "Companies",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProcessedStripeEvents",
                columns: table => new
                {
                    EventId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    EventType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedStripeEvents", x => x.EventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_StripeSubscriptionId",
                table: "Companies",
                column: "StripeSubscriptionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedStripeEvents_ProcessedAtUtc",
                table: "ProcessedStripeEvents",
                column: "ProcessedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessedStripeEvents");

            migrationBuilder.DropIndex(
                name: "IX_Companies_StripeSubscriptionId",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "StripeSubscriptionId",
                table: "Companies");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Psp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMerchantNotifyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MerchantNotified",
                table: "psp_transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MerchantNotifiedAtUtc",
                table: "psp_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MerchantNotifyAttempts",
                table: "psp_transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MerchantNotifyLastError",
                table: "psp_transactions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MerchantNotified",
                table: "psp_transactions");

            migrationBuilder.DropColumn(
                name: "MerchantNotifiedAtUtc",
                table: "psp_transactions");

            migrationBuilder.DropColumn(
                name: "MerchantNotifyAttempts",
                table: "psp_transactions");

            migrationBuilder.DropColumn(
                name: "MerchantNotifyLastError",
                table: "psp_transactions");
        }
    }
}

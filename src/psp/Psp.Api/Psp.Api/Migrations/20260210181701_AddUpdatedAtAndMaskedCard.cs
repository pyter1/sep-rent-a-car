using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Psp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdatedAtAndMaskedCard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CardBrand",
                table: "psp_transactions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PanFirst6",
                table: "psp_transactions",
                type: "character varying(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PanLast4",
                table: "psp_transactions",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "psp_transactions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.CreateIndex(
                name: "IX_psp_transactions_BankPaymentId",
                table: "psp_transactions",
                column: "BankPaymentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_psp_transactions_BankPaymentId",
                table: "psp_transactions");

            migrationBuilder.DropColumn(
                name: "CardBrand",
                table: "psp_transactions");

            migrationBuilder.DropColumn(
                name: "PanFirst6",
                table: "psp_transactions");

            migrationBuilder.DropColumn(
                name: "PanLast4",
                table: "psp_transactions");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "psp_transactions");
        }
    }
}

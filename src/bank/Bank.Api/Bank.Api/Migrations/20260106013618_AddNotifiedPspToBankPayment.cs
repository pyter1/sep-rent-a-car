using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bank.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifiedPspToBankPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotifiedPsp",
                table: "bank_payments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_bank_payments_ExpiresAtUtc",
                table: "bank_payments",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_bank_payments_Status",
                table: "bank_payments",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bank_payments_ExpiresAtUtc",
                table: "bank_payments");

            migrationBuilder.DropIndex(
                name: "IX_bank_payments_Status",
                table: "bank_payments");

            migrationBuilder.DropColumn(
                name: "NotifiedPsp",
                table: "bank_payments");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bank.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMaskedPanToBankPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PanFirst6",
                table: "bank_payments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PanFirst6",
                table: "bank_payments");
        }
    }
}

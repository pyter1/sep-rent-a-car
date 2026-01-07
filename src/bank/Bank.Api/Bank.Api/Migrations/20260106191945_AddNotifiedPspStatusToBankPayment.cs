using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bank.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifiedPspStatusToBankPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NotifiedPspStatus",
                table: "bank_payments",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotifiedPspStatus",
                table: "bank_payments");
        }
    }
}

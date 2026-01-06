using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Psp.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialPsp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "psp_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantOrderId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SuccessUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FailUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ErrorUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BankPaymentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_psp_transactions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_psp_transactions_MerchantOrderId",
                table: "psp_transactions",
                column: "MerchantOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "psp_transactions");
        }
    }
}

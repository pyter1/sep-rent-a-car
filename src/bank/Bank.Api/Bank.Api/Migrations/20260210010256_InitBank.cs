using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bank.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CardBrand",
                table: "bank_payments",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PanLast4",
                table: "bank_payments",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PspMerchantId",
                table: "bank_payments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "PspTimestampUtc",
                table: "bank_payments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Stan",
                table: "bank_payments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "bank_audit_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Service = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ActorId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    MerchantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    MerchantOrderId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PspTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    BankPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Stan = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Result = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    DetailsJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_audit_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bank_payments_PanLast4",
                table: "bank_payments",
                column: "PanLast4");

            migrationBuilder.CreateIndex(
                name: "IX_bank_payments_PspMerchantId_Stan_PspTimestampUtc",
                table: "bank_payments",
                columns: new[] { "PspMerchantId", "Stan", "PspTimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_bank_audit_events_BankPaymentId",
                table: "bank_audit_events",
                column: "BankPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_bank_audit_events_CorrelationId",
                table: "bank_audit_events",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_bank_audit_events_PspTransactionId",
                table: "bank_audit_events",
                column: "PspTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_bank_audit_events_Stan",
                table: "bank_audit_events",
                column: "Stan");

            migrationBuilder.CreateIndex(
                name: "IX_bank_audit_events_TimestampUtc",
                table: "bank_audit_events",
                column: "TimestampUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bank_audit_events");

            migrationBuilder.DropIndex(
                name: "IX_bank_payments_PanLast4",
                table: "bank_payments");

            migrationBuilder.DropIndex(
                name: "IX_bank_payments_PspMerchantId_Stan_PspTimestampUtc",
                table: "bank_payments");

            migrationBuilder.DropColumn(
                name: "CardBrand",
                table: "bank_payments");

            migrationBuilder.DropColumn(
                name: "PanLast4",
                table: "bank_payments");

            migrationBuilder.DropColumn(
                name: "PspMerchantId",
                table: "bank_payments");

            migrationBuilder.DropColumn(
                name: "PspTimestampUtc",
                table: "bank_payments");

            migrationBuilder.DropColumn(
                name: "Stan",
                table: "bank_payments");
        }
    }
}

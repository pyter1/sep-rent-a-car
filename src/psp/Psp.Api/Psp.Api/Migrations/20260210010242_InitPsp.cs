using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Psp.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitPsp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MerchantId",
                table: "psp_transactions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "MerchantTimestampUtc",
                table: "psp_transactions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "PspTimestampUtc",
                table: "psp_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Stan",
                table: "psp_transactions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "psp_audit_events",
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
                    table.PrimaryKey("PK_psp_audit_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_psp_transactions_MerchantId_MerchantOrderId",
                table: "psp_transactions",
                columns: new[] { "MerchantId", "MerchantOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_psp_transactions_Stan",
                table: "psp_transactions",
                column: "Stan");

            migrationBuilder.CreateIndex(
                name: "IX_psp_audit_events_BankPaymentId",
                table: "psp_audit_events",
                column: "BankPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_psp_audit_events_CorrelationId",
                table: "psp_audit_events",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_psp_audit_events_MerchantOrderId",
                table: "psp_audit_events",
                column: "MerchantOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_psp_audit_events_PspTransactionId",
                table: "psp_audit_events",
                column: "PspTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_psp_audit_events_Stan",
                table: "psp_audit_events",
                column: "Stan");

            migrationBuilder.CreateIndex(
                name: "IX_psp_audit_events_TimestampUtc",
                table: "psp_audit_events",
                column: "TimestampUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "psp_audit_events");

            migrationBuilder.DropIndex(
                name: "IX_psp_transactions_MerchantId_MerchantOrderId",
                table: "psp_transactions");

            migrationBuilder.DropIndex(
                name: "IX_psp_transactions_Stan",
                table: "psp_transactions");

            migrationBuilder.DropColumn(
                name: "MerchantId",
                table: "psp_transactions");

            migrationBuilder.DropColumn(
                name: "MerchantTimestampUtc",
                table: "psp_transactions");

            migrationBuilder.DropColumn(
                name: "PspTimestampUtc",
                table: "psp_transactions");

            migrationBuilder.DropColumn(
                name: "Stan",
                table: "psp_transactions");
        }
    }
}

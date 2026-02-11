using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WebShop.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitWebShop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "webshop_audit_events",
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
                    table.PrimaryKey("PK_webshop_audit_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "webshop_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FailedLoginCount = table.Column<int>(type: "integer", nullable: false),
                    LockoutUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webshop_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "webshop_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantOrderId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PspTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webshop_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_webshop_orders_webshop_users_UserId",
                        column: x => x.UserId,
                        principalTable: "webshop_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_webshop_audit_events_CorrelationId",
                table: "webshop_audit_events",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_webshop_audit_events_MerchantOrderId",
                table: "webshop_audit_events",
                column: "MerchantOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_webshop_audit_events_PspTransactionId",
                table: "webshop_audit_events",
                column: "PspTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_webshop_audit_events_TimestampUtc",
                table: "webshop_audit_events",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_webshop_orders_MerchantOrderId",
                table: "webshop_orders",
                column: "MerchantOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webshop_orders_Status",
                table: "webshop_orders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_webshop_orders_UserId",
                table: "webshop_orders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_webshop_users_Email",
                table: "webshop_users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "webshop_audit_events");

            migrationBuilder.DropTable(
                name: "webshop_orders");

            migrationBuilder.DropTable(
                name: "webshop_users");
        }
    }
}

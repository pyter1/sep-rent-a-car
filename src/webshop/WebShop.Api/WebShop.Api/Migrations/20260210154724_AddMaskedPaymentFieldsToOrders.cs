using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebShop.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMaskedPaymentFieldsToOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BankPaymentId",
                table: "webshop_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardBrand",
                table: "webshop_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PanLast4",
                table: "webshop_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Stan",
                table: "webshop_orders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankPaymentId",
                table: "webshop_orders");

            migrationBuilder.DropColumn(
                name: "CardBrand",
                table: "webshop_orders");

            migrationBuilder.DropColumn(
                name: "PanLast4",
                table: "webshop_orders");

            migrationBuilder.DropColumn(
                name: "Stan",
                table: "webshop_orders");
        }
    }
}

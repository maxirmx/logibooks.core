using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_7_0_Ozon_Order : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "weight_kg",
                table: "wbr_orders",
                type: "numeric(10,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,3)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_price",
                table: "wbr_orders",
                type: "numeric(10,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,3)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "wbr_orders",
                type: "numeric(10,0)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,3)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "article",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "barcode",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "birth_date",
                table: "ozon_orders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "box_number",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cmn",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cmn_id",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "comment",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "country",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description_en",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "first_name",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "imei",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "imei_2",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "inn",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_name",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "manufacturer",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "passport_issue_date",
                table: "ozon_orders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "passport_issued_by",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "passport_number",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "passport_series",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "patronymic",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "places_count",
                table: "ozon_orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "postal_code",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "product_link",
                table: "ozon_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "quantity",
                table: "ozon_orders",
                type: "numeric(10,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "shipment_weight_kg",
                table: "ozon_orders",
                type: "numeric(10,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "unit_price",
                table: "ozon_orders",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "weight_kg",
                table: "ozon_orders",
                type: "numeric(10,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "address",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "article",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "barcode",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "birth_date",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "box_number",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "city",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "cmn",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "cmn_id",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "comment",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "country",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "description_en",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "email",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "first_name",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "imei",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "imei_2",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "inn",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "last_name",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "manufacturer",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "passport_issue_date",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "passport_issued_by",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "passport_number",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "passport_series",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "patronymic",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "places_count",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "postal_code",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "product_link",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "quantity",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "shipment_weight_kg",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "unit_price",
                table: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "weight_kg",
                table: "ozon_orders");

            migrationBuilder.AlterColumn<decimal>(
                name: "weight_kg",
                table: "wbr_orders",
                type: "numeric(10,3)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_price",
                table: "wbr_orders",
                type: "numeric(10,3)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "wbr_orders",
                type: "numeric(10,3)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,0)",
                oldNullable: true);
        }
    }
}

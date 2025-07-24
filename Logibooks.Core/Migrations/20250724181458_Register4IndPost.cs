using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class Register4IndPost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "country",
                table: "wbr_orders");

            migrationBuilder.DropColumn(
                name: "country",
                table: "ozon_orders");

            migrationBuilder.AddColumn<int>(
                name: "customs_procedure_id",
                table: "registers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<short>(
                name: "dest_country_iso_numeric",
                table: "registers",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "invoice_date",
                table: "registers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_number",
                table: "registers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "transportation_type_id",
                table: "registers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<short>(
                name: "country_code",
                table: "base_orders",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.CreateTable(
                name: "customs_procedures",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<short>(type: "smallint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customs_procedures", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transportation_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<decimal>(type: "numeric(2,0)", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transportation_types", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "customs_procedures",
                columns: new[] { "id", "code", "name" },
                values: new object[,]
                {
                    { 1, (short)10, "Экспорт" },
                    { 2, (short)60, "Реимпорт" }
                });

            migrationBuilder.InsertData(
                table: "transportation_types",
                columns: new[] { "id", "code", "name" },
                values: new object[,]
                {
                    { 1, 0m, "Авиа" },
                    { 2, 1m, "Авто" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_registers_customs_procedure_id",
                table: "registers",
                column: "customs_procedure_id");

            migrationBuilder.CreateIndex(
                name: "IX_registers_dest_country_iso_numeric",
                table: "registers",
                column: "dest_country_iso_numeric");

            migrationBuilder.CreateIndex(
                name: "IX_registers_transportation_type_id",
                table: "registers",
                column: "transportation_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_base_orders_country_code",
                table: "base_orders",
                column: "country_code");

            migrationBuilder.AddForeignKey(
                name: "FK_base_orders_countries_country_code",
                table: "base_orders",
                column: "country_code",
                principalTable: "countries",
                principalColumn: "iso_numeric",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_registers_countries_dest_country_iso_numeric",
                table: "registers",
                column: "dest_country_iso_numeric",
                principalTable: "countries",
                principalColumn: "iso_numeric",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_registers_customs_procedures_customs_procedure_id",
                table: "registers",
                column: "customs_procedure_id",
                principalTable: "customs_procedures",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_registers_transportation_types_transportation_type_id",
                table: "registers",
                column: "transportation_type_id",
                principalTable: "transportation_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_base_orders_countries_country_code",
                table: "base_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_registers_countries_dest_country_iso_numeric",
                table: "registers");

            migrationBuilder.DropForeignKey(
                name: "FK_registers_customs_procedures_customs_procedure_id",
                table: "registers");

            migrationBuilder.DropForeignKey(
                name: "FK_registers_transportation_types_transportation_type_id",
                table: "registers");

            migrationBuilder.DropTable(
                name: "customs_procedures");

            migrationBuilder.DropTable(
                name: "transportation_types");

            migrationBuilder.DropIndex(
                name: "IX_registers_customs_procedure_id",
                table: "registers");

            migrationBuilder.DropIndex(
                name: "IX_registers_dest_country_iso_numeric",
                table: "registers");

            migrationBuilder.DropIndex(
                name: "IX_registers_transportation_type_id",
                table: "registers");

            migrationBuilder.DropIndex(
                name: "IX_base_orders_country_code",
                table: "base_orders");

            migrationBuilder.DropColumn(
                name: "customs_procedure_id",
                table: "registers");

            migrationBuilder.DropColumn(
                name: "dest_country_iso_numeric",
                table: "registers");

            migrationBuilder.DropColumn(
                name: "invoice_date",
                table: "registers");

            migrationBuilder.DropColumn(
                name: "invoice_number",
                table: "registers");

            migrationBuilder.DropColumn(
                name: "transportation_type_id",
                table: "registers");

            migrationBuilder.DropColumn(
                name: "country_code",
                table: "base_orders");

            migrationBuilder.AddColumn<string>(
                name: "country",
                table: "wbr_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "country",
                table: "ozon_orders",
                type: "text",
                nullable: true);
        }
    }
}

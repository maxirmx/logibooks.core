﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_8_0_Register4IndPost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.InsertData(
                table: "customs_procedures",
                columns: new[] { "id", "code", "name" },
                values: new object[,]
                {
                    { 1, (short)10, "Экспорт" },
                    { 2, (short)60, "Реимпорт" }
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
                table: "transportation_types",
                columns: new[] { "id", "code", "name" },
                values: new object[,]
                {
                    { 1, 0m, "Авиа" },
                    { 2, 1m, "Авто" }
                });

            migrationBuilder.AddColumn<short>(
                name: "dest_country_code",
                table: "registers",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "country_code",
                table: "base_orders",
                type: "smallint",
                nullable: false,
                defaultValue: (short)643);

            // First, migrate country data from WBR and Ozon orders to base_orders before dropping the columns
            migrationBuilder.Sql(@"
                -- Migrate WBR country data (ISO Alpha2 codes to numeric)
                UPDATE base_orders 
                SET country_code = COALESCE(
                    (SELECT c.iso_numeric 
                     FROM countries c 
                     WHERE UPPER(c.iso_alpha2) = UPPER(TRIM(w.country))
                     LIMIT 1), 
                    0
                )
                FROM wbr_orders w 
                WHERE base_orders.id = w.id 
                AND w.country IS NOT NULL 
                AND TRIM(w.country) != '';

                -- Migrate Ozon country data (Russian names to numeric)
                UPDATE base_orders 
                SET country_code = CASE 
                    WHEN TRIM(o.country) ILIKE 'Россия' THEN 643
                    ELSE COALESCE(
                        (SELECT c.iso_numeric 
                         FROM countries c 
                         WHERE c.name_ru_short ILIKE TRIM(o.country)
                         LIMIT 1), 
                        0
                    )
                END
                FROM ozon_orders o 
                WHERE base_orders.id = o.id 
                AND o.country IS NOT NULL 
                AND TRIM(o.country) != '';
            ");

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
                defaultValue: 1);

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
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "enabled",
                table: "feacn_orders",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.InsertData(
                table: "check_statuses",
                columns: new[] { "id", "title" },
                values: new object[] { 301, "Согласовано логистом" });

            migrationBuilder.CreateIndex(
                name: "IX_registers_customs_procedure_id",
                table: "registers",
                column: "customs_procedure_id");

            migrationBuilder.CreateIndex(
                name: "IX_registers_dest_country_code",
                table: "registers",
                column: "dest_country_code");

            migrationBuilder.CreateIndex(
                name: "IX_registers_transportation_type_id",
                table: "registers",
                column: "transportation_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_ozon_orders_posting_number",
                table: "ozon_orders",
                column: "posting_number");

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
                name: "FK_registers_countries_dest_country_code",
                table: "registers",
                column: "dest_country_code",
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
                name: "FK_registers_countries_dest_country_code",
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
                name: "IX_registers_dest_country_code",
                table: "registers");

            migrationBuilder.DropIndex(
                name: "IX_registers_transportation_type_id",
                table: "registers");

            migrationBuilder.DropIndex(
                name: "IX_ozon_orders_posting_number",
                table: "ozon_orders");

            migrationBuilder.DropIndex(
                name: "IX_base_orders_country_code",
                table: "base_orders");

            migrationBuilder.DeleteData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 301);

            migrationBuilder.DropColumn(
                name: "customs_procedure_id",
                table: "registers");

            migrationBuilder.DropColumn(
                name: "dest_country_code",
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
                name: "enabled",
                table: "feacn_orders");

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

            // Restore country data when rolling back
            migrationBuilder.Sql(@"
                -- Restore WBR country data (numeric to ISO Alpha2 codes)
                UPDATE wbr_orders 
                SET country = COALESCE(
                    (SELECT c.iso_alpha2 
                     FROM countries c 
                     JOIN base_orders b ON b.country_code = c.iso_numeric
                     WHERE b.id = wbr_orders.id
                     LIMIT 1), 
                    NULL
                );

                -- Restore Ozon country data (numeric to Russian names)
                UPDATE ozon_orders 
                SET country = CASE 
                    WHEN b.country_code = 643 THEN 'Россия'
                    ELSE COALESCE(
                        (SELECT c.name_ru_short 
                         FROM countries c 
                         WHERE c.iso_numeric = b.country_code
                         LIMIT 1), 
                        NULL
                    )
                END
                FROM base_orders b 
                WHERE b.id = ozon_orders.id 
                AND b.country_code != 0;
            ");

            migrationBuilder.DropColumn(
                name: "country_code",
                table: "base_orders");

        }
    }
}

// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_5_0_OrdersHierarchy_and_StopWords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alta_exceptions");

            migrationBuilder.DropTable(
                name: "alta_items");

            migrationBuilder.CreateTable(
                name: "base_orders",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    register_id = table.Column<int>(type: "integer", nullable: false),
                    status_id = table.Column<int>(type: "integer", nullable: false),
                    check_status_id = table.Column<int>(type: "integer", nullable: false),
                    product_name = table.Column<string>(type: "text", nullable: true),
                    tn_ved = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_base_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_base_orders_check_statuses_check_status_id",
                        column: x => x.check_status_id,
                        principalTable: "check_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_base_orders_registers_register_id",
                        column: x => x.register_id,
                        principalTable: "registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_base_orders_statuses_status_id",
                        column: x => x.status_id,
                        principalTable: "statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stop_words",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    word = table.Column<string>(type: "text", nullable: false),
                    exact_match = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stop_words", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ozon_orders",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    posting_number = table.Column<string>(type: "text", nullable: true),
                    ozon_id = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ozon_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_ozon_orders_base_orders_id",
                        column: x => x.id,
                        principalTable: "base_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wbr_orders",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    row_number = table.Column<int>(type: "integer", nullable: false),
                    order_number = table.Column<string>(type: "text", nullable: true),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: true),
                    sticker = table.Column<string>(type: "text", nullable: true),
                    shk = table.Column<string>(type: "text", nullable: true),
                    sticker_code = table.Column<string>(type: "text", nullable: true),
                    ext_id = table.Column<string>(type: "text", nullable: true),
                    site_article = table.Column<string>(type: "text", nullable: true),
                    heel_height = table.Column<string>(type: "text", nullable: true),
                    size = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    gender = table.Column<string>(type: "text", nullable: true),
                    brand = table.Column<string>(type: "text", nullable: true),
                    fabric_type = table.Column<string>(type: "text", nullable: true),
                    composition = table.Column<string>(type: "text", nullable: true),
                    lining = table.Column<string>(type: "text", nullable: true),
                    insole = table.Column<string>(type: "text", nullable: true),
                    sole = table.Column<string>(type: "text", nullable: true),
                    country = table.Column<string>(type: "text", nullable: true),
                    factory_address = table.Column<string>(type: "text", nullable: true),
                    unit = table.Column<string>(type: "text", nullable: true),
                    weight_kg = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    unit_price = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    currency = table.Column<string>(type: "text", nullable: true),
                    barcode = table.Column<string>(type: "text", nullable: true),
                    declaration = table.Column<string>(type: "text", nullable: true),
                    product_link = table.Column<string>(type: "text", nullable: true),
                    recipient_name = table.Column<string>(type: "text", nullable: true),
                    recipient_inn = table.Column<string>(type: "text", nullable: true),
                    passport_number = table.Column<string>(type: "text", nullable: true),
                    pinfl = table.Column<string>(type: "text", nullable: true),
                    recipient_address = table.Column<string>(type: "text", nullable: true),
                    contact_phone = table.Column<string>(type: "text", nullable: true),
                    box_number = table.Column<string>(type: "text", nullable: true),
                    supplier = table.Column<string>(type: "text", nullable: true),
                    supplier_inn = table.Column<string>(type: "text", nullable: true),
                    category = table.Column<string>(type: "text", nullable: true),
                    subcategory = table.Column<string>(type: "text", nullable: true),
                    personal_data = table.Column<string>(type: "text", nullable: true),
                    customs_clearance = table.Column<string>(type: "text", nullable: true),
                    duty_payment = table.Column<string>(type: "text", nullable: true),
                    other_reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wbr_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_wbr_orders_base_orders_id",
                        column: x => x.id,
                        principalTable: "base_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "base_order_stop_words",
                columns: table => new
                {
                    base_order_id = table.Column<int>(type: "integer", nullable: false),
                    stop_word_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_base_order_stop_words", x => new { x.base_order_id, x.stop_word_id });
                    table.ForeignKey(
                        name: "FK_base_order_stop_words_base_orders_base_order_id",
                        column: x => x.base_order_id,
                        principalTable: "base_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_base_order_stop_words_stop_words_stop_word_id",
                        column: x => x.stop_word_id,
                        principalTable: "stop_words",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Migrate data from the original orders table to the new TPT structure
            migrationBuilder.Sql(@"
                -- First, copy the base fields to base_orders table
                INSERT INTO base_orders (id, register_id, status_id, check_status_id, product_name, tn_ved)
                SELECT id, register_id, status_id, check_status_id, product_name, tn_ved
                FROM orders;

                -- Next, copy all WBR-specific fields to the wbr_orders table
                INSERT INTO wbr_orders (
                    id, row_number, order_number, invoice_date, sticker, shk, sticker_code, ext_id,
                    site_article, heel_height, size, description, gender, brand, fabric_type,
                    composition, lining, insole, sole, country, factory_address, unit,
                    weight_kg, quantity, unit_price, currency, barcode, declaration,
                    product_link, recipient_name, recipient_inn, passport_number, pinfl,
                    recipient_address, contact_phone, box_number, supplier, supplier_inn,
                    category, subcategory, personal_data, customs_clearance, duty_payment, other_reason
                )
                SELECT 
                    id, row_number, order_number, invoice_date, sticker, shk, sticker_code, ext_id,
                    site_article, heel_height, size, description, gender, brand, fabric_type,
                    composition, lining, insole, sole, country, factory_address, unit,
                    weight_kg, quantity, unit_price, currency, barcode, declaration,
                    product_link, recipient_name, recipient_inn, passport_number, pinfl,
                    recipient_address, contact_phone, box_number, supplier, supplier_inn,
                    category, subcategory, personal_data, customs_clearance, duty_payment, other_reason
                FROM orders;

                -- Make sure sequence is properly updated to account for existing IDs
                SELECT setval('base_orders_id_seq', (SELECT COALESCE(MAX(id), 0) + 1 FROM base_orders), false);
            ");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.UpdateData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 1,
                column: "title",
                value: "Не проверен");

            migrationBuilder.UpdateData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 101,
                column: "title",
                value: "Выявлены проблемы");

            migrationBuilder.UpdateData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 201,
                column: "title",
                value: "Не выявлено проблем");

            migrationBuilder.CreateIndex(
                name: "IX_base_order_stop_words_stop_word_id",
                table: "base_order_stop_words",
                column: "stop_word_id");

            migrationBuilder.CreateIndex(
                name: "IX_base_orders_check_status_id",
                table: "base_orders",
                column: "check_status_id");

            migrationBuilder.CreateIndex(
                name: "IX_base_orders_register_id",
                table: "base_orders",
                column: "register_id");

            migrationBuilder.CreateIndex(
                name: "IX_base_orders_status_id",
                table: "base_orders",
                column: "status_id");

            migrationBuilder.CreateIndex(
                name: "IX_base_orders_tn_ved",
                table: "base_orders",
                column: "tn_ved");

            migrationBuilder.CreateIndex(
                name: "IX_stop_words_word",
                table: "stop_words",
                column: "word",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wbr_orders_shk",
                table: "wbr_orders",
                column: "shk");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "base_order_stop_words");

            migrationBuilder.DropTable(
                name: "stop_words");

            migrationBuilder.CreateTable(
                name: "alta_exceptions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    number = table.Column<string>(type: "text", nullable: true),
                    url = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alta_exceptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "alta_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    number = table.Column<string>(type: "text", nullable: true),
                    url = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alta_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    check_status_id = table.Column<int>(type: "integer", nullable: false),
                    register_id = table.Column<int>(type: "integer", nullable: false),
                    status_id = table.Column<int>(type: "integer", nullable: false),
                    barcode = table.Column<string>(type: "text", nullable: true),
                    box_number = table.Column<string>(type: "text", nullable: true),
                    brand = table.Column<string>(type: "text", nullable: true),
                    category = table.Column<string>(type: "text", nullable: true),
                    composition = table.Column<string>(type: "text", nullable: true),
                    contact_phone = table.Column<string>(type: "text", nullable: true),
                    country = table.Column<string>(type: "text", nullable: true),
                    currency = table.Column<string>(type: "text", nullable: true),
                    customs_clearance = table.Column<string>(type: "text", nullable: true),
                    declaration = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    duty_payment = table.Column<string>(type: "text", nullable: true),
                    ext_id = table.Column<string>(type: "text", nullable: true),
                    fabric_type = table.Column<string>(type: "text", nullable: true),
                    factory_address = table.Column<string>(type: "text", nullable: true),
                    gender = table.Column<string>(type: "text", nullable: true),
                    heel_height = table.Column<string>(type: "text", nullable: true),
                    insole = table.Column<string>(type: "text", nullable: true),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: true),
                    lining = table.Column<string>(type: "text", nullable: true),
                    order_number = table.Column<string>(type: "text", nullable: true),
                    other_reason = table.Column<string>(type: "text", nullable: true),
                    passport_number = table.Column<string>(type: "text", nullable: true),
                    personal_data = table.Column<string>(type: "text", nullable: true),
                    pinfl = table.Column<string>(type: "text", nullable: true),
                    product_link = table.Column<string>(type: "text", nullable: true),
                    product_name = table.Column<string>(type: "text", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    recipient_address = table.Column<string>(type: "text", nullable: true),
                    recipient_inn = table.Column<string>(type: "text", nullable: true),
                    recipient_name = table.Column<string>(type: "text", nullable: true),
                    row_number = table.Column<int>(type: "integer", nullable: false),
                    shk = table.Column<string>(type: "text", nullable: true),
                    site_article = table.Column<string>(type: "text", nullable: true),
                    size = table.Column<string>(type: "text", nullable: true),
                    sole = table.Column<string>(type: "text", nullable: true),
                    sticker = table.Column<string>(type: "text", nullable: true),
                    sticker_code = table.Column<string>(type: "text", nullable: true),
                    subcategory = table.Column<string>(type: "text", nullable: true),
                    supplier = table.Column<string>(type: "text", nullable: true),
                    supplier_inn = table.Column<string>(type: "text", nullable: true),
                    tn_ved = table.Column<string>(type: "text", nullable: true),
                    unit = table.Column<string>(type: "text", nullable: true),
                    unit_price = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    weight_kg = table.Column<decimal>(type: "numeric(10,3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_orders_check_statuses_check_status_id",
                        column: x => x.check_status_id,
                        principalTable: "check_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orders_registers_register_id",
                        column: x => x.register_id,
                        principalTable: "registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_orders_statuses_status_id",
                        column: x => x.status_id,
                        principalTable: "statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 1,
                column: "title",
                value: "Загружен");

            migrationBuilder.UpdateData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 101,
                column: "title",
                value: "Проблема");

            migrationBuilder.UpdateData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 201,
                column: "title",
                value: "Проверен");

            migrationBuilder.CreateIndex(
                name: "IX_alta_exceptions_code",
                table: "alta_exceptions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_alta_items_code",
                table: "alta_items",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_check_status_id",
                table: "orders",
                column: "check_status_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_register_id",
                table: "orders",
                column: "register_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_shk",
                table: "orders",
                column: "shk");

            migrationBuilder.CreateIndex(
                name: "IX_orders_status_id",
                table: "orders",
                column: "status_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_tn_ved",
                table: "orders",
                column: "tn_ved");

            // Migrate data back from wbr_orders to orders
            migrationBuilder.Sql(@"
                -- Copy data from the TPT structure back to the original orders table
                INSERT INTO orders (
                    id, register_id, status_id, check_status_id, product_name, tn_ved,
                    row_number, order_number, invoice_date, sticker, shk, sticker_code, ext_id,
                    site_article, heel_height, size, description, gender, brand, fabric_type,
                    composition, lining, insole, sole, country, factory_address, unit,
                    weight_kg, quantity, unit_price, currency, barcode, declaration,
                    product_link, recipient_name, recipient_inn, passport_number, pinfl,
                    recipient_address, contact_phone, box_number, supplier, supplier_inn,
                    category, subcategory, personal_data, customs_clearance, duty_payment, other_reason
                )
                SELECT 
                    b.id, b.register_id, b.status_id, b.check_status_id, b.product_name, b.tn_ved,
                    w.row_number, w.order_number, w.invoice_date, w.sticker, w.shk, w.sticker_code, w.ext_id,
                    w.site_article, w.heel_height, w.size, w.description, w.gender, w.brand, w.fabric_type,
                    w.composition, w.lining, w.insole, w.sole, w.country, w.factory_address, w.unit,
                    w.weight_kg, w.quantity, w.unit_price, w.currency, w.barcode, w.declaration,
                    w.product_link, w.recipient_name, w.recipient_inn, w.passport_number, w.pinfl,
                    w.recipient_address, w.contact_phone, w.box_number, w.supplier, w.supplier_inn,
                    w.category, w.subcategory, w.personal_data, w.customs_clearance, w.duty_payment, w.other_reason
                FROM base_orders b
                JOIN wbr_orders w ON b.id = w.id;
            ");

            migrationBuilder.DropTable(
                name: "ozon_orders");

            migrationBuilder.DropTable(
                name: "wbr_orders");

            migrationBuilder.DropTable(
                name: "base_orders");

        }
    }
}

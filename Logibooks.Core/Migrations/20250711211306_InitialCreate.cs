using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alta_exceptions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    url = table.Column<string>(type: "text", nullable: false),
                    number = table.Column<string>(type: "text", nullable: true),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true)
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
                    url = table.Column<string>(type: "text", nullable: false),
                    number = table.Column<string>(type: "text", nullable: true),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alta_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "check_statuses",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_check_statuses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "countries",
                columns: table => new
                {
                    iso_numeric = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    iso_alpha2 = table.Column<string>(type: "text", nullable: false),
                    name_en_short = table.Column<string>(type: "text", nullable: false),
                    name_en_formal = table.Column<string>(type: "text", nullable: false),
                    name_en_official = table.Column<string>(type: "text", nullable: false),
                    name_en_cldr = table.Column<string>(type: "text", nullable: false),
                    name_ru_short = table.Column<string>(type: "text", nullable: false),
                    name_ru_formal = table.Column<string>(type: "text", nullable: false),
                    name_ru_official = table.Column<string>(type: "text", nullable: false),
                    loaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_countries", x => x.iso_numeric);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "statuses",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_statuses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    patronymic = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    password = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    inn = table.Column<string>(type: "text", nullable: false),
                    kpp = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    short_name = table.Column<string>(type: "text", nullable: false),
                    country_iso_numeric = table.Column<short>(type: "smallint", nullable: false),
                    postal_code = table.Column<string>(type: "text", nullable: false),
                    city = table.Column<string>(type: "text", nullable: false),
                    street = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companies", x => x.id);
                    table.ForeignKey(
                        name: "FK_companies_countries_country_iso_numeric",
                        column: x => x.country_iso_numeric,
                        principalTable: "countries",
                        principalColumn: "iso_numeric",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "registers",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    filename = table.Column<string>(type: "text", nullable: false),
                    dtime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    company_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registers", x => x.id);
                    table.ForeignKey(
                        name: "FK_registers_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    register_id = table.Column<int>(type: "integer", nullable: false),
                    status_id = table.Column<int>(type: "integer", nullable: false),
                    check_status_id = table.Column<int>(type: "integer", nullable: false),
                    row_number = table.Column<int>(type: "integer", nullable: false),
                    order_number = table.Column<string>(type: "text", nullable: true),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: true),
                    sticker = table.Column<string>(type: "text", nullable: true),
                    shk = table.Column<string>(type: "text", nullable: true),
                    sticker_code = table.Column<string>(type: "text", nullable: true),
                    ext_id = table.Column<string>(type: "text", nullable: true),
                    tn_ved = table.Column<string>(type: "text", nullable: true),
                    site_article = table.Column<string>(type: "text", nullable: true),
                    heel_height = table.Column<string>(type: "text", nullable: true),
                    size = table.Column<string>(type: "text", nullable: true),
                    product_name = table.Column<string>(type: "text", nullable: true),
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

            migrationBuilder.InsertData(
                table: "check_statuses",
                columns: new[] { "id", "title" },
                values: new object[,]
                {
                    { 1, "Загружен" },
                    { 101, "Проблема" },
                    { 201, "Проверен" }
                });

            migrationBuilder.InsertData(
                table: "countries",
                columns: new[] { "iso_numeric", "iso_alpha2", "loaded_at", "name_en_cldr", "name_en_formal", "name_en_official", "name_en_short", "name_ru_formal", "name_ru_official", "name_ru_short" },
                values: new object[] { (short)643, "RU", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Rusia", "the Russian Federation", "Russian Federation", "Russian Federation (the)", "Российская Федерация", "Российская Федерация", "Российская Федерация" });

            migrationBuilder.InsertData(
                table: "roles",
                columns: new[] { "id", "name", "title" },
                values: new object[,]
                {
                    { 1, "logist", "Логист" },
                    { 2, "administrator", "Администратор" }
                });

            migrationBuilder.InsertData(
                table: "statuses",
                columns: new[] { "id", "title" },
                values: new object[] { 1, "Не известен" });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "email", "first_name", "last_name", "password", "patronymic" },
                values: new object[,]
                {
                    { 1, "maxirmx@sw.consulting", "Maxim", "Samsonov", "$2b$12$eOXzlwFzyGVERe0sNwFeJO5XnvwsjloUpL4o2AIQ8254RT88MnsDi", "" },
                    { 2, "director@global-tc.ru", "Эльдар", "Сергутов", "$2a$11$KUvUbYg79OvDjq9xFKw1Ge4AYboMse4xduI.ZD54vp28zkb4DjWfK", "Юрьевич" },
                    { 3, "wild@global-tc.ru", "Полина", "Баландина", "$2a$11$zA1ohkl1U6UGbkhUlNvtTexHkbQ7CtiFnHTSsBc4xz8a5BY8D9yDS", "Анатольевна" }
                });

            migrationBuilder.InsertData(
                table: "companies",
                columns: new[] { "id", "city", "country_iso_numeric", "inn", "kpp", "name", "postal_code", "short_name", "street" },
                values: new object[,]
                {
                    { 1, "Москва", (short)643, "7704217370", "997750001", "ООО \"Интернет Решения\"", "123112", "", "Пресненская набережная д.10, пом.1, этаж 41, ком.6" },
                    { 2, "д. Коледино", (short)643, "9714053621", "507401001", "", "", "ООО \"РВБ\"", "Индустриальный Парк Коледино, д.6, стр.1" }
                });

            migrationBuilder.InsertData(
                table: "user_roles",
                columns: new[] { "role_id", "user_id" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 2, 1 },
                    { 1, 2 },
                    { 2, 2 },
                    { 1, 3 }
                });

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
                name: "IX_companies_country_iso_numeric",
                table: "companies",
                column: "country_iso_numeric");

            migrationBuilder.CreateIndex(
                name: "IX_companies_inn",
                table: "companies",
                column: "inn",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_check_status_id",
                table: "orders",
                column: "check_status_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_order_number",
                table: "orders",
                column: "order_number");

            migrationBuilder.CreateIndex(
                name: "IX_orders_register_id_order_number",
                table: "orders",
                columns: new[] { "register_id", "order_number" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_status_id",
                table: "orders",
                column: "status_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_tn_ved",
                table: "orders",
                column: "tn_ved");

            migrationBuilder.CreateIndex(
                name: "IX_registers_company_id",
                table: "registers",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_role_id",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alta_exceptions");

            migrationBuilder.DropTable(
                name: "alta_items");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "check_statuses");

            migrationBuilder.DropTable(
                name: "registers");

            migrationBuilder.DropTable(
                name: "statuses");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "companies");

            migrationBuilder.DropTable(
                name: "countries");
        }
    }
}

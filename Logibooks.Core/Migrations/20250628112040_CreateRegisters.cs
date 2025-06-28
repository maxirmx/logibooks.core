using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class CreateRegisters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "registers",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    filename = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "statuses",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_statuses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    register_id = table.Column<int>(type: "integer", nullable: false),
                    status_id = table.Column<int>(type: "integer", nullable: false),
                    row_number = table.Column<int>(type: "integer", nullable: false),
                    order_number = table.Column<string>(type: "text", nullable: true),
                    invoice_date = table.Column<string>(type: "text", nullable: true),
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
                    weight_kg = table.Column<string>(type: "text", nullable: true),
                    quantity = table.Column<string>(type: "text", nullable: true),
                    unit_price = table.Column<string>(type: "text", nullable: true),
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
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "statuses",
                columns: new[] { "id", "name", "title" },
                values: new object[] { 1, "loaded", "Загружен" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_register_id",
                table: "orders",
                column: "register_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_status_id",
                table: "orders",
                column: "status_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "registers");

            migrationBuilder.DropTable(
                name: "statuses");
        }
    }
}

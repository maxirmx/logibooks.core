using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_6_0_Feacn_Codes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "feacn_orders",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: true),
                    comment = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feacn_orders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "feacn_prefixes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    comment = table.Column<string>(type: "text", nullable: true),
                    interval_code = table.Column<string>(type: "text", nullable: true),
                    feacn_order_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feacn_prefixes", x => x.id);
                    table.ForeignKey(
                        name: "FK_feacn_prefixes_feacn_orders_feacn_order_id",
                        column: x => x.feacn_order_id,
                        principalTable: "feacn_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "base_order_feacn_prefixes",
                columns: table => new
                {
                    base_order_id = table.Column<int>(type: "integer", nullable: false),
                    feacn_prefix_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_base_order_feacn_prefixes", x => new { x.base_order_id, x.feacn_prefix_id });
                    table.ForeignKey(
                        name: "FK_base_order_feacn_prefixes_base_orders_base_order_id",
                        column: x => x.base_order_id,
                        principalTable: "base_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_base_order_feacn_prefixes_feacn_prefixes_feacn_prefix_id",
                        column: x => x.feacn_prefix_id,
                        principalTable: "feacn_prefixes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "feacn_prefix_exceptions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: false),
                    feacn_prefix_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feacn_prefix_exceptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_feacn_prefix_exceptions_feacn_prefixes_feacn_prefix_id",
                        column: x => x.feacn_prefix_id,
                        principalTable: "feacn_prefixes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "check_statuses",
                columns: new[] { "id", "title" },
                values: new object[,]
                {
                    { 102, "Неправильный формат ТН ВЭД" },
                    { 103, "Несуществующий ТН ВЭД" }
                });

            migrationBuilder.InsertData(
                table: "feacn_orders",
                columns: new[] { "id", "comment", "title", "Url" },
                values: new object[,]
                {
                    { 1, "Подлежит ветеринарному контролю", "Решение Комиссии Таможенного союза от 18 июня 2010 г. N 317 \"О применении ветеринарно-санитарных мер в Евразийском экономическом союзе\"", "10sr0317" },
                    { 2, "Подлежит карантинному фитосанитарному контролю", "Решение Комиссии Таможенного союза от 18 июня 2010 г. N 318 \"Об обеспечении карантина растений в Евразийском экономическом союзе\"", "10sr0318" },
                    { 3, "Операции в отношении драгоценных металлов и драгоценных камней", "Приказ ФТС России от 12 мая 2011 г. N 971 \"О компетенции таможенных органов по совершению таможенных операций в отношении драгоценных металлов и драгоценных камней\"", "11pr0971" },
                    { 4, "Временный запрет на вывоз", "Постановление Правительства РФ от 09.03.2022 № 311 \"О мерах по реализации Указа Президента Российской Федерации от 8 марта 2022 г. N 100\"", "22ps0311" },
                    { 5, "Разрешительный порядок вывоза", "Постановление Правительства Российской Федерации от 9 марта 2022 г. N 312 \"О введении на временной основе разрешительного порядка вывоза отдельных видов товаров за пределы территории Российской Федерации\"", "22ps0312" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_base_order_feacn_prefixes_feacn_prefix_id",
                table: "base_order_feacn_prefixes",
                column: "feacn_prefix_id");

            migrationBuilder.CreateIndex(
                name: "IX_feacn_prefix_exceptions_feacn_prefix_id",
                table: "feacn_prefix_exceptions",
                column: "feacn_prefix_id");

            migrationBuilder.CreateIndex(
                name: "IX_feacn_prefixes_feacn_order_id",
                table: "feacn_prefixes",
                column: "feacn_order_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "base_order_feacn_prefixes");

            migrationBuilder.DropTable(
                name: "feacn_prefix_exceptions");

            migrationBuilder.DropTable(
                name: "feacn_prefixes");

            migrationBuilder.DropTable(
                name: "feacn_orders");

            migrationBuilder.DeleteData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 102);

            migrationBuilder.DeleteData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 103);
        }
    }
}

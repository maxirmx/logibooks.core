using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_9_0_Keywords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stop_words_stop_word_match_types_match_type_id",
                table: "stop_words");

            // Rename the existing table instead of dropping and recreating
            migrationBuilder.RenameTable(
                name: "stop_word_match_types",
                newName: "word_match_types");

            // Fix the typo in the existing data
            migrationBuilder.UpdateData(
                table: "word_match_types",
                keyColumn: "id",
                keyValue: 1,
                column: "name",
                value: "Точная последовательность букв, цифр и пробелов");

            migrationBuilder.CreateTable(
                name: "feacn_codes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feacn_codes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "key_words",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    word = table.Column<string>(type: "text", nullable: false),
                    match_type_id = table.Column<int>(type: "integer", nullable: false),
                    feacn_code_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_key_words", x => x.id);
                    table.ForeignKey(
                        name: "FK_key_words_feacn_codes_feacn_code_id",
                        column: x => x.feacn_code_id,
                        principalTable: "feacn_codes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_key_words_word_match_types_match_type_id",
                        column: x => x.match_type_id,
                        principalTable: "word_match_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "base_order_key_words",
                columns: table => new
                {
                    base_order_id = table.Column<int>(type: "integer", nullable: false),
                    key_word_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_base_order_key_words", x => new { x.base_order_id, x.key_word_id });
                    table.ForeignKey(
                        name: "FK_base_order_key_words_base_orders_base_order_id",
                        column: x => x.base_order_id,
                        principalTable: "base_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_base_order_key_words_key_words_key_word_id",
                        column: x => x.key_word_id,
                        principalTable: "key_words",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "transportation_types",
                keyColumn: "id",
                keyValue: 1,
                column: "name",
                value: "AWB");

            migrationBuilder.UpdateData(
                table: "transportation_types",
                keyColumn: "id",
                keyValue: 2,
                column: "name",
                value: "CMR");

            migrationBuilder.CreateIndex(
                name: "IX_base_order_key_words_key_word_id",
                table: "base_order_key_words",
                column: "key_word_id");

            migrationBuilder.CreateIndex(
                name: "IX_key_words_feacn_code_id",
                table: "key_words",
                column: "feacn_code_id");

            migrationBuilder.CreateIndex(
                name: "IX_key_words_match_type_id",
                table: "key_words",
                column: "match_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_key_words_word",
                table: "key_words",
                column: "word",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_stop_words_word_match_types_match_type_id",
                table: "stop_words",
                column: "match_type_id",
                principalTable: "word_match_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stop_words_word_match_types_match_type_id",
                table: "stop_words");

            migrationBuilder.DropTable(
                name: "base_order_key_words");

            migrationBuilder.DropTable(
                name: "key_words");

            migrationBuilder.DropTable(
                name: "feacn_codes");

            // Revert the typo fix
            migrationBuilder.UpdateData(
                table: "word_match_types",
                keyColumn: "id",
                keyValue: 1,
                column: "name",
                value: "Точная последовательность букв, цифр и проблелов");

            // Rename the table back to its original name
            migrationBuilder.RenameTable(
                name: "word_match_types",
                newName: "stop_word_match_types");


            migrationBuilder.UpdateData(
                table: "transportation_types",
                keyColumn: "id",
                keyValue: 1,
                column: "name",
                value: "Авиа");

            migrationBuilder.UpdateData(
                table: "transportation_types",
                keyColumn: "id",
                keyValue: 2,
                column: "name",
                value: "Авто");

            migrationBuilder.AddForeignKey(
                name: "FK_stop_words_stop_word_match_types_match_type_id",
                table: "stop_words",
                column: "match_type_id",
                principalTable: "stop_word_match_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

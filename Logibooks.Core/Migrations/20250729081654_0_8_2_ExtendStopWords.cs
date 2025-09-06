using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_8_2_ExtendStopWords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_base_orders_register_id",
                table: "base_orders");

            migrationBuilder.AddColumn<int>(
                name: "match_type_id",
                table: "stop_words",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
                UPDATE stop_words SET match_type_id = CASE
                    WHEN exact_match THEN 11
                    ELSE 51
                END
            ");

            migrationBuilder.DropColumn(
                name: "exact_match",
                table: "stop_words");

            migrationBuilder.CreateTable(
                name: "stop_word_match_type",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stop_word_match_type", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "stop_word_match_type",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { 1, "Точная последовательность букв, цифр и проблелов" },
                    { 11, "Точное слово" },
                    { 21, "Фраза (последовательность слов)" },
                    { 41, "Слово и его формы (Золото -> c золотом, о золоте, ..." },
                    { 51, "Слово и однокоренные (Золото -> золотой, золотистый, ..." }
                });

            migrationBuilder.CreateIndex(
                name: "IX_stop_words_match_type_id",
                table: "stop_words",
                column: "match_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_base_orders_registerid_checkstatusid_id",
                table: "base_orders",
                columns: new[] { "register_id", "check_status_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "FK_stop_words_stop_word_match_type_match_type_id",
                table: "stop_words",
                column: "match_type_id",
                principalTable: "stop_word_match_type",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stop_words_stop_word_match_type_match_type_id",
                table: "stop_words");

            migrationBuilder.DropTable(
                name: "stop_word_match_type");

            migrationBuilder.DropIndex(
                name: "IX_stop_words_match_type_id",
                table: "stop_words");

            migrationBuilder.DropIndex(
                name: "IX_base_orders_registerid_checkstatusid_id",
                table: "base_orders");

            migrationBuilder.AddColumn<bool>(
                name: "exact_match",
                table: "stop_words",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(@"
                UPDATE stop_words SET exact_match = CASE
                    WHEN match_type_id < 41 THEN TRUE
                    ELSE FALSE
                END
            ");

            migrationBuilder.DropColumn(
                name: "match_type_id",
                table: "stop_words");

            migrationBuilder.CreateIndex(
                name: "IX_base_orders_register_id",
                table: "base_orders",
                column: "register_id");
        }
    }
}

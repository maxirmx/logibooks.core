using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_5_0_StopWords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_base_order_stop_words_stop_word_id",
                table: "base_order_stop_words",
                column: "stop_word_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "base_order_stop_words");

            migrationBuilder.DropTable(
                name: "stop_words");
        }
    }
}

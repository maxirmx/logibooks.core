// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

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

            // Rename the table instead of dropping and recreating
            migrationBuilder.RenameTable(
                name: "stop_word_match_types",
                newName: "word_match_types");

            // Update the data to fix the typo: "проблелов" -> "пробелов"
            migrationBuilder.UpdateData(
                table: "word_match_types",
                keyColumn: "id",
                keyValue: 1,
                column: "name",
                value: "Точная последовательность букв, цифр и пробелов");

            migrationBuilder.CreateTable(
                name: "key_words",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    word = table.Column<string>(type: "text", nullable: false),
                    match_type_id = table.Column<int>(type: "integer", nullable: false),
                    feacn_code = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_key_words", x => x.id);
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

            migrationBuilder.CreateIndex(
                name: "IX_base_order_key_words_key_word_id",
                table: "base_order_key_words",
                column: "key_word_id");

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

            // Rename back to the original table name
            migrationBuilder.RenameTable(
                name: "word_match_types",
                newName: "stop_word_match_types");

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

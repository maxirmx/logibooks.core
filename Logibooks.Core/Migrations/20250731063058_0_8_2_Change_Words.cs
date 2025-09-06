// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_8_2_Change_Words : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 101,
                column: "title",
                value: "Запрет");

            migrationBuilder.UpdateData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 201,
                column: "title",
                value: "Ок");

            migrationBuilder.UpdateData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 301,
                column: "title",
                value: "Согласовано");

            migrationBuilder.UpdateData(
                table: "stop_word_match_type",
                keyColumn: "id",
                keyValue: 41,
                column: "name",
                value: "Слово и его формы (Золото -> c золотом, о золоте, ...)");

            migrationBuilder.UpdateData(
                table: "stop_word_match_type",
                keyColumn: "id",
                keyValue: 51,
                column: "name",
                value: "Слово и однокоренные (Золото -> золотой, золотистый, ...)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.UpdateData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 301,
                column: "title",
                value: "Согласовано логистом");

            migrationBuilder.UpdateData(
                table: "stop_word_match_type",
                keyColumn: "id",
                keyValue: 41,
                column: "name",
                value: "Слово и его формы (Золото -> c золотом, о золоте, ...");

            migrationBuilder.UpdateData(
                table: "stop_word_match_type",
                keyColumn: "id",
                keyValue: 51,
                column: "name",
                value: "Слово и однокоренные (Золото -> золотой, золотистый, ...");
        }
    }
}

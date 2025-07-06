using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddAdditionalOrderStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "statuses",
                columns: ["id", "name", "title"],
                values: new object[,]
                {
                    { 101, "issue", "Проблема" },
                    { 201, "ok", "Без проблем" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "statuses",
                keyColumn: "id",
                keyValue: 101);

            migrationBuilder.DeleteData(
                table: "statuses",
                keyColumn: "id",
                keyValue: 201);
        }
    }
}

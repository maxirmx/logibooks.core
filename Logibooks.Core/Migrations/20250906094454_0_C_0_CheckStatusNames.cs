using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_C_0_CheckStatusNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "check_statuses",
                columns: new[] { "id", "title" },
                values: new object[,]
                {
                    { 129, "Стоп ТН ВЭД" },
                    { 130, "Стоп ТН ВЭД/Слово" },
                    { 131, "Нет ТН ВЭД" },
                    { 132, "Нет ТН ВЭД, Cтоп слово" },
                    { 133, "Формат ТН ВЭД" },
                    { 134, "Формат ТН ВЭД, Стоп слово" },
                    { 135, "Стоп слово" },
                    { 136, "Стоп слово" },
                    { 137, "Стоп ТН ВЭД" },
                    { 138, "Нет ТН ВЭД" },
                    { 139, "Формат ТН ВЭД" },
                    { 202, "Ок (стоп-слова)" },
                    { 203, "Ок (ТН ВЭД)" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // First, update any foreign key references to point to id=1 before deleting
            migrationBuilder.Sql(@"
                UPDATE parcels SET check_status_id = 1 WHERE check_status_id IN (129, 130, 131, 132, 133, 134, 135, 136, 137, 138, 139, 202, 203);
                UPDATE parcel_views SET check_status_id = 1 WHERE check_status_id IN (129, 130, 131, 132, 133, 134, 135, 136, 137, 138, 139, 202, 203);
            ");

            migrationBuilder.DeleteData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 129);

            migrationBuilder.DeleteData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 130);

            migrationBuilder.DeleteData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 131);

            migrationBuilder.DeleteData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 132);

            migrationBuilder.DeleteData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 133);

            migrationBuilder.DeleteData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 134);

            migrationBuilder.DeleteData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 135);

            migrationBuilder.DeleteData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 136);

            migrationBuilder.DeleteData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 137);

            migrationBuilder.DeleteData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 138);

            migrationBuilder.DeleteData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 139);

            migrationBuilder.DeleteData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 202);

            migrationBuilder.DeleteData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 203);
        }
    }
}

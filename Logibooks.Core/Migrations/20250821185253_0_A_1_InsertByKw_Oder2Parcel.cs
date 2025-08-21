using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_A_1_InsertByKw_Oder2Parcel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "insert_after",
                table: "key_words",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "insert_before",
                table: "key_words",
                type: "text",
                nullable: true);

            migrationBuilder.RenameIndex(
                name: "IX_ozon_orders_posting_number",
                table: "ozon_orders",
                newName: "IX_ozon_parcels_posting_number");

            migrationBuilder.RenameTable(
                name: "ozon_orders",
                newName: "ozon_parcels");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_ozon_parcels_posting_number",
                table: "ozon_parcels",
                newName: "IX_ozon_orders_posting_number");

            migrationBuilder.RenameTable(
                name: "ozon_parcels",
                newName: "ozon_orders");

            migrationBuilder.DropColumn(
                name: "insert_after",
                table: "key_words");

            migrationBuilder.DropColumn(
                name: "insert_before",
                table: "key_words");
        }
    }
}

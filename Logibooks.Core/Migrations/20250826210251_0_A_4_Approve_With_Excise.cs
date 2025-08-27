using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_A_4_Approve_With_Excise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the old foreign key constraint before renaming
            migrationBuilder.DropForeignKey(
                name: "FK_base_order_key_words_base_orders_base_order_id",
                table: "base_order_key_words");

            // Drop the old primary key constraint before renaming
            migrationBuilder.DropPrimaryKey(
                name: "PK_base_order_key_words",
                table: "base_order_key_words");

            // Rename table from base_order_key_words to base_parcel_key_words
            migrationBuilder.RenameTable(
                name: "base_order_key_words",
                newName: "base_parcel_key_words");

            // Rename column from base_order_id to base_parcel_id
            migrationBuilder.RenameColumn(
                name: "base_order_id",
                table: "base_parcel_key_words",
                newName: "base_parcel_id");

            // Rename the index
            migrationBuilder.RenameIndex(
                name: "IX_base_order_key_words_key_word_id",
                table: "base_parcel_key_words",
                newName: "IX_base_parcel_key_words_key_word_id");

            // Add the new primary key constraint
            migrationBuilder.AddPrimaryKey(
                name: "PK_base_parcel_key_words",
                table: "base_parcel_key_words",
                columns: new[] { "base_parcel_id", "key_word_id" });

            // Add the new foreign key constraint
            migrationBuilder.AddForeignKey(
                name: "FK_base_parcel_key_words_base_parcels_base_parcel_id",
                table: "base_parcel_key_words",
                column: "base_parcel_id",
                principalTable: "base_parcels",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.UpdateData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 200,
                column: "title",
                value: "Запрещено партнёром");

            migrationBuilder.InsertData(
                table: "check_statuses",
                columns: new[] { "id", "title" },
                values: new object[] { 399, "Согласовано с акцизом" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the new foreign key constraint before renaming back
            migrationBuilder.DropForeignKey(
                name: "FK_base_parcel_key_words_base_parcels_base_parcel_id",
                table: "base_parcel_key_words");

            // Drop the new primary key constraint before renaming back
            migrationBuilder.DropPrimaryKey(
                name: "PK_base_parcel_key_words",
                table: "base_parcel_key_words");

            // Rename table back from base_parcel_key_words to base_order_key_words
            migrationBuilder.RenameTable(
                name: "base_parcel_key_words",
                newName: "base_order_key_words");

            // Rename column back from base_parcel_id to base_order_id
            migrationBuilder.RenameColumn(
                name: "base_parcel_id",
                table: "base_order_key_words",
                newName: "base_order_id");

            // Rename the index back
            migrationBuilder.RenameIndex(
                name: "IX_base_parcel_key_words_key_word_id",
                table: "base_order_key_words",
                newName: "IX_base_order_key_words_key_word_id");

            // Add the old primary key constraint
            migrationBuilder.AddPrimaryKey(
                name: "PK_base_order_key_words",
                table: "base_order_key_words",
                columns: new[] { "base_order_id", "key_word_id" });

            // Add the old foreign key constraint
            migrationBuilder.AddForeignKey(
                name: "FK_base_order_key_words_base_orders_base_order_id",
                table: "base_order_key_words",
                column: "base_order_id",
                principalTable: "base_parcels",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DeleteData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 399);

            migrationBuilder.UpdateData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 200,
                column: "title",
                value: "Отмечено партнёром");
        }
    }
}

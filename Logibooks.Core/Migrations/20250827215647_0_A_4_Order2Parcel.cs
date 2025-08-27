using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_A_4_Order2Parcel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the old foreign key constraints and primary key before renaming
            migrationBuilder.DropForeignKey(
                name: "FK_base_order_stop_words_base_orders_base_order_id",
                table: "base_order_stop_words");

            migrationBuilder.DropForeignKey(
                name: "FK_base_order_stop_words_stop_words_stop_word_id",
                table: "base_order_stop_words");

            migrationBuilder.DropPrimaryKey(
                name: "PK_base_order_stop_words",
                table: "base_order_stop_words");

            // Rename table from base_order_stop_words to base_parcel_stop_words
            migrationBuilder.RenameTable(
                name: "base_order_stop_words",
                newName: "base_parcel_stop_words");

            // Rename column from base_order_id to base_parcel_id
            migrationBuilder.RenameColumn(
                name: "base_order_id",
                table: "base_parcel_stop_words",
                newName: "base_parcel_id");

            // Rename the index
            migrationBuilder.RenameIndex(
                name: "IX_base_order_stop_words_stop_word_id",
                table: "base_parcel_stop_words",
                newName: "IX_base_parcel_stop_words_stop_word_id");

            // Add the new primary key constraint
            migrationBuilder.AddPrimaryKey(
                name: "PK_base_parcel_stop_words",
                table: "base_parcel_stop_words",
                columns: new[] { "base_parcel_id", "stop_word_id" });

            // Add the new foreign key constraints
            migrationBuilder.AddForeignKey(
                name: "FK_base_parcel_stop_words_base_parcels_base_parcel_id",
                table: "base_parcel_stop_words",
                column: "base_parcel_id",
                principalTable: "base_parcels",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_base_parcel_stop_words_stop_words_stop_word_id",
                table: "base_parcel_stop_words",
                column: "stop_word_id",
                principalTable: "stop_words",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the new foreign key constraints and primary key before renaming back
            migrationBuilder.DropForeignKey(
                name: "FK_base_parcel_stop_words_base_parcels_base_parcel_id",
                table: "base_parcel_stop_words");

            migrationBuilder.DropForeignKey(
                name: "FK_base_parcel_stop_words_stop_words_stop_word_id",
                table: "base_parcel_stop_words");

            migrationBuilder.DropPrimaryKey(
                name: "PK_base_parcel_stop_words",
                table: "base_parcel_stop_words");

            // Rename table back from base_parcel_stop_words to base_order_stop_words
            migrationBuilder.RenameTable(
                name: "base_parcel_stop_words",
                newName: "base_order_stop_words");

            // Rename column back from base_parcel_id to base_order_id
            migrationBuilder.RenameColumn(
                name: "base_parcel_id",
                table: "base_order_stop_words",
                newName: "base_order_id");

            // Rename the index back
            migrationBuilder.RenameIndex(
                name: "IX_base_parcel_stop_words_stop_word_id",
                table: "base_order_stop_words",
                newName: "IX_base_order_stop_words_stop_word_id");

            // Add the old primary key constraint
            migrationBuilder.AddPrimaryKey(
                name: "PK_base_order_stop_words",
                table: "base_order_stop_words",
                columns: new[] { "base_order_id", "stop_word_id" });

            // Add the old foreign key constraints
            migrationBuilder.AddForeignKey(
                name: "FK_base_order_stop_words_base_orders_base_order_id",
                table: "base_order_stop_words",
                column: "base_order_id",
                principalTable: "base_parcels",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_base_order_stop_words_stop_words_stop_word_id",
                table: "base_order_stop_words",
                column: "stop_word_id",
                principalTable: "stop_words",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexesForMatchSorting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_key_word_feacn_codes_feacn_code",
                table: "key_word_feacn_codes",
                column: "feacn_code");

            migrationBuilder.CreateIndex(
                name: "IX_key_word_feacn_codes_key_word_id",
                table: "key_word_feacn_codes",
                column: "key_word_id");

            migrationBuilder.CreateIndex(
                name: "IX_base_parcel_key_words_base_parcel_id",
                table: "base_parcel_key_words",
                column: "base_parcel_id");

            migrationBuilder.CreateIndex(
                name: "IX_base_parcel_key_words_base_parcel_id_key_word_id",
                table: "base_parcel_key_words",
                columns: new[] { "base_parcel_id", "key_word_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_key_word_feacn_codes_feacn_code",
                table: "key_word_feacn_codes");

            migrationBuilder.DropIndex(
                name: "IX_key_word_feacn_codes_key_word_id",
                table: "key_word_feacn_codes");

            migrationBuilder.DropIndex(
                name: "IX_base_parcel_key_words_base_parcel_id",
                table: "base_parcel_key_words");

            migrationBuilder.DropIndex(
                name: "IX_base_parcel_key_words_base_parcel_id_key_word_id",
                table: "base_parcel_key_words");
        }
    }
}

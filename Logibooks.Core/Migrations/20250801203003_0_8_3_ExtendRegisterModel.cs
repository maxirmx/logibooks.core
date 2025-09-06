using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_8_3_ExtendRegisterModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_registers_countries_dest_country_code",
                table: "registers");

            migrationBuilder.DropForeignKey(
                name: "FK_stop_words_stop_word_match_type_match_type_id",
                table: "stop_words");

            migrationBuilder.DropPrimaryKey(
                name: "PK_stop_word_match_type",
                table: "stop_word_match_type");

            migrationBuilder.RenameTable(
                name: "stop_word_match_type",
                newName: "stop_word_match_types");

            migrationBuilder.RenameColumn(
                name: "dest_country_code",
                table: "registers",
                newName: "the_other_country_code");

            migrationBuilder.RenameIndex(
                name: "IX_registers_dest_country_code",
                table: "registers",
                newName: "IX_registers_the_other_country_code");

            migrationBuilder.AddColumn<string>(
                name: "deal_number",
                table: "registers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "the_other_company_id",
                table: "registers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_stop_word_match_types",
                table: "stop_word_match_types",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "IX_registers_the_other_company_id",
                table: "registers",
                column: "the_other_company_id");

            migrationBuilder.AddForeignKey(
                name: "FK_registers_companies_the_other_company_id",
                table: "registers",
                column: "the_other_company_id",
                principalTable: "companies",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_registers_countries_the_other_country_code",
                table: "registers",
                column: "the_other_country_code",
                principalTable: "countries",
                principalColumn: "iso_numeric",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stop_words_stop_word_match_types_match_type_id",
                table: "stop_words",
                column: "match_type_id",
                principalTable: "stop_word_match_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_registers_companies_the_other_company_id",
                table: "registers");

            migrationBuilder.DropForeignKey(
                name: "FK_registers_countries_the_other_country_code",
                table: "registers");

            migrationBuilder.DropForeignKey(
                name: "FK_stop_words_stop_word_match_types_match_type_id",
                table: "stop_words");

            migrationBuilder.DropIndex(
                name: "IX_registers_the_other_company_id",
                table: "registers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_stop_word_match_types",
                table: "stop_word_match_types");

            migrationBuilder.DropColumn(
                name: "deal_number",
                table: "registers");

            migrationBuilder.DropColumn(
                name: "the_other_company_id",
                table: "registers");

            migrationBuilder.RenameTable(
                name: "stop_word_match_types",
                newName: "stop_word_match_type");

            migrationBuilder.RenameColumn(
                name: "the_other_country_code",
                table: "registers",
                newName: "dest_country_code");

            migrationBuilder.RenameIndex(
                name: "IX_registers_the_other_country_code",
                table: "registers",
                newName: "IX_registers_dest_country_code");

            migrationBuilder.AddPrimaryKey(
                name: "PK_stop_word_match_type",
                table: "stop_word_match_type",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_registers_countries_dest_country_code",
                table: "registers",
                column: "dest_country_code",
                principalTable: "countries",
                principalColumn: "iso_numeric",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stop_words_stop_word_match_type_match_type_id",
                table: "stop_words",
                column: "match_type_id",
                principalTable: "stop_word_match_type",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddCountryCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "iso_alpha2",
                table: "country_codes",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "iso_alpha2",
                table: "country_codes",
                type: "character(2)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}

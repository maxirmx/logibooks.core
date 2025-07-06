using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class AltaParser2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_alta_items_code",
                table: "alta_items",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_alta_exceptions_code",
                table: "alta_exceptions",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_alta_items_code",
                table: "alta_items");

            migrationBuilder.DropIndex(
                name: "IX_alta_exceptions_code",
                table: "alta_exceptions");
        }
    }
}

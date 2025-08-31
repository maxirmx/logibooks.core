using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_A_7_AWB_CMR : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "document",
                table: "transportation_types",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "transportation_types",
                keyColumn: "id",
                keyValue: 1,
                column: "document",
                value: "AWB");

            migrationBuilder.UpdateData(
                table: "transportation_types",
                keyColumn: "id",
                keyValue: 2,
                column: "document",
                value: "CMR");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "document",
                table: "transportation_types");
        }
    }
}

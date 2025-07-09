using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddCountryCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "country_codes",
                columns: table => new
                {
                    iso_numeric = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    iso_alpha2 = table.Column<string>(type: "text", nullable: false),
                    name_en_short = table.Column<string>(type: "text", nullable: true),
                    name_en_formal = table.Column<string>(type: "text", nullable: true),
                    name_en_official = table.Column<string>(type: "text", nullable: true),
                    name_en_cldr = table.Column<string>(type: "text", nullable: true),
                    name_ru_short = table.Column<string>(type: "text", nullable: true),
                    name_ru_formal = table.Column<string>(type: "text", nullable: true),
                    name_ru_official = table.Column<string>(type: "text", nullable: true),
                    loaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_country_codes", x => x.iso_numeric);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "country_codes");
        }
    }
}

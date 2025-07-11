using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class Re : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_country_codes",
                table: "country_codes");

            migrationBuilder.RenameTable(
                name: "country_codes",
                newName: "countries");

            migrationBuilder.AddPrimaryKey(
                name: "PK_countries",
                table: "countries",
                column: "iso_numeric");

            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    inn = table.Column<string>(type: "text", nullable: false),
                    kpp = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    short_name = table.Column<string>(type: "text", nullable: false),
                    country_iso_numeric = table.Column<short>(type: "smallint", nullable: false),
                    postal_code = table.Column<string>(type: "text", nullable: false),
                    city = table.Column<string>(type: "text", nullable: false),
                    street = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companies", x => x.id);
                    table.ForeignKey(
                        name: "FK_companies_countries_country_iso_numeric",
                        column: x => x.country_iso_numeric,
                        principalTable: "countries",
                        principalColumn: "iso_numeric",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_companies_country_iso_numeric",
                table: "companies",
                column: "country_iso_numeric");

            migrationBuilder.Sql(@"
                INSERT INTO countries (
                    iso_numeric, iso_alpha2, name_en_short, name_en_formal, name_en_official, name_en_cldr, name_ru_short, name_ru_formal, name_ru_official, loaded_at
                )
                SELECT 643, 'RU', 'Russian Federation (the)', 'the Russian Federation', 'Russian Federation', 'Rusia', 'Российская Федерация', 'Российская Федерация', 'Российская Федерация', NOW()
                WHERE NOT EXISTS (
                    SELECT 1 FROM countries WHERE iso_numeric = 643
                );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO companies
                    (inn, kpp, name, short_name, country_iso_numeric, postal_code, city, street)
                VALUES
                    ('7704217370', '997750001', 'ООО Интернет Решения', '', 643, '123112', 'Москва', 'Пресненская набережная д.10, пом.1, этаж 41, ком.6'),
                    ('9714053621', '507401001', '', 'ООО ""РВБ""', 643, '', 'д. Коледино', 'Индустриальный Парк Коледино, д.6, стр.1');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "companies");

            migrationBuilder.DropPrimaryKey(
                name: "PK_countries",
                table: "countries");

            migrationBuilder.RenameTable(
                name: "countries",
                newName: "country_codes");

            migrationBuilder.AddPrimaryKey(
                name: "PK_country_codes",
                table: "country_codes",
                column: "iso_numeric");
        }
    }
}

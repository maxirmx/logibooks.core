using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_9_2_Keywords_NxN : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                      name: "key_word_feacn_codes",
                      columns: table => new
                      {
                          key_word_id = table.Column<int>(type: "integer", nullable: false),
                          feacn_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                      },
                      constraints: table =>
                      {
                          table.PrimaryKey("PK_key_word_feacn_codes", x => new { x.key_word_id, x.feacn_code });
                          table.ForeignKey(
                              name: "FK_key_word_feacn_codes_key_words_key_word_id",
                              column: x => x.key_word_id,
                              principalTable: "key_words",
                              principalColumn: "id",
                              onDelete: ReferentialAction.Cascade);
                      });

            // Migrate existing data from key_words.feacn_code to the junction table
            migrationBuilder.Sql(@"
                INSERT INTO key_word_feacn_codes (key_word_id, feacn_code)
                SELECT id, feacn_code 
                FROM key_words 
                WHERE feacn_code IS NOT NULL AND feacn_code != ''
            ");

            migrationBuilder.DropColumn(
                name: "feacn_code",
                table: "key_words");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                    name: "feacn_code",
                    table: "key_words",
                    type: "text",
                    nullable: false,
                    defaultValue: "");

            // Migrate data back (taking only the first FeacnCode for each KeyWord)
            migrationBuilder.Sql(@"
                UPDATE key_words 
                SET feacn_code = kwfc.feacn_code
                FROM (
                    SELECT DISTINCT ON (key_word_id) key_word_id, feacn_code
                    FROM key_word_feacn_codes
                    ORDER BY key_word_id, feacn_code
                ) kwfc
                WHERE key_words.id = kwfc.key_word_id
            ");

            migrationBuilder.DropTable(
                name: "key_word_feacn_codes");
        }
    }
}

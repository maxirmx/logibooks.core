using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_A_3_Insert_Before_And_After : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename table from base_order_feacn_prefixes to base_parcel_feacn_prefixes
            migrationBuilder.RenameTable(
                name: "base_order_feacn_prefixes",
                newName: "base_parcel_feacn_prefixes");

            // Rename the index
            migrationBuilder.RenameIndex(
                name: "IX_base_order_feacn_prefixes_feacn_prefix_id",
                table: "base_parcel_feacn_prefixes",
                newName: "IX_base_parcel_feacn_prefixes_feacn_prefix_id");

            // Rename the column from base_order_id to base_parcel_id
            migrationBuilder.RenameColumn(
                name: "base_order_id",
                table: "base_parcel_feacn_prefixes",
                newName: "base_parcel_id");

            // Update the foreign key constraint
            migrationBuilder.DropForeignKey(
                name: "FK_base_order_feacn_prefixes_base_parcels_base_order_id",
                table: "base_parcel_feacn_prefixes");

            migrationBuilder.AddForeignKey(
                name: "FK_base_parcel_feacn_prefixes_base_parcels_base_parcel_id",
                table: "base_parcel_feacn_prefixes",
                column: "base_parcel_id",
                principalTable: "base_parcels",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateTable(
                name: "feacn_insert_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    insert_before = table.Column<string>(type: "text", nullable: true),
                    insert_after = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feacn_insert_items", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_insert_items_code",
                table: "feacn_insert_items",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feacn_insert_items");

            // Reverse the foreign key constraint
            migrationBuilder.DropForeignKey(
                name: "FK_base_parcel_feacn_prefixes_base_parcels_base_parcel_id",
                table: "base_parcel_feacn_prefixes");

            // Rename the column back from base_parcel_id to base_order_id
            migrationBuilder.RenameColumn(
                name: "base_parcel_id",
                table: "base_parcel_feacn_prefixes",
                newName: "base_order_id");

            // Rename the index back
            migrationBuilder.RenameIndex(
                name: "IX_base_parcel_feacn_prefixes_feacn_prefix_id",
                table: "base_parcel_feacn_prefixes",
                newName: "IX_base_order_feacn_prefixes_feacn_prefix_id");

            // Rename table back from base_parcel_feacn_prefixes to base_order_feacn_prefixes
            migrationBuilder.RenameTable(
                name: "base_parcel_feacn_prefixes",
                newName: "base_order_feacn_prefixes");

            // Restore the original foreign key constraint
            migrationBuilder.AddForeignKey(
                name: "FK_base_order_feacn_prefixes_base_parcels_base_order_id",
                table: "base_order_feacn_prefixes",
                column: "base_order_id",
                principalTable: "base_parcels",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

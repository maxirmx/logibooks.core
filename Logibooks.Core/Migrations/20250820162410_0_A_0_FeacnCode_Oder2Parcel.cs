using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_A_0_FeacnCode_Oder2Parcel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_base_orders_tn_ved",
                table: "base_orders",
                newName: "IX_base_parcels_tn_ved");

            migrationBuilder.RenameIndex(
                name: "IX_base_orders_registerid_checkstatusid_id",
                table: "base_orders",
                newName: "IX_base_parcels_registerid_checkstatusid_id");

            migrationBuilder.RenameTable(
                name: "base_orders",
                newName: "base_parcels");

            migrationBuilder.AlterColumn<string>(
                name: "code",
                table: "feacn_prefixes",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "code",
                table: "feacn_prefix_exceptions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateTable(
                name: "feacn_codes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    code_ex = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    normalized = table.Column<string>(type: "text", nullable: false),
                    from_date = table.Column<DateOnly>(type: "date", nullable: true),
                    to_date = table.Column<DateOnly>(type: "date", nullable: true),
                    old_name = table.Column<string>(type: "text", nullable: true),
                    old_name_to_date = table.Column<DateOnly>(type: "date", nullable: true),
                    parent_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feacn_codes", x => x.id);
                    table.ForeignKey(
                        name: "FK_feacn_codes_feacn_codes_parent_id",
                        column: x => x.parent_id,
                        principalTable: "feacn_codes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_feacn_codes_code",
                table: "feacn_codes",
                column: "code");

            migrationBuilder.CreateIndex(
                name: "IX_feacn_codes_parent_id",
                table: "feacn_codes",
                column: "parent_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feacn_codes");

            migrationBuilder.RenameTable(
                name: "base_parcels",
                newName: "base_orders");

            migrationBuilder.RenameIndex(
                name: "IX_base_parcels_tn_ved",
                table: "base_orders",
                newName: "IX_base_orders_tn_ved");

            migrationBuilder.RenameIndex(
                name: "IX_base_parcels_registerid_checkstatusid_id",
                table: "base_orders",
                newName: "IX_base_orders_registerid_checkstatusid_id");

            migrationBuilder.AlterColumn<string>(
                name: "code",
                table: "feacn_prefixes",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "code",
                table: "feacn_prefix_exceptions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);
        }
    }
}

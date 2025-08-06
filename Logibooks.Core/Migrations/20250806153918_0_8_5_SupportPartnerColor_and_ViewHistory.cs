using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_8_5_SupportPartnerColor_and_ViewHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "partner_color",
                table: "base_orders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "parcel_views",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    base_order_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parcel_views", x => x.id);
                    table.ForeignKey(
                        name: "FK_parcel_views_base_orders_base_order_id",
                        column: x => x.base_order_id,
                        principalTable: "base_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_parcel_views_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "check_statuses",
                columns: new[] { "id", "title" },
                values: new object[] { 200, "Отмечено партнёром" });

            migrationBuilder.UpdateData(
                table: "feacn_orders",
                keyColumn: "id",
                keyValue: 3,
                column: "comment",
                value: "Операции in отношении драгоценных металлов и драгоценных камней");

            migrationBuilder.CreateIndex(
                name: "IX_parcel_views_baseorderid_userid",
                table: "parcel_views",
                columns: new[] { "base_order_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_parcel_views_userid_timestamp",
                table: "parcel_views",
                columns: new[] { "user_id", "timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parcel_views");

            migrationBuilder.DeleteData(
                table: "check_statuses",
                keyColumn: "id",
                keyValue: 200);

            migrationBuilder.DropColumn(
                name: "partner_color",
                table: "base_orders");

            migrationBuilder.UpdateData(
                table: "feacn_orders",
                keyColumn: "id",
                keyValue: 3,
                column: "comment",
                value: "Операции в отношении драгоценных металлов и драгоценных камней");
        }
    }
}

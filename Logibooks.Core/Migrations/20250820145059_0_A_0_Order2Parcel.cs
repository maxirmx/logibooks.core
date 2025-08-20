using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_A_0_Order2Parcel : Migration
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}

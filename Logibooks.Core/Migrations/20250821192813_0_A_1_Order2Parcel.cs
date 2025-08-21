using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_A_1_Order2Parcel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_wbr_orders_shk",
                table: "wbr_orders",
                newName: "IX_wbr_parcels_shk");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_ozon_orders_posting_number') THEN
                        CREATE INDEX IX_ozon_orders_posting_number ON ozon_orders (posting_number);
                    END IF;
                END $$;
            ");

            migrationBuilder.RenameIndex(
                name: "IX_ozon_orders_posting_number",
                table: "ozon_orders",
                newName: "IX_ozon_parcels_posting_number");

            migrationBuilder.RenameTable(
                name: "wbr_orders",
                newName: "wbr_parcels");

            migrationBuilder.RenameTable(
                name: "ozon_orders",
                newName: "ozon_parcels");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "ozon_parcels",
                newName: "ozon_orders");

            migrationBuilder.RenameIndex(
                name: "IX_ozon_parcels_posting_number",
                table: "ozon_orders",
                newName: "IX_ozon_orders_posting_number");

            migrationBuilder.RenameTable(
                name: "wbr_parcels",
                newName: "wbr_orders");

            migrationBuilder.RenameIndex(
                name: "IX_wbr_parcels_shk",
                table: "wbr_orders",
                newName: "IX_wbr_orders_shk");
        }
    }
}

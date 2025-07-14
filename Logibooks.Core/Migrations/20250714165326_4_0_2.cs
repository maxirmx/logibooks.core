using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class _4_0_2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_order_number",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_register_id_order_number",
                table: "orders");

            migrationBuilder.CreateIndex(
                name: "IX_orders_register_id",
                table: "orders",
                column: "register_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_shk",
                table: "orders",
                column: "shk");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_register_id",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_shk",
                table: "orders");

            migrationBuilder.CreateIndex(
                name: "IX_orders_order_number",
                table: "orders",
                column: "order_number");

            migrationBuilder.CreateIndex(
                name: "IX_orders_register_id_order_number",
                table: "orders",
                columns: new[] { "register_id", "order_number" });
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_orders_register_status",
                table: "orders",
                columns: new[] { "register_id", "status_id" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_register_tnved",
                table: "orders",
                columns: new[] { "register_id", "tn_ved" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_register_status",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_register_tnved",
                table: "orders");
        }
    }
}

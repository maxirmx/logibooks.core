using Microsoft.EntityFrameworkCore.Migrations;

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class OrderHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "order_type",
                table: "orders",
                type: "integer",
                nullable: false,
                computedColumnSql: "(SELECT company_id FROM registers r WHERE r.id = register_id)",
                stored: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "order_type",
                table: "orders");
        }
    }
}

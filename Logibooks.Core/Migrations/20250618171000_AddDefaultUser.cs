using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "first_name", "last_name", "patronimic", "email", "password" },
                values: new object[] { 1, "Maxim", "Samsonov", "", "maxirmx@sw.consulting", "$2b$12$eOXzlwFzyGVERe0sNwFeJO5XnvwsjloUpL4o2AIQ8254RT88MnsDi" }
            );

            migrationBuilder.InsertData(
                table: "user_roles",
                columns: new[] { "user_id", "role_id" },
                values: new object[] { 1, 2 }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "user_roles",
                keyColumns: new[] { "user_id", "role_id" },
                keyValues: new object[] { 1, 2 }
            );

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "id",
                keyValue: 1
            );
        }
    }
}

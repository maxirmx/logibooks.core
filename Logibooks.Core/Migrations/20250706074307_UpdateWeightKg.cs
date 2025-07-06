using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class UpdateWeightKg : Migration
    {
        /// <inheritdoc />
       protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert text to numeric with error handling, preserving column order
            migrationBuilder.Sql(@"
                ALTER TABLE orders 
                ALTER COLUMN weight_kg TYPE numeric(10,3) 
                USING CASE 
                    WHEN weight_kg ~ '^[0-9]+\.?[0-9]*$' THEN weight_kg::numeric(10,3)
                    ELSE 0
                END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "weight_kg",
                table: "orders",
                type: "text",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,3)",
                oldNullable: true);
        }
    }
}

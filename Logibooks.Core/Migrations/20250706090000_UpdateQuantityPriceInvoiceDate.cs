using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class UpdateQuantityPriceInvoiceDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE orders
                ALTER COLUMN quantity TYPE numeric(10,3)
                    USING CASE
                        WHEN quantity ~ '^[0-9]+\.?[0-9]*$' THEN quantity::numeric(10,3)
                        ELSE 0
                    END,
                ALTER COLUMN unit_price TYPE numeric(10,3)
                    USING CASE
                        WHEN unit_price ~ '^[0-9]+\.?[0-9]*$' THEN unit_price::numeric(10,3)
                        ELSE 0
                    END,
                ALTER COLUMN invoice_date TYPE date
                    USING CASE
                        WHEN invoice_date ~ '^\\d{4}-\\d{2}-\\d{2}$' THEN invoice_date::date
                        ELSE NULL
                    END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "quantity",
                table: "orders",
                type: "text",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,3)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "unit_price",
                table: "orders",
                type: "text",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,3)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "invoice_date",
                table: "orders",
                type: "text",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);
        }
    }
}

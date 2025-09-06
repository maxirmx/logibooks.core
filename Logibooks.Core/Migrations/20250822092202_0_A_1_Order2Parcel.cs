// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System;
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
            // The base_orders table was already renamed to base_parcels in a previous migration
            // We only need to rename the ozon_orders and wbr_orders tables
            
            migrationBuilder.RenameTable(
                name: "ozon_orders",
                newName: "ozon_parcels");

            migrationBuilder.RenameTable(
                name: "wbr_orders",
                newName: "wbr_parcels");

            // Rename indexes for the tables that are being renamed
            migrationBuilder.RenameIndex(
                name: "IX_ozon_orders_posting_number",
                table: "ozon_parcels",
                newName: "IX_ozon_parcels_posting_number");

            migrationBuilder.RenameIndex(
                name: "IX_wbr_orders_shk",
                table: "wbr_parcels",
                newName: "IX_wbr_parcels_shk");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rename indexes back to original names
            migrationBuilder.RenameIndex(
                name: "IX_ozon_parcels_posting_number",
                table: "ozon_parcels",
                newName: "IX_ozon_orders_posting_number");

            migrationBuilder.RenameIndex(
                name: "IX_wbr_parcels_shk",
                table: "wbr_parcels",
                newName: "IX_wbr_orders_shk");

            // Rename tables back to original names
            migrationBuilder.RenameTable(
                name: "ozon_parcels",
                newName: "ozon_orders");

            migrationBuilder.RenameTable(
                name: "wbr_parcels",
                newName: "wbr_orders");
        }
    }
}

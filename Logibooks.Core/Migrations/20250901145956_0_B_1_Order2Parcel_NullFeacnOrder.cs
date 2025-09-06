// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logibooks.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_B_1_Order2Parcel_NullFeacnOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_parcel_views_base_orders_base_order_id",
                table: "parcel_views");

            migrationBuilder.RenameColumn(
                name: "base_order_id",
                table: "parcel_views",
                newName: "base_parcel_id");

            migrationBuilder.RenameIndex(
                name: "IX_parcel_views_baseorderid_userid_dtime",
                table: "parcel_views",
                newName: "IX_parcel_views_baseparcelid_userid_dtime");

            migrationBuilder.AlterColumn<int>(
                name: "feacn_order_id",
                table: "feacn_prefixes",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_parcel_views_base_parcels_base_parcel_id",
                table: "parcel_views",
                column: "base_parcel_id",
                principalTable: "base_parcels",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_parcel_views_base_parcels_base_parcel_id",
                table: "parcel_views");

            migrationBuilder.RenameColumn(
                name: "base_parcel_id",
                table: "parcel_views",
                newName: "base_order_id");

            migrationBuilder.RenameIndex(
                name: "IX_parcel_views_baseparcelid_userid_dtime",
                table: "parcel_views",
                newName: "IX_parcel_views_baseorderid_userid_dtime");

            migrationBuilder.AlterColumn<int>(
                name: "feacn_order_id",
                table: "feacn_prefixes",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_parcel_views_base_orders_base_order_id",
                table: "parcel_views",
                column: "base_order_id",
                principalTable: "base_parcels",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

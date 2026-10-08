using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGY.CheckIn.Migrations
{
    /// <inheritdoc />
    public partial class AddKidsCheckOut : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckOutCode",
                table: "CheckIns",
                type: "TEXT",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckedOutAt",
                table: "CheckIns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_CheckOutCode",
                table: "CheckIns",
                column: "CheckOutCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CheckIns_CheckOutCode",
                table: "CheckIns");

            migrationBuilder.DropColumn(
                name: "CheckOutCode",
                table: "CheckIns");

            migrationBuilder.DropColumn(
                name: "CheckedOutAt",
                table: "CheckIns");
        }
    }
}

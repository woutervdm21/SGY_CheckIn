using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGY.CheckIn.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Youths",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Surname = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CellNo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Grade = table.Column<int>(type: "INTEGER", nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ParentName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ParentSurname = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ParentCellNo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Youths", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CheckIns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    YouthId = table.Column<int>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckIns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckIns_Youths_YouthId",
                        column: x => x.YouthId,
                        principalTable: "Youths",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_Timestamp",
                table: "CheckIns",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_YouthId",
                table: "CheckIns",
                column: "YouthId");

            migrationBuilder.CreateIndex(
                name: "IX_Youths_Surname_Name",
                table: "Youths",
                columns: new[] { "Surname", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckIns");

            migrationBuilder.DropTable(
                name: "Youths");
        }
    }
}

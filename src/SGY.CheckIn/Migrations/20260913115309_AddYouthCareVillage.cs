using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGY.CheckIn.Migrations
{
    /// <inheritdoc />
    public partial class AddYouthCareVillage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InCareVillage",
                table: "Youths",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InCareVillage",
                table: "Youths");
        }
    }
}

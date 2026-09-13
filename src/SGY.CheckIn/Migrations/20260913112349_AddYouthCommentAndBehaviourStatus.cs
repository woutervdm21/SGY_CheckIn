using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGY.CheckIn.Migrations
{
    /// <inheritdoc />
    public partial class AddYouthCommentAndBehaviourStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BehaviourStatus",
                table: "Youths",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "Youths",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BehaviourStatus",
                table: "Youths");

            migrationBuilder.DropColumn(
                name: "Comment",
                table: "Youths");
        }
    }
}

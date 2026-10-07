using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGY.CheckIn.Migrations
{
    /// <inheritdoc />
    public partial class AddGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Youths_Surname_Name",
                table: "Youths");

            migrationBuilder.AlterColumn<string>(
                name: "ParentSurname",
                table: "Youths",
                type: "TEXT",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "ParentName",
                table: "Youths",
                type: "TEXT",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "ParentCellNo",
                table: "Youths",
                type: "TEXT",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<int>(
                name: "Grade",
                table: "Youths",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "CellNo",
                table: "Youths",
                type: "TEXT",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 20);

            // Everyone registered before groups existed is in Youth (Group.Youth = 2).
            migrationBuilder.AddColumn<int>(
                name: "Group",
                table: "Youths",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<string>(
                name: "Medical",
                table: "Youths",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Youths_Group_Surname_Name",
                table: "Youths",
                columns: new[] { "Group", "Surname", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversible in place: Kids and Young Adults rows hold NULL grades, cell
            // numbers and parents that the old NOT NULL columns can't take, and dropping
            // Group would merge everyone into Youth. Roll back by restoring the backup made
            // before updating (README: Restoring a backup) instead.
            throw new NotSupportedException("AddGroups can't be undone in place. Restore the backup made before updating.");
        }
    }
}

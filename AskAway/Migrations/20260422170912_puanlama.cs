using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AskAway.Migrations
{
    /// <inheritdoc />
    public partial class puanlama : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnswerOrder",
                table: "Players",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnswerOrder",
                table: "Players");
        }
    }
}

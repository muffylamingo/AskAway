using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AskAway.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionTypeAndRound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentRound",
                table: "Rooms",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QuestionType",
                table: "Questions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentRound",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "QuestionType",
                table: "Questions");
        }
    }
}

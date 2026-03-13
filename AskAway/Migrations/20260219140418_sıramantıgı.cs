using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AskAway.Migrations
{
    /// <inheritdoc />
    public partial class sıramantıgı : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentKingIndex",
                table: "Rooms",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentKingIndex",
                table: "Rooms");
        }
    }
}

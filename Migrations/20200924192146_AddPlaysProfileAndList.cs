using Microsoft.EntityFrameworkCore.Migrations;

namespace MCloudServer.Migrations
{
    public partial class AddPlaysProfileAndList : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "audioprofile",
                table: "plays",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "listid",
                table: "plays",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_plays_audioprofile",
                table: "plays",
                column: "audioprofile");

            migrationBuilder.CreateIndex(
                name: "IX_plays_listid",
                table: "plays",
                column: "listid");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_plays_audioprofile",
                table: "plays");

            migrationBuilder.DropIndex(
                name: "IX_plays_listid",
                table: "plays");

            migrationBuilder.DropColumn(
                name: "audioprofile",
                table: "plays");

            migrationBuilder.DropColumn(
                name: "listid",
                table: "plays");
        }
    }
}

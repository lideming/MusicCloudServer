using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MCloudServer.Migrations
{
    public partial class file_sha256 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "sha256",
                table: "file",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_file_sha256",
                table: "file",
                column: "sha256");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_file_sha256",
                table: "file");

            migrationBuilder.DropColumn(
                name: "sha256",
                table: "file");
        }
    }
}

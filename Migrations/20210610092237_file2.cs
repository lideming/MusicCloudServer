using Microsoft.EntityFrameworkCore.Migrations;

namespace MCloudServer.Migrations
{
    public partial class file2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "files",
                table: "tracks");

            migrationBuilder.DropColumn(
                name: "size",
                table: "tracks");

            migrationBuilder.DropColumn(
                name: "url",
                table: "tracks");

            migrationBuilder.DropColumn(
                name: "Size",
                table: "trackFile");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "files",
                table: "tracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "size",
                table: "tracks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "url",
                table: "tracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Size",
                table: "trackFile",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);
        }
    }
}

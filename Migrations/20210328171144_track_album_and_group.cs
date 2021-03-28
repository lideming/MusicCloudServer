using Microsoft.EntityFrameworkCore.Migrations;

namespace MCloudServer.Migrations
{
    public partial class track_album_and_group : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "album",
                table: "tracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "albumArtist",
                table: "tracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "groupId",
                table: "tracks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "album",
                table: "tracks");

            migrationBuilder.DropColumn(
                name: "albumArtist",
                table: "tracks");

            migrationBuilder.DropColumn(
                name: "groupId",
                table: "tracks");
        }
    }
}

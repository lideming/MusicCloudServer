using Microsoft.EntityFrameworkCore.Migrations;

namespace MCloudServer.Migrations
{
    public partial class track_pic : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "pictureFileId",
                table: "tracks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tracks_pictureFileId",
                table: "tracks",
                column: "pictureFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_tracks_file_pictureFileId",
                table: "tracks",
                column: "pictureFileId",
                principalTable: "file",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tracks_file_pictureFileId",
                table: "tracks");

            migrationBuilder.DropIndex(
                name: "IX_tracks_pictureFileId",
                table: "tracks");

            migrationBuilder.DropColumn(
                name: "pictureFileId",
                table: "tracks");
        }
    }
}

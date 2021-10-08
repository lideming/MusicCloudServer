using Microsoft.EntityFrameworkCore.Migrations;

namespace MCloudServer.Migrations
{
    public partial class track_thumbpic : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "thumbPictureFileId",
                table: "tracks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tracks_thumbPictureFileId",
                table: "tracks",
                column: "thumbPictureFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_tracks_file_thumbPictureFileId",
                table: "tracks",
                column: "thumbPictureFileId",
                principalTable: "file",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tracks_file_thumbPictureFileId",
                table: "tracks");

            migrationBuilder.DropIndex(
                name: "IX_tracks_thumbPictureFileId",
                table: "tracks");

            migrationBuilder.DropColumn(
                name: "thumbPictureFileId",
                table: "tracks");
        }
    }
}

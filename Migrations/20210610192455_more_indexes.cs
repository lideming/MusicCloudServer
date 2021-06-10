using Microsoft.EntityFrameworkCore.Migrations;

namespace MCloudServer.Migrations
{
    public partial class more_indexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_tracks_owner",
                table: "tracks",
                column: "owner");

            migrationBuilder.AddForeignKey(
                name: "FK_tracks_users_owner",
                table: "tracks",
                column: "owner",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tracks_users_owner",
                table: "tracks");

            migrationBuilder.DropIndex(
                name: "IX_tracks_owner",
                table: "tracks");
        }
    }
}

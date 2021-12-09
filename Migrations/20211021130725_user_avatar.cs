using Microsoft.EntityFrameworkCore.Migrations;

namespace MCloudServer.Migrations
{
    public partial class user_avatar : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "avatarId",
                table: "users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_avatarId",
                table: "users",
                column: "avatarId");

            migrationBuilder.AddForeignKey(
                name: "FK_users_file_avatarId",
                table: "users",
                column: "avatarId",
                principalTable: "file",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_file_avatarId",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_avatarId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "avatarId",
                table: "users");
        }
    }
}

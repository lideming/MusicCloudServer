using Microsoft.EntityFrameworkCore.Migrations;

namespace MCloudServer.Migrations
{
    public partial class more_constraints : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_lists_owner",
                table: "lists",
                column: "owner");

            migrationBuilder.CreateIndex(
                name: "IX_comments_uid",
                table: "comments",
                column: "uid");

            migrationBuilder.AddForeignKey(
                name: "FK_comments_users_uid",
                table: "comments",
                column: "uid",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_lists_users_owner",
                table: "lists",
                column: "owner",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_comments_users_uid",
                table: "comments");

            migrationBuilder.DropForeignKey(
                name: "FK_lists_users_owner",
                table: "lists");

            migrationBuilder.DropIndex(
                name: "IX_lists_owner",
                table: "lists");

            migrationBuilder.DropIndex(
                name: "IX_comments_uid",
                table: "comments");
        }
    }
}

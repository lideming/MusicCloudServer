using Microsoft.EntityFrameworkCore.Migrations;

namespace MCloudServer.Migrations
{
    public partial class list_picurl : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "picId",
                table: "lists",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_lists_picId",
                table: "lists",
                column: "picId");

            migrationBuilder.AddForeignKey(
                name: "FK_lists_file_picId",
                table: "lists",
                column: "picId",
                principalTable: "file",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_lists_file_picId",
                table: "lists");

            migrationBuilder.DropIndex(
                name: "IX_lists_picId",
                table: "lists");

            migrationBuilder.DropColumn(
                name: "picId",
                table: "lists");
        }
    }
}

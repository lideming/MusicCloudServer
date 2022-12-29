using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MCloudServer.Migrations
{
    public partial class user_store : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "userStore",
                columns: table => new
                {
                    userId = table.Column<int>(type: "INTEGER", nullable: false),
                    key = table.Column<string>(type: "TEXT", nullable: false),
                    value = table.Column<byte[]>(type: "BLOB", nullable: true),
                    visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    revision = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userStore", x => new { x.userId, x.key });
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "userStore");
        }
    }
}

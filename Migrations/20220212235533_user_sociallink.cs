using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MCloudServer.Migrations
{
    public partial class user_sociallink : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "userSocialLinks",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    userId = table.Column<int>(type: "INTEGER", nullable: false),
                    provider = table.Column<string>(type: "TEXT", nullable: true),
                    accessToken = table.Column<string>(type: "TEXT", nullable: true),
                    refreshToken = table.Column<string>(type: "TEXT", nullable: true),
                    idFromProvider = table.Column<string>(type: "TEXT", nullable: true),
                    nameFromProvider = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userSocialLinks", x => x.id);
                    table.ForeignKey(
                        name: "FK_userSocialLinks_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_userSocialLinks_provider_idFromProvider",
                table: "userSocialLinks",
                columns: new[] { "provider", "idFromProvider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_userSocialLinks_userId",
                table: "userSocialLinks",
                column: "userId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "userSocialLinks");
        }
    }
}

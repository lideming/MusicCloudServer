using Microsoft.EntityFrameworkCore.Migrations;

namespace MCloudServer.Migrations
{
    public partial class file : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "fileRecordId",
                table: "tracks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "file",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    size = table.Column<long>(type: "INTEGER", nullable: false),
                    path = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trackFile",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConvName = table.Column<string>(type: "TEXT", nullable: true),
                    Format = table.Column<string>(type: "TEXT", nullable: true),
                    Bitrate = table.Column<int>(type: "INTEGER", nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    TrackID = table.Column<int>(type: "INTEGER", nullable: false),
                    FileID = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trackFile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trackFile_file_FileID",
                        column: x => x.FileID,
                        principalTable: "file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_trackFile_tracks_TrackID",
                        column: x => x.TrackID,
                        principalTable: "tracks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tracks_fileRecordId",
                table: "tracks",
                column: "fileRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_trackFile_FileID",
                table: "trackFile",
                column: "FileID");

            migrationBuilder.CreateIndex(
                name: "IX_trackFile_TrackID",
                table: "trackFile",
                column: "TrackID");

            migrationBuilder.AddForeignKey(
                name: "FK_tracks_file_fileRecordId",
                table: "tracks",
                column: "fileRecordId",
                principalTable: "file",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tracks_file_fileRecordId",
                table: "tracks");

            migrationBuilder.DropTable(
                name: "trackFile");

            migrationBuilder.DropTable(
                name: "file");

            migrationBuilder.DropIndex(
                name: "IX_tracks_fileRecordId",
                table: "tracks");

            migrationBuilder.DropColumn(
                name: "fileRecordId",
                table: "tracks");
        }
    }
}

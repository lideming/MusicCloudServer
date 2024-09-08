using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using MCloudServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

class MigrationService
{
    public static async Task AppMigrate(AppService appService, DbCtx dbctx, ILogger logger, FileService fileService)
    {
        dbctx.Database.BeginTransaction();
        var val = await dbctx.GetConfig("appver");
        var origVal = val;
        if (new[] { "1", "2", "3", "4" }.Contains(val))
        {
            throw new Exception(
                $"Use \"migration-file\" branch to migrate from \"{val}\" database."
            );
        }
        if (val == null)
        {
            val = "5";
        }
        if (val == "4")
        {
            val = "5";
            logger.LogInformation("Migration v5: creating thumbnail pictures...");
            var count = 0;
            var tracksWithPic = dbctx
                .Tracks.Include(t => t.pictureFile)
                .Where(t => t.pictureFileId != null);
            foreach (var track in tracksWithPic)
            {
                var pathSmall = track.pictureFile.path + ".128.jpg";
                var fsPathSmall = appService.ResolveStoragePath(pathSmall);
                using (
                    var origPic = Image.Load(appService.ResolveStoragePath(track.pictureFile.path))
                )
                {
                    origPic.Mutate(p => p.Resize(128, 0));
                    origPic.SaveAsJpeg(fsPathSmall);
                }
                track.thumbPictureFile = new StoredFile
                {
                    path = pathSmall,
                    size = new FileInfo(fsPathSmall).Length
                };
                if (++count % 100 == 0)
                {
                    logger.LogInformation("Created {count} thumbnail pictures.", count);
                    await dbctx.SaveChangesAsync();
                }
            }
            logger.LogInformation("Created {count} thumbnail pictures.", count);
            await dbctx.SaveChangesAsync();
            logger.LogInformation("Migration v5: update list picId for thumbnails...");
            count = 0;
            foreach (var list in dbctx.Lists)
            {
                var firstId = list.trackids.FirstOrDefault();
                if (firstId != 0)
                {
                    list.picId = (
                        await dbctx
                            .Tracks.Where(t =>
                                t.id == firstId
                                && (t.owner == list.owner || t.visibility == Visibility.Public)
                            )
                            .FirstOrDefaultAsync()
                    )?.thumbPictureFileId;
                }
                if (++count % 100 == 0)
                {
                    logger.LogInformation("Updated {count} lists for pic.", count);
                    await dbctx.SaveChangesAsync();
                }
            }
            logger.LogInformation("Updated {count} lists for pic.", count);
            await dbctx.SaveChangesAsync();
            logger.LogInformation("Migration v5: done.");
        }
        if (val == "5")
        {
            logger.LogInformation("Migration v6: Calculating SHA256 hashes for files...");
            int count = 0;
            foreach (var file in dbctx.Files)
            {
                if (file.sha256 != null)
                {
                    continue;
                }
                await fileService.FillHash(file);
                if (++count % 10 == 0)
                {
                    logger.LogInformation("Processed {count} files.", count);
                    await dbctx.SaveChangesAsync();
                }
            }
            logger.LogInformation("Processed {count} files in total.", count);
            await dbctx.SaveChangesAsync();
            logger.LogInformation("Migration v6: SHA256 hash calculation completed.");
            val = "6";
        }
        if (val != "6")
        {
            throw new Exception($"Unsupported appver \"{val}\"");
        }
        if (val != origVal)
        {
            logger.LogInformation("appver changed from {orig} to {val}", origVal, val);
            await dbctx.SetConfig("appver", val);
        }
        dbctx.Database.CommitTransaction();
    }
}

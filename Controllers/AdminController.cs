using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MCloudServer;
using System.IO;
using System.Text;
using System.Text.Json;

namespace MCloudServer.Controllers
{
    [Route("api/admin")]
    [ApiController]
    public class AdminController : MyControllerBase
    {
        public AdminController(DbCtx context) : base(context)
        {
        }

        [HttpPost("job/{arg}")]
        public async Task<ActionResult> RunJob(string arg)
        {
            var user = await GetLoginUser();
            if (user == null || user.role != UserRole.SuperAdmin) return GetErrorResult("permission_denied");

            var result = "unknown job";
            var sb = new StringBuilder();

            try {
                if (arg == "tracks_migration") {
                    result = "ok";
                    var listOk = new List<int>();
                    var listFail = new List<int>();
                    await _context.Tracks
                        .Where(t => t.artist == "Unknown" || !t.url.Contains(".") || t.length == 0)
                        .ForEachAsync((t) => {
                        if (t.TryGetStoragePath(_app, out var path)) {
                            try {
                                if (!t.url.Contains(".")) {
                                    var guessedExt = t.artist == "Unknown" ? ".m4a" : ".mp3";
                                    System.IO.File.Move(path, path + guessedExt);
                                    t.url += guessedExt;
                                } 
                                t.ReadTrackInfoFromFile(_app);
                                listOk.Add(t.id);
                            } catch (Exception) {
                                listFail.Add(t.id);
                            }
                        }
                    });
                    await _context.SaveChangesAsync();
                    sb.Append("ok: ").Append(string.Join(" ", listOk)).AppendLine();
                    sb.Append("fail: ").Append(string.Join(" ", listFail));
                } else if (arg == "sql") {
                    result = "ok";
                    var sql = await new StreamReader(this.HttpContext.Request.Body, Encoding.UTF8).ReadToEndAsync();
                    sb.Append("affected: ").Append(await _context.Database.ExecuteSqlRawAsync(sql));
                } else if (arg == "trackfiles_cleanup") {
                    result = "ok";
                    var dict = _app.Config.Converters.ToDictionary(c => c.Name);
                    await _context.Tracks.ForEachAsync(t => {
                        if (t.files?.Count > 0) {
                            for (int i = t.files.Count - 1; i >= 0; i--)
                            {
                                TrackFile item = t.files[i];
                                if (dict.ContainsKey(item.ConvName) == false) {
                                    t.files.RemoveAt(i);
                                    var url = TrackFileVM.GetUrlWithConv(t.url, item.ConvName);
                                    System.IO.File.Delete(_app.Config.ResolveStoragePath(url));
                                    if (_app.StorageService.Mode != StorageMode.Direct) {
                                        _app.StorageService.DeleteFile(_app.Config.GetStoragePath(url));
                                    }
                                    sb.Append(t.id).Append("-").Append(item.ConvName).Append(" ");
                                }
                            }
                        }
                    });
                    await _context.SaveChangesAsync();
                }
            } catch (Exception ex) {
                result = "error";
                sb.Append("\nerror:\n").Append(ex.ToString());
            }

            return new JsonResult(new {
                result,
                detail = sb.ToString()
            });
        }
    }
}

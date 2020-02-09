﻿using System;
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
                if (arg == "fill_tracks_info") {
                    result = "ok";
                    var listOk = new List<int>();
                    var listFail = new List<int>();
                    await _context.Tracks.Where(t => t.artist == "Unknown").ForEachAsync((t) => {
                        if (t.url.StartsWith("storage/")) {
                            try {
                                t.ReadTrackInfoFromFile(_context.MCloudConfig);
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
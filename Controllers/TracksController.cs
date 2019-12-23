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
    [Route("api/tracks")]
    [ApiController]
    public class TracksController : MyControllerBase
    {
        public TracksController(DbCtx context) : base(context)
        {
        }

        // [Warning! New Binary Format!]
        // When clients upload a new track:
        //
        // POST /api/tracks/newfile with the request body in the following format:
        //
        //    MIME: application/x-mcloud-upload
        //
        //    [BLOCK(track info in json)] | [BLOCK(raw track file content)]
        //
        //    where [BLOCK(DATA)] := [length of DATA in 8 hex digits in ASCII] | "\r\n"
        //                         | [DATA] | "\r\n"
        //
        // When finished receiving, the server will response with the new Track entity.

        [HttpPost("newfile")]
        public async Task<ActionResult> PostNewFile()
        {
            if (Request.ContentType != "application/x-mcloud-upload")
                return GetErrorResult("bad_content_type");

            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            var stream = Request.Body;

            // Read the Track json
            var jsonBytesLen = await ReadBlockLength(stream);
            if (jsonBytesLen < 0 || jsonBytesLen > 1000)
                return GetErrorResult("json_len_out_of_range");
            var jsonStr = await ReadString(stream, jsonBytesLen);
            var track = JsonSerializer.Deserialize<Track>(jsonStr);

            // Now start reading the file
            var fileLength = await ReadBlockLength(stream);
            if (fileLength < 0 || fileLength > 100 * 1024 * 1024)
                return GetErrorResult("file_len_out_of_range");

            // Read the stream into a temp file
            var tmpdir = Path.Combine(_context.MCloudConfig.StorageDir, "tracks-inprogress");
            Directory.CreateDirectory(tmpdir);

            var filename = Guid.NewGuid().ToString("D");
            var tmpfile = Path.Combine(tmpdir, filename);

            try
            {
                using (var fs = System.IO.File.Create(tmpfile))
                {
                    var buffer = new byte[64 * 1024];
                    for (int read, cur = 0; cur < fileLength; cur += read)
                    {
                        read = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (read == 0) throw new Exception("Unexpected EOF");
                        await fs.WriteAsync(buffer, 0, read);
                    }
                    await stream.CopyToAsync(fs);
                }
            }
            catch (Exception)
            {
                System.IO.File.Delete(tmpfile);
                throw;
            }

            // Move the temp file to storage "tracks" directory
            var tracksdir = Path.Combine(_context.MCloudConfig.StorageDir, "tracks");
            Directory.CreateDirectory(tracksdir);

            System.IO.File.Move(tmpfile, Path.Combine(tracksdir, filename));

            // Fill the track info, and complete.
            track.url = "storage/tracks/" + filename;
            track.owner = user.id;
            _context.Tracks.Add(track);
            await _context.SaveChangesAsync();

            return new JsonResult(track) { StatusCode = 201 };
        }

        private async Task<int> ReadBlockLength(Stream stream)
        {
            var str = await ReadString(stream, 10);
            if (str.AsSpan(8).CompareTo("\r\n", StringComparison.Ordinal) != 0) throw new Exception();
            return ParseHex(str.AsSpan(0, 8));
        }

        private int ParseHex(ReadOnlySpan<char> str)
        {
            int r = 0;
            const string hex = "0123456789abcdefABCDEF";
            for (int i = 0; i < str.Length; i++)
            {
                var digit = hex.IndexOf(str[i]);
                if (digit < 0) throw new ArgumentException($"Unexpected char '{str[i]}' parsing hex number");
                if (digit >= 16) digit -= 6;
                r = (r << 4) + digit;
            }
            return r;
        }

        private async Task<string> ReadString(Stream stream, int len)
        {
            var buf = new byte[len];
            if (await stream.ReadAsync(buf, 0, len) < len)
            {
                throw new IOException("Unexpected EOF");
            }
            // Note that the length of decoded string might be smaller than bytes length.
            return Encoding.UTF8.GetString(buf);
        }
    }
}
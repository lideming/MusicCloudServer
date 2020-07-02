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

        [HttpPut("{id}")]
        public async Task<ActionResult> PutTrack(int id, TrackVM vm, int? ifVersion)
        {
            if (id != vm.id) return GetErrorResult("bad_request");

            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            var track = _context.Tracks.Find(id);
            if (track == null || track.owner != user.id) return GetErrorResult("track_not_found");

            if (ifVersion != null && ifVersion.Value != track.version) {
                return GetErrorResult("track_changed", TrackVM.FromTrack(track, _app, vm.lyrics != null));
            }

            track.name = vm.name;
            track.artist = vm.artist;
            if (vm.lyrics != null) track.lyrics = vm.lyrics;
            if (vm.visibility != null) track.visibility = vm.visibility.Value;

            // _context.Entry(track).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return new JsonResult(TrackVM.FromTrack(track, _app, vm.lyrics != null));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult> GetTrack(int id)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            var track = _context.Tracks.Find(id);
            if (track == null || track.owner != user.id) return GetErrorResult("track_not_found");

            return new JsonResult(TrackVM.FromTrack(track, _app, true));
        }

        [HttpGet("{id}/lyrics")]
        public async Task<ActionResult> GetTrackLyrics(int id)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            var track = _context.Tracks.Find(id);
            if (track == null || track.owner != user.id) return GetErrorResult("track_not_found");

            return new JsonResult(new { 
                lyrics = track.lyrics
            });
        }

        [HttpGet("{id}/url")]
        public async Task<ActionResult> GetTrackUrl(int id, [FromQuery] string conv, [FromServices] ConvertService cs)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            var track = _context.Tracks.Find(id);
            if (track == null || track.owner != user.id) return GetErrorResult("track_not_found");

            if (conv == null) return new JsonResult(new { url = track.url });

            if (track.files == null) track.files = new List<TrackFile>();
            var file = track.files.Find(x => x.ConvName == conv);
            if (file != null) return new JsonResult(new
            {
                url = track.ConvUrl(conv)
            });

            var convObj = _context.MCloudConfig.FindConverter(conv);
            if (convObj == null) return GetErrorResult("conv_not_found");

            var r = await cs.GetConverted(_context, track, convObj);
            if (!r.AlreadyExisted)
            {
            RETRY:
                track.files.Add(r.TrackFile);
                if (await _context.FailedSavingChanges()) goto RETRY;
            }

            return new JsonResult(new
            {
                url = track.ConvUrl(conv)
            });
        }

        [HttpGet]
        public async Task<ActionResult> FindTracks([FromQuery] string query)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");
            if (query == null) return GetErrorResult("no_query");

            query = query.ToLower();
            var result = _context.Tracks.Where(t =>
                (t.owner == user.id || t.visibility == Visibility.Public) // visible by user
                && ((t.name.Length > 0 && query.Contains(t.name.ToLower())) 
                    || (t.artist.Length > 0 && query.Contains(t.artist.ToLower()))
                    || t.name.ToLower().Contains(query) || t.artist.ToLower().Contains(query))
            );

            return new JsonResult(new {
                tracks = result.Select(x => TrackVM.FromTrack(x, _app, false))
            });
        }

        [HttpPost("uploadrequest")]
        public async Task<ActionResult> UploadRequest([FromBody] UploadRequestArg arg)
        {
            if (!_context.IsLogged) return GetErrorResult("no_login");

            if (arg.Size < 0 || (arg.Size > 100 * 1024 * 1024 && _context.User.role != UserRole.SuperAdmin))
                return GetErrorResult("size_out_of_range");

            var extNamePos = arg.Filename.LastIndexOf('.');
            var extName = extNamePos >= 0 ? arg.Filename.Substring(extNamePos + 1).ToLower() : "mp3";
            if (!SupportedFileFormats.Contains(extName) && _context.User.role != UserRole.SuperAdmin)
                return GetErrorResult("unsupported_file_format");
            var filename = Guid.NewGuid().ToString("D") + "." + extName;
            var filepath = "tracks/" + filename;

            if (_app.StorageService.Mode == StorageMode.Direct)
            {
                return new JsonResult(new { mode = "direct" });
            }
            else
            {
                var r = await _app.StorageService.RequestUpload(new RequestUploadOptions
                {
                    DestFilePath = filepath,
                    Length = arg.Size
                });

                return new JsonResult(new
                {
                    mode = "put-url",
                    url = r.Url,
                    method = r.Method,
                    tag = filepath + "|" + arg.Size + "|" + _app.SignTag(r.Url + "|" + filepath + "|" + arg.Size)
                });
            }
        }

        public class UploadRequestArg
        {
            public string Filename { get; set; }
            public long Size { get; set; }
        }

        public class UploadResultArg
        {
            public string Url { get; set; }
            public string Filename { get; set; }
            public string Tag { get; set; }
        }

        [HttpPost("uploadresult")]
        public async Task<ActionResult> UploadResult([FromBody]UploadResultArg arg)
        {
            if (!_context.IsLogged) return GetErrorResult("no_login");

            var tagSplits = arg.Tag.Split('|');
            var filepath = tagSplits[0];
            var size = long.Parse(tagSplits[1]);
            if (tagSplits[2] != _app.SignTag(arg.Url + "|" + filepath + "|" + size))
                return GetErrorResult("invalid_tag");

            var track = new Track
            {
                name = arg.Filename,
                artist = "Unknown",
                owner = _context.User.id,
                url = "storage/" + filepath,
                size = size > int.MaxValue ? int.MaxValue : (int)size
            };

            var extNamePos = arg.Filename.LastIndexOf('.');
            var extName = extNamePos >= 0 ? arg.Filename.Substring(extNamePos + 1).ToLower() : null;

            if (extName == null && _context.User.role == UserRole.SuperAdmin)
            {
                track.artist = "";
            }
            else
            {
                Directory.CreateDirectory(Path.Combine(_context.MCloudConfig.StorageDir, "tracks"));
                var destFile = Path.Combine(_context.MCloudConfig.StorageDir, filepath);

                await _app.StorageService.GetFile(arg.Url, destFile);

                if (new FileInfo(destFile).Length != size)
                {
                    track.DeleteFile(_app);
                    return GetErrorResult("wrong_size");
                }

                await Task.Run(() =>
                {
                    try
                    {
                        track.ReadTrackInfoFromFile(_app);
                    }
                    catch
                    {
                    }
                });
            }

            _context.Tracks.Add(track);
            await _context.SaveChangesAsync();

            return new JsonResult(TrackVM.FromTrack(track, _app, false)) { StatusCode = 201 };
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

        static string[] SupportedFileFormats = new[] {
            "mp3", "aac", "m4a", "mp4", "ogg", "opus", "flac", "ape", "wav"
        };

        [HttpPost("newfile")]
        [RequestSizeLimit(128 * 1024 * 1024)]
        public async Task<ActionResult> PostNewFile()
        {
            if (Request.ContentType != "application/x-mcloud-upload")
                return GetErrorResult("bad_content_type");

            if (_app.StorageService.Mode != StorageMode.Direct)
                return GetErrorResult("direct_upload_disabled");

            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            var stream = Request.Body;

            // Read the Track json
            var jsonBytesLen = await ReadBlockLength(stream);
            if (jsonBytesLen < 0 || jsonBytesLen > 1000)
                return GetErrorResult("json_len_out_of_range");
            var jsonStr = await ReadString(stream, jsonBytesLen);
            var track = JsonSerializer.Deserialize<Track>(jsonStr);

            var extNamePos = track.name.LastIndexOf('.');
            var extName = extNamePos >= 0 ? track.name.Substring(extNamePos + 1).ToLower() : "mp3";
            if (!SupportedFileFormats.Contains(extName) && _context.User.role != UserRole.SuperAdmin)
                return GetErrorResult("unsupported_file_format");

            // Now start reading the file
            var fileLength = await ReadBlockLength(stream);
            if (fileLength < 0 || fileLength > 100 * 1024 * 1024)
                return GetErrorResult("file_len_out_of_range");

            // Read the stream into a temp file
            var tmpdir = Path.Combine(_context.MCloudConfig.StorageDir, "tracks-inprogress");
            Directory.CreateDirectory(tmpdir);

            var filename = Guid.NewGuid().ToString("D") + "." + extName;
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
            track.size = fileLength;
            track.url = "storage/tracks/" + filename;
            track.owner = user.id;
            await Task.Run(() =>
            {
                try
                {
                    track.ReadTrackInfoFromFile(_app);
                }
                catch
                {
                }
            });
            _context.Tracks.Add(track);
            await _context.SaveChangesAsync();

            return new JsonResult(TrackVM.FromTrack(track, _app, false)) { StatusCode = 201 };
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

        [HttpDelete("{trackid}")]
        public async Task<ActionResult> DeleteTrack([FromRoute] int trackid)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            var track = await _context.Tracks.FindAsync(trackid);
            if (track?.IsVisibleToUser(user) != true) return GetErrorResult("track_not_found");
            if (track?.IsWritableByUser(user) != true) return GetErrorResult("permission_denied");

            _context.Tracks.Remove(track);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return GetErrorResult("track_not_found");
            }

            track.DeleteFile(_app);

            return NoContent();
        }

        bool IsCommentsEnabled(User user) => _app.Config.TrackCommentsEnabled || user.role == UserRole.SuperAdmin;

        [HttpGet("{trackid}/comments")]
        public async Task<ActionResult> GetComments([FromRoute] int trackid, [FromQuery] int begin)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");
            if (!IsCommentsEnabled(user)) return GetErrorResult("track_comments_disabled");

            var track = await _context.Tracks.FindAsync(trackid);
            if (track?.IsVisibleToUser(user) != true) return GetErrorResult("track_not_found");

            return RenderComments("track/" + trackid);
        }

        [HttpPost("{trackid}/comments/new")]
        public async Task<ActionResult> PostComments([FromRoute] int trackid, [FromBody] CommentVM vm)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");
            if (!IsCommentsEnabled(user)) return GetErrorResult("track_comments_disabled");

            var track = await _context.Tracks.FindAsync(trackid);
            if (track?.IsVisibleToUser(user) != true) return GetErrorResult("track_not_found");

            var comm = new Comment
            {
                tag = "track/" + trackid,
                uid = user.id,
                date = DateTime.UtcNow,
                content = vm.content
            };
            _context.Comments.Add(comm);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(PostComments), comm.ToVM(user));
        }

        [HttpPut("{trackid}/comments/{id}")]
        public async Task<ActionResult> PutComments([FromRoute] int trackid, [FromRoute] int id, [FromBody] CommentVM vm)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");
            if (!IsCommentsEnabled(user)) return GetErrorResult("track_comments_disabled");

            if (id != vm.id) return GetErrorResult("bad_id");

            var track = await _context.Tracks.FindAsync(trackid);
            if (track?.IsVisibleToUser(user) != true) return GetErrorResult("track_not_found");

            var comm = await _context.Comments.FindAsync(id);
            if (comm.uid != user.id || comm.tag != "track/" + trackid) return GetErrorResult("bad_comment");

            comm.content = vm.content;

            // _context.Entry(comm).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return Ok(comm.ToVM(user));
        }

        [HttpDelete("{trackid}/comments/{id}")]
        public async Task<ActionResult> DeleteComments([FromRoute] int trackid, [FromRoute] int id)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");
            if (!IsCommentsEnabled(user)) return GetErrorResult("track_comments_disabled");

            var track = await _context.Tracks.FindAsync(trackid);
            if (track?.IsVisibleToUser(user) != true) return GetErrorResult("track_not_found");

            var comm = await _context.Comments.FindAsync(id);
            if (comm.uid != user.id || comm.tag != "track/" + trackid) return GetErrorResult("bad_comment");

            _context.Comments.Remove(comm);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
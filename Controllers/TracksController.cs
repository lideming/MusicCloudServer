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
using Microsoft.Extensions.Logging;

namespace MCloudServer.Controllers
{
    [Route("api/tracks")]
    [ApiController]
    public class TracksController : MyControllerBase
    {
        public TracksController(DbCtx context, ILogger<TracksController> logger) : base(context)
        {
            this.logger = logger;
        }

        private readonly ILogger<TracksController> logger;

        [HttpPut("{id}")]
        public async Task<ActionResult> PutTrack(int id, TrackVM vm)
        {
            if (id != vm.id) return GetErrorResult("bad_request");

            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            var track = _context.Tracks.Find(id);
            if (track?.IsWritableByUser(user) != true) return GetErrorResult("track_not_found");

            if (vm.version != null && vm.version.Value != track.version) goto TRACK_CHANGED;

            RETRY:
            track.name = vm.name;
            track.artist = vm.artist;
            if (vm.album != null) track.album = vm.album;
            if (vm.albumArtist != null) track.albumArtist = vm.albumArtist;
            if (vm.lyrics != null) track.lyrics = vm.lyrics;
            if (vm.visibility != null) track.visibility = vm.visibility.Value;
            if (vm.groupId != null) track.groupId = vm.groupId.Value;
            track.version++;

            // _context.Entry(track).State = EntityState.Modified;
            if (await _context.FailedSavingChanges())
            {
                if (vm.version != null) goto TRACK_CHANGED;
                goto RETRY;
            }

            return new JsonResult(TrackVM.FromTrack(track, _app, vm.lyrics != null));

        TRACK_CHANGED:
            return new JsonResult(new
            {
                error = "track_changed",
                track = TrackVM.FromTrack(track, _app, vm.lyrics != null)
            });
        }

        [HttpGet("{id}")]
        public async Task<ActionResult> GetTrack(int id)
        {
            var user = await GetLoginUser();
            //if (user == null) return GetErrorResult("no_login");

            var track = await _context.GetTrack(id);
            if (track?.IsVisibleToUser(user) != true) return GetErrorResult("track_not_found");

            return new JsonResult(TrackVM.FromTrack(track, _app, true));
        }

        [HttpGet("{id}/lyrics")]
        public async Task<ActionResult> GetTrackLyrics(int id)
        {
            var user = await GetLoginUser();
            //if (user == null) return GetErrorResult("no_login");

            var track = await _context.GetTrack(id);
            if (track?.IsVisibleToUser(user) != true) return GetErrorResult("track_not_found");

            return new JsonResult(new
            {
                lyrics = track.lyrics
            });
        }

        [HttpGet("group/{id}")]
        public async Task<ActionResult> GetGroup(int id)
        {
            var user = await GetLoginUser();
            var uid = user?.id ?? 0;
            //if (user == null) return GetErrorResult("no_login");

            var result = _context.Tracks.Where(t =>
                t.groupId == id &&
                (t.owner == uid || t.visibility == Visibility.Public) // visible by user)
            );

            return new JsonResult(new
            {
                tracks = result.Select(x => TrackVM.FromTrack(x, _app, false))
            });
        }

        public class ConvertArg
        {
            public string profile { get; set; }
        }

        [HttpPost("{id}/convert")]
        public async Task<ActionResult> PostConvert(int id, [FromBody] ConvertArg arg, [FromServices] ConvertService cs)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            var track = await _context.GetTrack(id);
            if (track?.IsVisibleToUser(user) != true) return GetErrorResult("track_not_found");

            var profile = arg.profile;
            if (profile == null) return GetErrorResult("no_profile");

            if (track.files == null) track.files = new List<TrackFile>();
            var file = track.files.Find(x => x.ConvName == profile);
            if (file == null)
            {
                var convObj = _context.MCloudConfig.FindConverter(profile);
                if (convObj == null) return GetErrorResult("profile_not_found");

                var r = await cs.GetConverted(_context, track, convObj);
                file = r.TrackFile;
                if (!r.AlreadyExisted)
                {
                RETRY:
                    _context.Files.Add(file.File);
                    _context.TrackFiles.Add(file);
                    track.files.Add(file);
                    if (await _context.FailedSavingChanges()) goto RETRY;
                }
            }

            return new JsonResult(new TrackFileVM(file));
        }

        [HttpGet]
        public async Task<ActionResult> FindTracks([FromQuery] string query, [FromQuery] int? offset)
        {
            var user = await GetLoginUser();
            //if (user == null) return GetErrorResult("no_login");
            if (query == null) return GetErrorResult("no_query");

            var uid = user?.id ?? 0;
            query = query.ToLower();
            var result = Track.Includes(_context.Tracks).Where(t =>
                (t.owner == uid || t.visibility == Visibility.Public) // visible by user
                && (
                    t.name.ToLower().Contains(query) || t.artist.ToLower().Contains(query) ||
                    t.album.ToLower().Contains(query) || t.albumArtist.ToLower().Contains(query)
                )
            ).Skip(offset ?? 0).Take(200);

            return new JsonResult(new
            {
                tracks = result.Select(x => TrackVM.FromTrack(x, _app, false))
            });
        }

        [HttpPost("uploadrequest")]
        public async Task<ActionResult> UploadRequest([FromBody] UploadRequestArg arg)
        {
            if (!_context.IsLogged) return GetErrorResult("no_login");
            var user = await GetLoginUser();

            if (arg.Size < 0 || !user.AllowFileUploadSize(arg.Size))
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
        public async Task<ActionResult> UploadResult([FromBody] UploadResultArg arg)
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
                fileRecord = new StoredFile{
                    path = "storage/" + filepath,
                    size = size
                }
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

                await ReadTrackFileInfo(track);
            }

            AddTrackWithFile(track, extName);
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
        [RequestSizeLimit(1024 * 1024 * 1024)]
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
            if (fileLength < 0 || !user.AllowFileUploadSize(fileLength))
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
                    await stream.CopyToAsync(fs, 64 * 1024);
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
            track.owner = user.id;
            track.fileRecord = new StoredFile {
                path = "storage/tracks/" + filename,
                size = fileLength
            };
            await ReadTrackFileInfo(track);
            AddTrackWithFile(track, extName);
            await _context.SaveChangesAsync();

            return new JsonResult(TrackVM.FromTrack(track, _app, false)) { StatusCode = 201 };
        }

        private Task ReadTrackFileInfo(Track track) {
            return Task.Run(() =>
            {
                try
                {
                    track.ReadTrackInfoFromFile(_app);
                    track.ReadPicutreFromTrackFile(_app);
                }
                catch (Exception e)
                {
                    logger.LogWarning(e, "Error reading track info from {path}", track.url);
                }
            });
        }

        private void AddTrackWithFile(Track track, string extName) {
            var file = track.fileRecord;
            _context.Files.Add(file);
            _context.TrackFiles.Add(new TrackFile {
                Track = track,
                Bitrate = track.length == 0 ? 0 : (int)(file.size * 8 / track.length / 1024),
                ConvName = "",
                File = file,
                Format = extName ?? ""
            });
            _context.Tracks.Add(track);
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

            var track = await _context.GetTrack(trackid);
            if (track?.IsVisibleToUser(user) != true) return GetErrorResult("track_not_found");
            if (track?.IsWritableByUser(user) != true) return GetErrorResult("permission_denied");

            track.DeleteFile(_app);

            _context.Files.RemoveRange(track.files.Select(x => x.File));
            _context.TrackFiles.RemoveRange(track.files);
            _context.Tracks.Remove(track);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return GetErrorResult("track_not_found");
            }


            return NoContent();
        }

        [HttpPost("visibility")]
        public async Task<ActionResult> PostVisibility(VisibilityArg arg)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            RETRY:
            var ids = arg.trackids.Distinct().ToList();
            var tracks = await _context.Tracks
                    .Where(x => ids.Contains(x.id))
                    .ToListAsync();

            foreach (var id in ids)
            {
                var track = tracks.Find(t => t.id == id);
                if (track?.IsVisibleToUser(user) != true)
                    return GetErrorResult("track_not_found", new { trackid = id });
                if (track?.IsWritableByUser(user) != true)
                    return GetErrorResult("permission_denied", new { trackid = id });
                track.visibility = arg.visibility;
            }

            if (await _context.FailedSavingChanges()) goto RETRY;

            return Ok();
        }

        public class VisibilityArg
        {
            public List<int> trackids { get; set; }
            public Visibility visibility { get; set; }
        }

        [HttpGet("{trackid}/stat")]
        public async Task<ActionResult> GetStat([FromRoute] int trackid)
        {
            var user = await GetLoginUser();
            var track = await _context.Tracks.FindAsync(trackid);
            if (track?.IsVisibleToUser(user) != true) return GetErrorResult("track_not_found");

            var plays = _context.Plays.Where(p => p.trackid == trackid);
            var comments = GetCommentsByTag("track/" + trackid);

            return new JsonResult(new
            {
                playcount = await plays.CountAsync(),
                lastplay = (await plays.OrderBy(p => p.time).LastOrDefaultAsync())?.time,
                commentcount = await comments.CountAsync()
            });
        }

        [HttpGet("recentplays")]
        public async Task<ActionResult> GetRecentPlays()
        {
            var plays = _context.Plays.Include(p => p.Track).Where(p => p.Track.visibility == Visibility.Public)
                    .OrderByDescending(p => p.id).Take(20);

            return new JsonResult(new
            {
                recentplays = await plays.Select(p => new
                {
                    uid = p.uid,
                    track = TrackVM.FromTrack(p.Track, _app, false),
                    time = p.time
                }).ToListAsync()
            });
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

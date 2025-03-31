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
using System.Diagnostics;

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

            var track = await _context.GetTrack(id);
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

        [HttpPut("{id}/picture")]
        public async Task<IActionResult> PutPicture(int id)
        {
            var login = await GetLoginUser();
            if (login == null) return GetErrorResult("no_login");
            byte[] pic;
            using(var ms = new MemoryStream()) {
                await Request.Body.CopyToAsync(ms);
                pic = ms.ToArray();
            }
            var track = await _context.GetTrack(id);
            if (track?.IsWritableByUser(login) != true) return GetErrorResult("track_not_found");
            await track.SetPicture(_app, pic);
            await _context.SaveChangesAsync();
            return new JsonResult(TrackVM.FromTrack(track, _app, true));
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

            var result = Track.Includes(
                _context.Tracks.Where(t =>
                    t.groupId == id &&
                    (t.owner == uid || t.visibility == Visibility.Public) // visible by user)
            ));

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

                var r = await cs.GetConverted(track, convObj);
                file = r.TrackFile;
            }

            return new JsonResult(new TrackFileVM(file));
        }

        [HttpGet("{id}/loudnessmap")]
        public async Task<ActionResult> GetAudioAnalytics(int id)
        {
            var user = await GetLoginUser();
            var track = await _context.GetTrack(id);
            if (track == null || !track.IsVisibleToUser(user)) return GetErrorResult("track_not_found");

            var info = await _context.TrackAudioInfos.FindAsync(id);
            if (info == null) {
                var input = _app.ResolveStoragePath(track.fileRecord.path);
                using (var ms = new MemoryStream()) {
                    var proc = Process.Start(new ProcessStartInfo() {
                        FileName = "ffmpeg",
                        Arguments = $"-loglevel warning -i {input} -f s8 -c:a pcm_s8 -",
                        RedirectStandardOutput = true,
                    });
                    var pcm = proc.StandardOutput.BaseStream;
                    var buffer = new byte[16 * 1024];
                    while(true) {
                        var haveRead = 0;
                        do {
                            var read = await pcm.ReadAsync(buffer.AsMemory(haveRead));
                            if (read == 0) break;
                            haveRead += read;
                        } while (haveRead < 16 * 1024);
                        if (haveRead == 0) break;
                        double sum = 0;
                        for (var i = 0; i < haveRead; i++) {
                            var x = (sbyte)buffer[i];
                            sum += x * x;
                        }
                        var rms = Math.Sqrt(sum / haveRead);
                        ms.WriteByte((byte)rms);
                    }
                    logger.LogInformation("Processed {0}", ms.Length);
                    info = new TrackAudioInfo() {
                        Id = id,
                        Peaks = ms.ToArray(),
                    };
                }
                _context.TrackAudioInfos.Add(info);
                try
                {
                     await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    logger.LogWarning("Saving loudness and DbUpdateConcurrencyException");
                }
            }
            return new FileStreamResult(new MemoryStream(info.Peaks, false), "application/octet-stream");
        }

        [HttpGet]
        public async Task<ActionResult> FindTracks([FromQuery] string query, [FromQuery] int? beforeId)
        {
            var limit = 200;
            var user = await GetLoginUser();
            //if (user == null) return GetErrorResult("no_login");
            // if (query == null) return GetErrorResult("no_query");

            var uid = user?.id ?? 0;
            var result = Track.Includes(_context.Tracks)
                .Where(t =>(t.owner == uid || t.visibility == Visibility.Public)); // visible by user
            if (query?.Length > 0) {
                query = query.ToLower();
                result = result.Where(t => (
                    t.name.ToLower().Contains(query) || t.artist.ToLower().Contains(query) ||
                    t.album.ToLower().Contains(query) || t.albumArtist.ToLower().Contains(query)
                ));
            }
            if (beforeId is int id) {
                result = result.Where(t => t.id < beforeId);
            }
            result = result.OrderByDescending(t => t.id);
            result = result.Take(limit);

            return new JsonResult(new
            {
                tracks = result.Select(x => TrackVM.FromTrack(x, _app, false)),
                limit
            });
        }

        public class UploadRequestArg
        {
            public string Filename { get; set; }
            public long Size { get; set; }
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
                throw new Exception("Unexpected StorageMode");;
            }
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
            "mp3", "aac", "m4a", "ogg", "opus", "flac", "ape", "wav"
        };

        static string[] SupportedVideoFileFormats = new[] {
            "mp4"
        };

        [HttpPost("newfile")]
        [RequestSizeLimit(1024 * 1024 * 1024)]
        public async Task<ActionResult> PostNewFile([FromServices] FileService fileService)
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
            var trackvm = JsonSerializer.Deserialize<TrackVM>(jsonStr);
            var track = new Track();
            track.name = trackvm.name;
            track.artist = trackvm.artist;
            track.album = trackvm.album;
            track.albumArtist = trackvm.albumArtist;
            track.lyrics = trackvm.lyrics;
            track.ctime = DateTime.Now;

            var extNamePos = track.name.LastIndexOf('.');
            var extName = extNamePos >= 0 ? track.name.Substring(extNamePos + 1).ToLower() : "mp3";
            TrackType trackType = SupportedFileFormats.Contains(extName) ? TrackType.audio :
                SupportedVideoFileFormats.Contains(extName) ? TrackType.video : 
                TrackType.unknown;

            logger.LogInformation("New file ({1}): '{0}'", trackType, track.name);

            if (trackType == TrackType.unknown && _context.User.role != UserRole.SuperAdmin)
                return GetErrorResult("unsupported_file_format");

            // Now start reading the file
            var fileLength = await ReadBlockLength(stream);
            if (fileLength < 0 || !user.AllowFileUploadSize(fileLength))
                return GetErrorResult("file_len_out_of_range");

            var storedFile = await fileService.SaveFile("storage/tracks/{0}." + extName, stream, fileLength);

            // Fill the track info, and complete.
            track.owner = user.id;
            track.type = trackType;
            track.fileRecord = storedFile;
            await ReadTrackFileInfo(track);
            AddTrackWithFile(track, extName);
            await _context.SaveChangesAsync();

            var origBitrate = (int)(track.length > 0 ? track.size / track.length / 128 : 0);
            foreach(var conv in _app.Config.Converters) {
                if (conv.Auto && conv.Bitrate <= origBitrate / 2) {
                    _app.ConvertService.AddBackgroundConvert(track, conv);
                }
            }

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

        [HttpGet("{trackid}/comments_count")]
        public async Task<ActionResult> GetCommentsCount([FromRoute] int trackid, [FromQuery] int begin)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");
            if (!IsCommentsEnabled(user)) return GetErrorResult("track_comments_disabled");

            var track = await _context.Tracks.FindAsync(trackid);
            if (track?.IsVisibleToUser(user) != true) return GetErrorResult("track_not_found");

            return await RenderCommentsCount("track/" + trackid);
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

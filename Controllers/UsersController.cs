using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MCloudServer;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Microsoft.AspNetCore.Http.Extensions;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace MCloudServer.Controllers
{
    [Route("api/users")]
    [ApiController]
    public class UsersController : MyControllerBase
    {

        public UsersController(DbCtx context) : base(context)
        {
        }

        // GET: api/Users/5
        [HttpGet("{id_or_name}")]
        public async Task<IActionResult> GetUser(string id_or_name)
        {
            User user = await _context.GetUserFromIdOrName(id_or_name);

            return await GetUser(user);
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetUserMe()
        {
            var user = await GetLoginUser();
            return await GetUser(user, true);
        }

        class LoginOptions {
            public string code { get; set; }
        }

        [HttpPost("me/login")]
        public async Task<IActionResult> PostUserLogin()
        {
            var user = await GetLoginUser();
            string token = null;
            if (user != null)
            {
                var record = await _context.UserService.CreateLoginRecord(user);
                token = record.token;
            }
            return await GetUser(user, true, token);
        }

        [HttpGet("me/socialLogin")]
        public async Task<IActionResult> SocialLogin([FromQuery] string provider, [FromQuery] string returnUrl)
        {
            return StartSocialLogin(provider, returnUrl, "login");
        }

        IActionResult StartSocialLogin(string provider, string returnUrl, string state) {
            if (!_app.Config.SocialLogin.TryGetValue(provider, out var config)) {
                return BadRequest("Invalid provider");
            }
            return Redirect(config.AuthEndpoint + new QueryBuilder() {
                {"client_id" , config.ClientId},
                {"redirect_uri", (Request.IsHttps ? "https" : "http") + "://" + Request.Host + "/api/users/me/socialLogin-continued"},
                {"response_type", "code"},
                {"scope", "openid profile"},
                {"state", _app.SignToken(new [] { provider, returnUrl, state }, TimeSpan.FromMinutes(30))},
            }.ToString());
        }

        [HttpGet("me/socialLogin-continued")]
        public async Task<IActionResult> SocialLoginContinued([FromQuery] string code, [FromQuery] string state)
        {
            // Parse code and state
            var parts = _app.ExtractToken(state);
            var providerId = parts[0];
            var returnUrl = parts[1];
            var innerState = parts[2];
            var provider = _app.Config.SocialLogin[providerId];

            // Get id token and access token from the Token Endpoint
            string accessToken, refreshToken, idToken;

            using (var client = new HttpClient())
            {
                var msg = new HttpRequestMessage(HttpMethod.Post, provider.TokenEndpoint)
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string> {
                        {"grant_type", "authorization_code"},
                        {"code", code},
                        {"redirect_uri", (Request.IsHttps ? "https" : "http") + "://" + Request.Host + "/api/users/me/socialLogin-continued"},
                    }),
                };
                msg.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(provider.ClientId + ":" + provider.ClientSecret)));
                var resp = await client.SendAsync(msg);
                var json = await resp.Content.ReadAsStringAsync();
                var parsed = JsonDocument.Parse(json);
                accessToken = parsed.RootElement.TryGetProperty("access_token", out var jsonat) ? jsonat.GetString() : null;
                refreshToken = parsed.RootElement.TryGetProperty("refresh_token", out var jsonrt) ? jsonrt.GetString() : null;
                idToken = parsed.RootElement.TryGetProperty("id_token", out var jsonit) ? jsonit.GetString() : null;
            }
            var claims = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(idToken);
            var providerUserId = claims.Subject;
            var providerUsername = claims.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value ?? claims.Subject;

            return await FinishSocialLogin(providerId, returnUrl, accessToken, refreshToken, providerUserId, providerUsername);
        }

        private async Task<IActionResult> FinishSocialLogin(string providerId, string returnUrl, string accessToken, string refreshToken, string providerUserId, string providerUsername)
        {
            var link = await _context.SocialLinks.Where(s => s.provider == providerId && s.idFromProvider == providerUserId)
                        .Include(s => s.user)
                        .SingleOrDefaultAsync();
            User user;

            if (link == null) {
                user = new User() {
                    username = providerUsername,
                    role = UserRole.User,
                    lists = new List<int>(),
                };
                _context.Users.Add(user);
                link = new UserSocialLink() {
                    accessToken = accessToken,
                    refreshToken = refreshToken,
                    user = user,
                    provider = providerId,
                    idFromProvider = providerUserId,
                    nameFromProvider = providerUsername,
                };
                _context.SocialLinks.Add(link);
            } else {
                user = link.user;
            }


            var record = _context.UserService.CreateLoginRecord_NoSave(user);
            var token = record.token;

            await _context.SaveChangesAsync();
            return Redirect(returnUrl + "#token=" + token);
        }

        [HttpPost("me/logout")]
        public async Task<IActionResult> PostUserLogout()
        {
            await _context.UserService.Logout();
            return Ok();
        }

        [HttpGet("me/serverconfig")]
        public IActionResult GetServerConfig() {
            return new JsonResult(ServerConfig());
        }

        private object ServerConfig()
        {
            return new
            {
                storageUrlBase = _app.Config.StorageUrlBase,
                msg = "uptime " + _app.GetUptime().TotalMinutes.ToString("N0") + " minutes",
                notesEnabled = _app.Config.NotesEnabled,
                discussionEnabled = _app.Config.DiscussionEnabled,
                trackCommentsEnabled = _app.Config.TrackCommentsEnabled,
                allowRegistration = _app.Config.AllowRegistration,
                passwordLogin = _app.Config.PasswordLogin,
                socialLogin = _app.Config.SocialLogin.Select(kv => new {
                    id = kv.Key,
                    name = kv.Value.Name,
                    icon = kv.Value.Icon,
                }).ToList(),
            };
        }

        private async Task<IActionResult> GetUser(User user, bool me = false, string newToken = null)
        {
            if (user == null)
            {
                return GetErrorResult("user_not_found");
            }

            // get all needed lists in a single SQL query.
            var query = await _context.Lists
                .Include(l => l.pic)
                .Include(l => l.user)
                .Where(l => l.owner == user.id || (user.lists.Contains(l.id) && l.visibility == Visibility.Public))
                .Select(l => new TrackListInfoVM
                {
                    id = l.id,
                    owner = l.owner,
                    name = l.name,
                    visibility = l.visibility,
                    picurl = l.pic.path,
                    ownerName = l.user.username
                })
                .ToListAsync();

            // get the order right, remove unreadable items
            var lists = user.lists.Select(id => query.Find(l => l.id == id)).Where(x => x != null).ToList();

            // add possible missing items owned by the user
            lists.AddRange(query.Where(x => !lists.Contains(x)));

            var avatar = user.avatarId.HasValue ? await _context.Files.FindAsync(user.avatarId.Value) : null;

            if (me)
            {
                return new JsonResult(new
                {
                    id = user.id,
                    username = user.username,
                    avatar = avatar?.path,
                    lists = lists,
                    playing = await GetUserPlaying(user),
                    role = user.role == UserRole.SuperAdmin ? "admin" : "user",
                    token = newToken,
                    serverOptions = ServerConfig(),
                }, new JsonSerializerOptions { IgnoreNullValues = true });
            }
            else
            {
                return new JsonResult(new
                {
                    id = user.id,
                    username = user.username,
                    avatar = avatar?.path,
                    lists = lists.Where(l => l.visibility == Visibility.Public)
                });
            }
        }

        // PUT: api/users/me
        // update lists
        [HttpPut("me")]
        public async Task<IActionResult> PutUser(UserPutVM newState)
        {
            var user = await GetLoginUser();
            if (user == null || user.id != newState.id || user.username != newState.username)
            {
                return GetErrorResult("check_failed");
            }

        RETRY:

            if (newState.listids != null)
            {
                var listids = newState.listids;
                user.lists = listids;
            }
            if (newState.passwd != null)
            {
                user.passwd = Utils.HashPassword(newState.passwd);
            }

            // _context.Entry(user).State = EntityState.Modified;
            user.version++;

            if (await _context.FailedSavingChanges()) goto RETRY;

            return await GetUser(user);
        }

        [HttpPut("me/avatar")]
        public async Task<IActionResult> PutUserAvatar()
        {
            var login = await GetLoginUser();
            if (login == null) return GetErrorResult("no_login");
            byte[] pic;
            using (var ms = new MemoryStream())
            {
                await Request.Body.CopyToAsync(ms);
                pic = ms.ToArray();
            }
            var internalPath = "storage/pic/" + Guid.NewGuid().ToString("D") + ".avatar.128.jpg";
            var fsPath = _app.ResolveStoragePath(internalPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fsPath));
            using (var img = Image.Load(pic))
            {
                img.Mutate(p => p.Resize(128, 0));
                img.SaveAsJpeg(fsPath);
            }
            login.avatar = new StoredFile
            {
                path = internalPath,
                size = new FileInfo(fsPath).Length,
            };
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("me/playing")]
        public async Task<IActionResult> PostMePlaying(TrackLocationWithProfile playing)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            await PostPlaying(_context, user, playing);

            return Ok();
        }

        public static async Task PostPlaying(DbCtx context, User user, TrackLocationWithProfile playing)
        {
        RETRY:
            var track = await context.Tracks.FindAsync(playing.trackid);
            var list = playing.listid > 0 ? await context.Lists.FindAsync(playing.listid) : null;
            if (track?.IsVisibleToUser(user) == true)
            {
                context.Plays.Add(new PlayRecord
                {
                    Track = track,
                    User = user,
                    listid = list?.IsVisibleToUser(user) == true ? playing.listid : 0,
                    audioprofile = playing.profile ?? "",
                    time = DateTime.Now
                });
            }

            user.last_playing = playing.ToString();
            // context.Entry(user).State = EntityState.Modified;
            user.version++;


            if (await context.FailedSavingChanges()) goto RETRY;
        }

        [HttpGet("me/playing")]
        public async Task<IActionResult> GetMePlaying()
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");
            return new JsonResult(await GetUserPlaying(user));
        }

        private async Task<object> GetUserPlaying(User user)
        {
            var location = TrackLocation.Parse(user.last_playing);
            Track track = null;
            if (location.trackid != 0)
            {
                track = await _context.GetTrack(location.trackid);
                if (track?.IsVisibleToUser(user) != true)
                    track = null;
            }

            return new
            {
                location.listid,
                location.position,
                location.trackid,
                track = track == null ? null : TrackVM.FromTrack(track, _app)
            };
        }

        [HttpPost("new")]
        public async Task<IActionResult> PostUser(UserRegisterVM userreg)
        {
            if (string.IsNullOrEmpty(userreg.username))
                return GetErrorResult("bad_arg");
            if (_context.Users.Any(u => u.username == userreg.username))
                return GetErrorResult("dup_user");
            var user = new User
            {
                role = UserRole.User,
                username = userreg.username,
                passwd = Utils.HashPassword(userreg.passwd),
                lists = new List<int>()
            };
            _context.Users.Add(user);
            var loginRecord = _context.UserService.CreateLoginRecord_NoSave(user);
            await _context.SaveChangesAsync();
            return await GetUser(user, true, loginRecord.token);
        }

        // Create a list and add to lists of user
        [HttpPost("me/lists/new")]
        public async Task<ActionResult<TrackListInfoVM>> PostMeList(TrackListPutVM vm)
        {
            using (var transa = await _context.Database.BeginTransactionAsync())
            {
                var user = await GetLoginUser();
                if (user == null) return GetErrorResult("no_login");

                var list = vm.ToList();
                list.owner = user.id;
                _context.Lists.Add(list);
                await _context.SaveChangesAsync();

            RETRY:
                user.lists.Add(list.id);
                user.version++;
                // _context.Entry(user).State = EntityState.Modified;
                if (await _context.FailedSavingChanges()) goto RETRY;

                await transa.CommitAsync();

                return list.ToTrackListInfo();
            }
        }

        // Delete a list
        [HttpDelete("me/lists/{id}")]
        public async Task<ActionResult<TrackListInfoVM>> DeleteMeList(int id)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            RETRY:
            var list = await _context.Lists.FindAsync(id);
            if (list == null || list.owner != user.id) return GetErrorResult("list_not_found");
            _context.Lists.Remove(list);
            user.lists.Remove(list.id);
            user.version++;
            // _context.Entry(user).State = EntityState.Modified;
            if (await _context.FailedSavingChanges()) goto RETRY;

            return list.ToTrackListInfo();
        }

        [HttpGet("me/uploads")]
        public async Task<ActionResult> GetMeUploads()
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            return new JsonResult(new
            {
                tracks = await Track.Includes(_context.Tracks).Where(t => t.owner == user.id)
                    .Select(x => TrackVM.FromTrack(x, _app, false))
                    .ToListAsync()
            });
        }

        [HttpGet("{id}/stat")]
        public async Task<ActionResult> GetStat([FromRoute] string id)
        {
            var login = await GetLoginUser();
            User user = await _context.GetUserFromIdOrName(id);
            if (user == null) return GetErrorResult("user_not_found");

            var plays = _context.Plays.Where(p => p.uid == user.id);

            var lastplay = await plays.OrderBy(p => p.time)
                .Include(p => p.Track.pictureFile)
                .Include(p => p.Track.thumbPictureFile)
                .Include(p => p.Track.fileRecord)
                .Include(p => p.Track.files).ThenInclude(f => f.File)
                .LastOrDefaultAsync();

            var lastPlayTime = lastplay?.time;
            var lastPlayTrack = lastplay?.Track;

            if (lastplay?.Track != null && !lastPlayTrack.IsVisibleToUser(login))
            {
                lastPlayTrack = null;
            }

            return new JsonResult(new
            {
                playCount = await plays.CountAsync(),
                lastPlayTime = lastPlayTime?.ToUniversalTime(),
                lastPlayTrack = lastPlayTrack == null ? null : TrackVM.FromTrack(lastPlayTrack, _app)
            });
        }

        [HttpGet("{id}/avatar.jpg")]
        public async Task<ActionResult> GetAvatar([FromRoute] string id)
        {
            User user = await _context.GetUserFromIdOrName(id);
            if (user == null) return NotFound();
            if (user.avatarId == null) return NotFound();
            var avatar = await _context.Files.FindAsync(user.avatarId);
            return Redirect(_app.GetFullUrlFromStoragePath(avatar.path));
        }

        [HttpGet("{id}/recentplays")]
        public async Task<ActionResult> GetRecentPlays([FromRoute] string id)
        {
            var login = await GetLoginUser();
            if (login == null) return GetErrorResult("no_login");
            User user = await _context.GetUserFromIdOrName(id);
            if (user == null || (user.id != login.id && login.role != UserRole.SuperAdmin))
                return GetErrorResult("user_not_found");

            var lastplay = await _context.Plays
                .Where(p => p.uid == user.id)
                .OrderByDescending(p => p.time)
                .Include(p => p.Track.pictureFile)
                .Include(p => p.Track.thumbPictureFile)
                .Include(p => p.Track.fileRecord)
                .Include(p => p.Track.files).ThenInclude(f => f.File)
                .Take(100)
                .ToListAsync();

            var tracks = lastplay
                .Select(p => p.Track)
                .Where(t => t.IsVisibleToUser(login))
                .Distinct()
                .Select(t => TrackVM.FromTrack(t, _app))
                .ToList();

            return new JsonResult(new { tracks });
        }

        bool IsNotesEnabled() => _app.Config.NotesEnabled || _context.User.role == UserRole.SuperAdmin;

        [HttpGet("me/notes")]
        public async Task<ActionResult> GetNotes([FromQuery] int begin)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");
            // When notes feature is disabled, users can still view or delete their notes (if exists).

            return RenderComments("un/" + user.id);
        }

        [HttpPost("me/notes/new")]
        public async Task<ActionResult> PostNotes([FromBody] CommentVM vm)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");
            if (!IsNotesEnabled()) return GetErrorResult("notes_disabled");

            var comm = new Comment
            {
                tag = "un/" + user.id,
                uid = user.id,
                date = DateTime.UtcNow,
                content = vm.content
            };
            _context.Comments.Add(comm);
            await _context.SaveChangesAsync();

            _message.TriggerEvent("note-changed", c => c.User.id == user.id);

            return CreatedAtAction(nameof(PostNotes), comm.ToVM(user));
        }

        [HttpPut("me/notes/{id}")]
        public async Task<ActionResult> PutNotes([FromRoute] int id, [FromBody] CommentVM vm)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");
            if (!IsNotesEnabled()) return GetErrorResult("notes_disabled");

            if (id != vm.id) return GetErrorResult("bad_id");

            var comm = await _context.Comments.FindAsync(id);
            if (comm.uid != user.id || comm.tag != "un/" + user.id) return GetErrorResult("bad_comment");

            comm.content = vm.content;

            // _context.Entry(comm).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            _message.TriggerEvent("note-changed", c => c.User.id == user.id);

            return Ok(comm.ToVM(user));
        }

        [HttpDelete("me/notes/{id}")]
        public async Task<ActionResult> DeleteNotes([FromRoute] int id)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");
            //if (!IsNotesEnabled()) return GetErrorResult("notes_disabled");

            var comm = await _context.Comments.FindAsync(id);
            if (comm.uid != user.id || comm.tag != "un/" + user.id) return GetErrorResult("bad_comment");

            _context.Comments.Remove(comm);
            await _context.SaveChangesAsync();

            _message.TriggerEvent("note-changed", c => c.User.id == user.id);

            return NoContent();
        }
    }
}

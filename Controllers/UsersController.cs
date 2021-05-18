using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MCloudServer;

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
            User user;
            if (int.TryParse(id_or_name, out var id)) {
                user = await _context.Users.FindAsync(id);
            } else {
                user = await _context.Users.Where(u => u.username == id_or_name).SingleOrDefaultAsync();
            }

            return await GetUser(user);
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetUserMe()
        {
            var user = await GetLoginUser();
            return await GetUser(user, true);
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

        [HttpPost("me/logout")]
        public async Task<IActionResult> PostUserLogout()
        {
            await _context.UserService.Logout();
            return Ok();
        }

        private async Task<IActionResult> GetUser(User user, bool me = false, string newToken = null)
        {
            if (user == null)
            {
                return GetErrorResult("user_not_found");
            }

            // get all needed lists in a single SQL query.
            var query = await _context.Lists
                .Where(l => l.owner == user.id || (user.lists.Contains(l.id) && l.visibility == Visibility.Public))
                .ToTrackListInfo()
                .ToListAsync();

            // get the order right, remove unreadable items
            var lists = user.lists.Select(id => query.Find(l => l.id == id)).Where(x => x != null).ToList();

            // add possible missing items owned by the user
            lists.AddRange(query.Where(x => !lists.Contains(x)));

            if (me)
            {
                return new JsonResult(new
                {
                    id = user.id,
                    username = user.username,
                    lists = lists,
                    playing = await GetUserPlaying(user),
                    role = user.role == UserRole.SuperAdmin ? "admin" : "user",
                    token = newToken,
                    serverOptions = new
                    {
                        storageUrlBase = _context.MCloudConfig.StorageUrlBase,
                        msg = "uptime " + _app.GetUptime().TotalMinutes.ToString("N0") + " minutes",
                        notesEnabled = _app.Config.NotesEnabled,
                        discussionEnabled = _app.Config.DiscussionEnabled,
                        trackCommentsEnabled = _app.Config.TrackCommentsEnabled
                    }
                }, new JsonSerializerOptions { IgnoreNullValues = true });
            }
            else
            {
                return new JsonResult(new
                {
                    id = user.id,
                    username = user.username
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

        [HttpPost("me/playing")]
        public async Task<IActionResult> PostMePlaying(TrackLocationWithProfile playing)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            await PostPlaying(_context, user, playing);

            return Ok();
        }

        public static async Task PostPlaying(DbCtx context, User user, TrackLocationWithProfile playing) {
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
                track = await _context.Tracks.FindAsync(location.trackid);
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
        public async Task<ActionResult<TrackListInfoVM>> PostMeList(ListPutVM vm)
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
                tracks = await _context.Tracks.Where(t => t.owner == user.id)
                    .Select(x => TrackVM.FromTrack(x, _app, false))
                    .ToListAsync()
            });
        }

        [HttpGet("{id}/stat")]
        public async Task<ActionResult> GetStat([FromRoute] string id)
        {
            var login = await GetLoginUser();
            User user;
            if (id == "me") {
                user = login;
                if (user == null) return GetErrorResult("no_login");
            } else {
                if (!int.TryParse(id, out var numid)) return GetErrorResult("wrong_id");
                user = await _context.Users.FindAsync(numid);
            }

            var plays = _context.Plays.Where(p => p.uid == user.id);

            var lastplay = (await plays.OrderBy(p => p.time).Include(p => p.Track).LastOrDefaultAsync());

            var lastPlayTime = lastplay?.time;
            var lastPlayTrack = lastplay?.Track;
            
            if (lastplay?.Track != null && !lastPlayTrack.IsVisibleToUser(login)) {
                lastPlayTrack = null;
            }

            return new JsonResult(new
            {
                playCount = await plays.CountAsync(),
                lastPlayTime = lastPlayTime?.ToUniversalTime(),
                lastPlayTrack = lastPlayTrack == null ? null : TrackVM.FromTrack(lastPlayTrack, _app)
            });
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

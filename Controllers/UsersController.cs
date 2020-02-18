using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        //// GET: api/Users
        //[HttpGet]
        //public async Task<ActionResult<IEnumerable<User>>> GetUsers()
        //{
        //    return await _context.Users.ToListAsync();
        //}

        // GET: api/Users/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var login = await GetLoginUser();
            if (login == null) return GetErrorResult("user_not_found");
            if (login.role != UserRole.SuperAdmin && login.id != id) return GetErrorResult("not_found");

            var user = await _context.Users.FindAsync(id);
            return await GetUser(user);
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetUserMe()
        {
            var user = await GetLoginUser();
            return await GetUser(user, true);
        }

        private async Task<IActionResult> GetUser(User user, bool me = false)
        {
            if (user == null) {
                return GetErrorResult("user_not_found");
            }

            // get all needed lists in a single SQL query.
            var lists = await _context.Lists
                .Where(l => user.lists.Contains(l.id))
                .ToTrackListInfo()
                .ToListAsync();

            // get the order right
            lists = user.lists.Select(id => lists.Find(l => l.id == id)).ToList();

            if (me) {
                return new JsonResult(new {
                    id = user.id,
                    username = user.username,
                    lists = lists,
                    servermsg = "uptime " + _app.GetUptime().TotalMinutes.ToString("N0") + " minutes",
                    playing = TrackLocation.Parse(user.last_playing)
                });
            } else {
                return new JsonResult(new {
                    id = user.id,
                    username = user.username,
                    // TODO: async
                    lists = lists
                });
            }
        }

        // PUT: api/users/me
        // update lists
        [HttpPut("me")]
        public async Task<IActionResult> PutUser(UserPutVM newState)
        {
            var user = await GetLoginUser();
            if (user == null || user.id != newState.id || user.username != newState.username) {
                return GetErrorResult("check_failed");
            }

            RETRY:

            if (newState.listids != null) {
                var listids = newState.listids;
                if (_context.Lists.Count(l => listids.Contains(l.id)) != listids.Count) {
                    return GetErrorResult("list_not_found");
                }
                user.lists = listids;
            }
            if (newState.passwd != null) {
                user.passwd = DbCtx.HashPassword(newState.passwd);
            }

            _context.Entry(user).State = EntityState.Modified;
            user.version++;

            if (await _context.FailedSavingChanges()) goto RETRY;

            return await GetUser(user);
        }

        [HttpPost("me/playing")]
        public async Task<IActionResult> PostMePlaying(TrackLocation playing)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            RETRY:
            user.last_playing = playing.ToString();
            _context.Entry(user).State = EntityState.Modified;
            user.version++;

            if (await _context.FailedSavingChanges()) goto RETRY;

            return Ok();
        }

        [HttpGet("me/playing")]
        public async Task<IActionResult> GetMePlaying()
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            return new JsonResult(TrackLocation.Parse(user.last_playing));
        }

        [HttpPost("new")]
        public async Task<IActionResult> PostUser(UserRegisterVM userreg)
        {
            if (string.IsNullOrEmpty(userreg.username))
                return GetErrorResult("bad_arg");
            if (_context.Users.Any(u => u.username == userreg.username))
                return GetErrorResult("dup_user");
            var user = new User {
                role = UserRole.User,
                username = userreg.username,
                passwd = DbCtx.HashPassword(userreg.passwd),
                lists = new List<int>()
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return await GetUser(user.id);
        }

        // Create a list and add to lists of user
        [HttpPost("me/lists/new")]
        public async Task<ActionResult<TrackListInfoVM>> PostMeList(ListPutVM vm)
        {
            using (var transa = await _context.Database.BeginTransactionAsync()) {
                var user = await GetLoginUser();
                if (user == null) return GetErrorResult("no_login");

                var list = vm.ToList();
                list.owner = user.id;
                _context.Lists.Add(list);
                await _context.SaveChangesAsync();

                RETRY:
                user.lists.Add(list.id);
                user.version++;
                _context.Entry(user).State = EntityState.Modified;
                if (await _context.FailedSavingChanges()) goto RETRY;

                await transa.CommitAsync();

                return list.ToTrackListInfo();
            }
        }

        // Create a list and add to lists of user
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
            _context.Entry(user).State = EntityState.Modified;
            if (await _context.FailedSavingChanges()) goto RETRY;

            return list.ToTrackListInfo();
        }

        [HttpGet("me/uploads")]
        public async Task<ActionResult> GetMeUploads()
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            return new JsonResult(new {
                tracks = await _context.Tracks.Where(t => t.owner == user.id).ToListAsync()
            });
        }

        [HttpGet("me/notes")]
        public async Task<ActionResult> GetNotes([FromQuery] int begin)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            return RenderComments("un/" + user.id);
        }

        [HttpPost("me/notes/new")]
        public async Task<ActionResult> PostNotes([FromBody] CommentVM vm)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            var comm = new Comment {
                tag = "un/" + user.id,
                uid = user.id,
                date = DateTime.UtcNow,
                content = vm.content
            };
            _context.Comments.Add(comm);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(PostNotes), comm.ToVM(user));
        }

        [HttpPut("me/notes/{id}")]
        public async Task<ActionResult> PutNotes([FromRoute] int id, [FromBody] CommentVM vm)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            if (id != vm.id) return GetErrorResult("bad_id");

            var comm = await _context.Comments.FindAsync(id);
            if (comm.uid != user.id || comm.tag != "un/" + user.id) return GetErrorResult("bad_comment");

            comm.content = vm.content;

            _context.Entry(comm).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return Ok(comm.ToVM(user));
        }

        [HttpDelete("me/notes/{id}")]
        public async Task<ActionResult> DeleteNotes([FromRoute] int id)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            var comm = await _context.Comments.FindAsync(id);
            if (comm.uid != user.id || comm.tag != "un/" + user.id) return GetErrorResult("bad_comment");

            _context.Comments.Remove(comm);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}

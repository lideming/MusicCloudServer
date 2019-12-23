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
        public async Task<ActionResult<UserGetVM>> GetUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            return await GetUser(user);
        }

        [HttpGet("me")]
        public async Task<ActionResult<UserGetVM>> GetUserMe()
        {
            var user = await GetLoginUser();
            return await GetUser(user);
        }

        private async Task<ActionResult<UserGetVM>> GetUser(User user)
        {
            if (user == null)
            {
                return GetErrorResult("user_not_found");
            }

            // get all needed lists in a single SQL query.
            var lists = await _context.Lists
                .Where(l => user.lists.Contains(l.id))
                .Select(l => l.ToTrackListInfo())
                .ToListAsync();

            // get the order right
            lists = user.lists.Select(id => lists.Find(l => l.id == id)).ToList();

            return new UserGetVM
            {
                id = user.id,
                username = user.username,
                // TODO: async
                lists = lists
            };
        }

        // PUT: api/users/me
        // update lists
        [HttpPut("me")]
        public async Task<ActionResult<UserGetVM>> PutUser(UserPutVM newState)
        {
            var user = await GetLoginUser();
            if (user == null || user.id != newState.id || user.username != newState.username
                || newState.listids == null)
            {
                return GetErrorResult("check_failed");
            }
            var listids = newState.listids;
            if (_context.Lists.Count(l => listids.Contains(l.id)) != listids.Count)
            {
                return GetErrorResult("list_not_found");
            }

            user.lists = listids;

            _context.Entry(user).State = EntityState.Modified;

            await _context.SaveChangesAsync();

            return await GetUser(user);
        }

        [HttpPost("new")]
        public async Task<ActionResult<UserGetVM>> PostUser(UserRegisterVM userreg)
        {
            if (string.IsNullOrEmpty(userreg.username))
                return GetErrorResult("bad_arg");
            if (_context.Users.Any(u => u.username == userreg.username))
                return GetErrorResult("dup_user");
            var user = new User
            {
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
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            var list = vm.ToList();
            list.owner = user.id;
            _context.Lists.Add(list);
            await _context.SaveChangesAsync();

            user.lists.Add(list.id);
            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return list.ToTrackListInfo();
        }

        // Create a list and add to lists of user
        [HttpDelete("me/lists/{id}")]
        public async Task<ActionResult<TrackListInfoVM>> DeleteMeList(int id)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            var list = await _context.Lists.FindAsync(id);
            if (list == null || list.owner != user.id) return GetErrorResult("list_not_found");
            _context.Lists.Remove(list);
            user.lists.Remove(list.id);
            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return list.ToTrackListInfo();
        }

        [HttpGet("me/uploads")]
        public async Task<ActionResult> GetMeUploads()
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            return new JsonResult(new
            {
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
        public async Task<ActionResult> PostNotes([FromQuery] int begin, [FromBody] CommentVM vm)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            var comm = new Comment
            {
                tag = "un/" + user.id,
                uid = user.id,
                date = DateTime.UtcNow,
                content = vm.content
            };
            _context.Comments.Add(comm);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(PostNotes), comm.ToVM());
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

            return Ok(comm.ToVM());
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

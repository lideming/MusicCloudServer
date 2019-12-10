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
    public class UsersController : ControllerBase
    {
        private readonly DbCtx _context;

        public UsersController(DbCtx context)
        {
            _context = context;
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
            var user = await _context.GetUser(HttpContext);
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
                .Select(l => l.GetTrackListInfo())
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
        public async Task<IActionResult> PutUser(UserPutVM newState)
        {
            var user = await _context.GetUser(HttpContext);
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

            return NoContent();
        }

        // Create a list and add to lists of user
        [HttpPost("me/lists/new")]
        public async Task<ActionResult<List>> PostMeList(List list)
        {
            var user = await _context.GetUser(HttpContext);
            if (user == null) return Forbid();

            _context.Lists.Add(list);
            await _context.SaveChangesAsync();

            user.lists.Add(list.id);
            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();


            return list;
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
                passwd = userreg.passwd,
                lists = new List<int>()
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return await GetUser(user.id);
        }

        //// DELETE: api/Users/5
        //[HttpDelete("{id}")]
        //public async Task<ActionResult<User>> DeleteUser(int id)
        //{
        //    var user = await _context.Users.FindAsync(id);
        //    if (user == null)
        //    {
        //        return NotFound();
        //    }

        //    _context.Users.Remove(user);
        //    await _context.SaveChangesAsync();

        //    return user;
        //}


        private ActionResult GetErrorResult(string error)
        {
            return new JsonResult(new
            {
                error = error
            })
            { StatusCode = 450 };
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.id == id);
        }
    }
}

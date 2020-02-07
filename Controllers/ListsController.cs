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
    [Route("api/lists")]
    [ApiController]
    public class ListsController : MyControllerBase
    {
        public ListsController(DbCtx context) : base(context)
        {
        }

        [HttpGet("index")]
        public async Task<ActionResult> GetIndex()
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            // should return all visible lists for the user
            var ret = new
            {
                lists = await _context.Lists
                    .Where(l => l.owner == user.id)
                    .Select(l => l.ToTrackListInfo())
                    .ToListAsync()
            };
            return new JsonResult(ret);
        }


        // GET: api/lists/5
        [HttpGet("{id}")]
        public async Task<ActionResult> GetList(int id)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            var list = await _context.Lists.FindAsync(id);
            if (list?.owner != user.id) return GetErrorResult("list_not_found");

            return RenderList(list);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutList(int id, ListPutVM vm)
        {
            if (id != vm.id)
            {
                return GetErrorResult("bad_request");
            }
            var user = await GetLoginUser();
            var list = await _context.Lists.FindAsync(id);
            if (list == null || user == null || list.owner != user.id) return GetErrorResult("list_not_found");

            var ids = vm.trackids;
            var foundTracks = await _context.Tracks.Where(x => ids.Contains(x.id)).ToListAsync();
            foundTracks = foundTracks.Where(x => x.IsVisibleToUser(user)).ToList();
            vm.trackids = vm.trackids.Where(x => foundTracks.Any(t => t.id == x)).ToList();

            vm.ApplyToList(list);

            _context.Entry(list).State = EntityState.Modified;

            await _context.SaveChangesAsync();

            return RenderList(list);
        }

        // POST: api/lists
        [HttpPost]
        public async Task<ActionResult<List>> PostList(ListPutVM vm)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");
            var list = vm.ToList();
            list.owner = user.id;
            _context.Lists.Add(list);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetList), new { id = list.id }, list.ToTrackListInfo());
        }

        // DELETE: api/lists/5
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteList(int id)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            var list = await _context.Lists.FindAsync(id);
            if (list == null || list.owner != user.id) return GetErrorResult("list_not_found");

            _context.Lists.Remove(list);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ListExists(int id)
        {
            return _context.Lists.Any(e => e.id == id);
        }
    }
}

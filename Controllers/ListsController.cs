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
            //if (user == null) return GetErrorResult("no_login");

            var list = await _context.Lists.FindAsync(id);
            if (list == null) return GetErrorResult("list_not_found");
            if (list.IsVisibleToUser(user)) return GetErrorResult("list_not_found");

            var tracks = _context.GetTracks(list.trackids)
                .Where(x => x.IsVisibleToUser(user)).ToList();

            return new JsonResult(new
            {
                id = list.id,
                name = list.name,
                tracks = tracks,
                version = list.version
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutList(int id, ListPutVM vm)
        {
            if (id != vm.id)
            {
                return GetErrorResult("bad_request");
            }
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");
            var list = await _context.Lists.FindAsync(id);
            if (list == null || list.owner != user.id) return GetErrorResult("list_not_found");
            if (vm.version != null && list.version != vm.version) goto LIST_CHANGED;

            var ids = vm.trackids;
            var foundTracks = await _context.Tracks.AsNoTracking().Where(x => ids.Contains(x.id)).ToListAsync();
            foundTracks = foundTracks.Where(x => x.IsVisibleToUser(user)).ToList();
            vm.trackids = vm.trackids.Where(x => foundTracks.Any(t => t.id == x)).ToList();

            vm.ApplyToList(list);
            list.version++;

            if(await _context.FailedSavingChanges()) goto LIST_CHANGED;

            return NoContent();
            LIST_CHANGED:
            return GetErrorResult("list_changed");
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

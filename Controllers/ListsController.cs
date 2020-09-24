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

            var lists = _context.Lists.Select(l => l.ToTrackListInfo());

            if (user.role != UserRole.SuperAdmin)
                lists = lists.Where(l => l.owner == user.id || l.visibility == Visibility.Public);

            // should return all visible lists for the user
            var ret = new
            {
                lists = await lists.ToListAsync()
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
            if (list?.IsVisibleToUser(user) != true) return GetErrorResult("list_not_found");

            var tracks = _context.GetTracks(list.trackids)
                .Where(x => x.IsVisibleToUser(user))
                .Select(x => TrackVM.FromTrack(x, _app, false))
                .ToList();

            return new JsonResult(new
            {
                id = list.id,
                owner = list.owner,
                name = list.name,
                visibility = list.visibility,
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

        [HttpGet("{id}/stat")]
        public async Task<ActionResult> GetStat([FromRoute] int id)
        {
            var user = await GetLoginUser();
            var list = await _context.Lists.FindAsync(id);
            if (list?.IsVisibleToUser(user) != true) return GetErrorResult("list_not_found");

            var plays = _context.Plays.Where(p => p.listid == list.id);

            return new JsonResult(new
            {
                playcount = await plays.CountAsync(),
                lastplay = (await plays.OrderBy(p => p.time).LastOrDefaultAsync())?.time
            });
        }

        private bool ListExists(int id)
        {
            return _context.Lists.Any(e => e.id == id);
        }
    }
}

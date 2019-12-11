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
            // should return all visible lists for the user
            var user = GetUser();
            var ret = new
            {
                lists = await _context.Lists.Select(l => new { l.id, l.name }).ToListAsync()
            };
            return new JsonResult(ret);
        }


        // GET: api/lists/5
        [HttpGet("{id}")]
        public async Task<ActionResult> GetList(int id)
        {
            var user = await GetUser();
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
            var user = await GetUser();
            var list = await _context.Lists.FindAsync(id);
            if (list == null || user == null || list.owner != user.id) return GetErrorResult("list_not_found");

            vm.ApplyToList(list);
            // TODO: check trackids

            _context.Entry(list).State = EntityState.Modified;

            await _context.SaveChangesAsync();

            return RenderList(list);
        }

        // POST: api/Lists
        [HttpPost]
        public async Task<ActionResult<List>> PostList(ListPutVM vm)
        {
            var user = await GetUser();
            if (user == null) return GetErrorResult("no_login");
            var list = vm.ToList();
            list.owner = user.id;
            _context.Lists.Add(list);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetList), new { id = list.id }, list.ToTrackListInfo());
        }

        // DELETE: api/ListsIList/5
        [HttpDelete("{id}")]
        public async Task<ActionResult<List>> DeleteList(int id)
        {
            var user = await GetUser();
            if (user == null) return GetErrorResult("no_login");

            var list = await _context.Lists.FindAsync(id);
            if (list == null || list.owner != user.id) return GetErrorResult("list_not_found");

            _context.Lists.Remove(list);
            await _context.SaveChangesAsync();

            return list;
        }

        private bool ListExists(int id)
        {
            return _context.Lists.Any(e => e.id == id);
        }
    }
}

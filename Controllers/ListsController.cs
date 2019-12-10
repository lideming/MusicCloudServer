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
    public class ListsController : ControllerBase
    {
        private readonly DbCtx _context;

        public ListsController(DbCtx context)
        {
            _context = context;
        }

        [HttpGet("index")]
        public async Task<ActionResult> GetIndex()
        {
            // should return all visible lists for the user
            var user = _context.GetUser(HttpContext);
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
            var list = await _context.Lists.FindAsync(id);

            return GetList(list);
        }

        private ActionResult GetList(List list)
        {
            if (list == null)
            {
                return NotFound();
            }

            return new JsonResult(new
            {
                id = list.id,
                name = list.name,
                tracks = list.trackids.Select(i => _context.Tracks.Find(i))
            });
        }

        // PUT: api/Lists/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        [HttpPut("{id}")]
        public async Task<IActionResult> PutList(int id, List list)
        {
            if (id != list.id)
            {
                return BadRequest();
            }

            _context.Entry(list).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ListExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return GetList(list);
        }

        // POST: api/Lists
        [HttpPost]
        public async Task<ActionResult<List>> PostList(List list)
        {
            _context.Lists.Add(list);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetList", new { id = list.id }, list);
        }

        // DELETE: api/Lists/5
        [HttpDelete("{id}")]
        public async Task<ActionResult<List>> DeleteList(int id)
        {
            var list = await _context.Lists.FindAsync(id);
            if (list == null)
            {
                return NotFound();
            }

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

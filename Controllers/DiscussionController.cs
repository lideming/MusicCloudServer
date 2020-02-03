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
    [Route("api/discussion")]
    [ApiController]
    public class DiscussionController : MyControllerBase {

        public DiscussionController(DbCtx context) : base(context)
        {
        }

        [HttpGet]
        public async Task<ActionResult> Index([FromQuery] int begin)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            return RenderComments("diss");
        }

        [HttpPost("new")]
        public async Task<ActionResult> PostComments([FromBody] CommentVM vm)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            var comm = new Comment {
                tag = "diss",
                uid = user.id,
                date = DateTime.UtcNow,
                content = vm.content
            };
            _context.Comments.Add(comm);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(PostComments), comm.ToVM(user));
        }

        [HttpPut("{id}")]
        public async Task<ActionResult> PutComments([FromRoute] int id, [FromBody] CommentVM vm)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            if (id != vm.id) return GetErrorResult("bad_id");

            var comm = await _context.Comments.FindAsync(id);
            if (comm.uid != user.id || comm.tag != "diss") return GetErrorResult("bad_comment");

            comm.content = vm.content;

            _context.Entry(comm).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return Ok(comm.ToVM(user));
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteComments([FromRoute] int id)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");

            var comm = await _context.Comments.FindAsync(id);
            if (comm.uid != user.id || comm.tag != "diss") return GetErrorResult("bad_comment");

            _context.Comments.Remove(comm);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

}
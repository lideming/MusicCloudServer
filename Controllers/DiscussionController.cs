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
            if (!IsEnabledForUser(user)) return GetErrorResult("discussion_disabled");

            return RenderComments("diss");
        }

        [HttpPost("new")]
        public async Task<ActionResult> PostComments([FromBody] CommentVM vm)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");
            if (!IsEnabledForUser(user)) return GetErrorResult("discussion_disabled");

            var comm = new Comment {
                tag = "diss",
                uid = user.id,
                date = DateTime.UtcNow,
                content = vm.content
            };
            _context.Comments.Add(comm);
            await _context.SaveChangesAsync();

            _message.TriggerEvent("diss-changed");

            return CreatedAtAction(nameof(PostComments), comm.ToVM(user));
        }

        [HttpPut("{id}")]
        public async Task<ActionResult> PutComments([FromRoute] int id, [FromBody] CommentVM vm)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");
            if (!IsEnabledForUser(user)) return GetErrorResult("discussion_disabled");

            if (id != vm.id) return GetErrorResult("bad_id");

            var comm = await _context.Comments.FindAsync(id);
            if (comm.uid != user.id || comm.tag != "diss") return GetErrorResult("bad_comment");

            comm.content = vm.content;

            // _context.Entry(comm).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            _message.TriggerEvent("diss-changed");

            return Ok(comm.ToVM(user));
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteComments([FromRoute] int id)
        {
            var user = await GetLoginUser();
            if (user == null) return GetErrorResult("no_login");
            if (!IsEnabledForUser(user)) return GetErrorResult("discussion_disabled");

            var comm = await _context.Comments.FindAsync(id);
            if (comm.tag != "diss" || !comm.IsWritableByUser(user)) return GetErrorResult("bad_comment");

            _context.Comments.Remove(comm);
            await _context.SaveChangesAsync();

            _message.TriggerEvent("diss-changed");

            return NoContent();
        }

        bool IsEnabledForUser(User user) => _app.Config.DiscussionEnabled || user.role == UserRole.SuperAdmin;
    }

}
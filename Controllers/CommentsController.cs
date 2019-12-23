using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MCloudServer;
using System.IO;
using System.Text;

namespace MCloudServer.Controllers
{
    [Route("api/comments")]
    [ApiController]
    public class CommentsController : MyControllerBase
    {
        public CommentsController(DbCtx context) : base(context)
        {
        }

        [HttpGet("discussion")]
        public ActionResult GetDiscussion()
        {
            return GetErrorResult("wip");
        }
    }
}
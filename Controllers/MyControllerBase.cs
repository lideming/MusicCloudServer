using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MCloudServer;
using Microsoft.Extensions.DependencyInjection;

namespace MCloudServer.Controllers
{
    public class MyControllerBase : ControllerBase
    {
        protected readonly DbCtx _context;

        private AppService __app;
        protected AppService _app => __app ?? (__app = this.HttpContext.RequestServices.GetService<AppService>());

        private MessageService __message;
        protected MessageService _message => __message ?? (__message = this.HttpContext.RequestServices.GetService<MessageService>());

        public MyControllerBase(DbCtx context)
        {
            _context = context;
        }

        protected Task<User> GetLoginUser() => _context.GetUser(HttpContext);

        protected ActionResult GetErrorResult(string error)
        {
            return new JsonResult(new
            {
                error = error
            })
            { StatusCode = 450 };
        }

        protected ActionResult GetErrorResult<T>(string error, T data)
        {
            return new JsonResult(new
            {
                error = error,
                data = data
            })
            { StatusCode = 450 };
        }

        protected ActionResult RenderComments(string tag)
        {
            var query = _context.Comments.Where(c => c.tag == tag);
            query = Request.Query["reverse"] != "1"
                ? query.OrderBy(c => c.id)
                : query.OrderByDescending(c => c.id);
            return new JsonResult(new
            {
                comments = query.Join(_context.Users, c => c.uid, u => u.id, (c, u) => c.ToVM(u))
            });
        }
    }
}
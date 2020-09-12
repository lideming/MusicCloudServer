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

        protected ErrorResult GetErrorResult(string error)
        {
            return new ErrorResult(error);
        }

        protected ErrorResult<T> GetErrorResult<T>(string error, T data)
        {
            return new ErrorResult<T>(error, data);
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

    public class ErrorResult<T> : ErrorResult
    {
        public ErrorResult(string error, T data) : base(new { error, data })
        {
            this.StatusCode = 450;
            this.Data = data;
        }

        public T Data { get; }
    }


    public class ErrorResult : JsonResult
    {
        public ErrorResult(string error) : this(new { error })
        {
        }

        internal ErrorResult(object value) : base(value)
        {
            this.StatusCode = 450;
        }
    }
}
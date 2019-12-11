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
    public class MyControllerBase : ControllerBase
    {
        protected readonly DbCtx _context;

        public MyControllerBase(DbCtx context)
        {
            _context = context;
        }

        protected Task<User> GetUser() => _context.GetUser(HttpContext);

        protected ActionResult GetErrorResult(string error)
        {
            return new JsonResult(new
            {
                error = error
            })
            { StatusCode = 450 };
        }

        protected ActionResult RenderList(List list)
        {
            if (list == null) return GetErrorResult("list_not_found");

            return new JsonResult(new
            {
                id = list.id,
                name = list.name,
                tracks = _context.GetTracks(list.trackids)
            });
        }
    }
}
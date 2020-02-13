using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SimplePasscode
{
    public class SimplePasscodeOptions
    {
        public string Passcode { get; set; }
        public string CookieName { get; set; } = "app_passcode";
        public string LoginPath { get; set; } = "/passcode";
        public string LoginPageFile { get; set; } = "passcode.html";
    }

    public class SimplePasscodeMiddleware
    {
        private readonly RequestDelegate next;
        private readonly ILogger<SimplePasscodeMiddleware> logger;
        private readonly SimplePasscodeOptions options;

        public SimplePasscodeMiddleware(SimplePasscodeOptions options,
            RequestDelegate next, ILogger<SimplePasscodeMiddleware> logger)
        {
            this.options = options;
            this.next = next;
            this.logger = logger;
        }

        public async Task InvokeAsync(HttpContext ctx)
        {
            var req = ctx.Request;
            var resp = ctx.Response;
            if (req.Method == "POST" && req.Path == options.LoginPath)
            {
                var form = await req.ReadFormAsync();
                if (form != null && form["passcode"] == options.Passcode)
                {
                    resp.Cookies.Append(options.CookieName, form["passcode"], new CookieOptions
                    {
                        Expires = DateTime.Now.AddDays(14)
                    });
                    resp.Redirect("/");
                    return;
                }
                else
                {
                    logger.LogWarning("Wrong passcode from {ip}:{port} {id}",
                        ctx.Connection.RemoteIpAddress, ctx.Connection.RemotePort, ctx.TraceIdentifier);
                }
            }
            else if (req.Method == "GET" && req.Path == options.LoginPath)
            {
                resp.StatusCode = 403;
                var accepts = req.Headers["Accept"];
                bool acceptHtml = accepts.Any(x => x.Split(',').Contains("text/html"));
                if (acceptHtml)
                {
                    resp.ContentType = "text/html";
                    using (var fs = File.OpenRead(options.LoginPageFile))
                    {
                        resp.ContentLength = fs.Length;
                        await fs.CopyToAsync(resp.Body);
                    }
                }
                else
                {
                    resp.ContentType = "text/plain";
                    await resp.Body.WriteAsync(Encoding.UTF8.GetBytes("passcode_missing"));
                }
                return;
            }
            resp.Redirect(options.LoginPath);
        }
    }

    public static class SimplePasscodeExtensions
    {
        public static void UsePasscode(this IApplicationBuilder app, SimplePasscodeOptions options)
        {
            app.UseWhen((ctx) => ctx.Request.Cookies[options.CookieName] != options.Passcode,
                app => app.UseMiddleware<SimplePasscodeMiddleware>(options));
        }
    }
}
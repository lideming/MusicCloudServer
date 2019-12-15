using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MCloudServer
{
    // The configration will be read from appsettings.json
    public class MCloudConfig
    {
        public DbType DbType { get; set; }
        // "dbtype": "pg" | "sqlite"
        // Thanks to EF Core, it can easily support two or more database engines.

        public string DbStr { get; set; }
        // "dbstr"
        // for SQLite, it's only the filename (i.e., without "Filename=").
        // for PostgreSQL, it's the connection string.

        public string StaticDir { get; set; }
        // "staticdir"
        // if exists, the server will serve static files

        public string StorageDir { get; set; }
        // "storagedir"
        // the directory to store track files

        public string Passcode { get; set; }
        // "passcode"
    }

    public enum DbType
    {
        SQLite,
        PostgreSQL
    }

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            var dbtype = Configuration["dbtype"];
            MyConfigration = new MCloudConfig
            {
                DbType = (dbtype == "pg") ? DbType.PostgreSQL :
                    (dbtype == "sqlite" || dbtype == null) ? DbType.SQLite :
                    throw new Exception($"unknown dbtype {dbtype}"),
                DbStr = Configuration["dbstr"],
                StaticDir = Configuration["staticdir"],
                StorageDir = Configuration["storagedir"] ?? "data/storage",
                Passcode = Configuration["passcode"]
            };
        }

        public IConfiguration Configuration { get; }

        public MCloudConfig MyConfigration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddSingleton(MyConfigration);
            services.AddDbContext<DbCtx>(options =>
            {
                if (MyConfigration.DbType == DbType.PostgreSQL)
                {
                    options.UseNpgsql(MyConfigration.DbStr ?? "Host=localhost;Database=testdb;Username=test;Password=test123");
                }
                else if (MyConfigration.DbType == DbType.SQLite)
                {
                    var fileName = MyConfigration.DbStr ?? "data/data.db";
                    Directory.CreateDirectory(Path.GetDirectoryName(fileName));
                    options.UseSqlite("Filename=" + fileName);
                }
                else
                {
                    throw new Exception("unknown DbType in MyConfigration");
                }
            });
            services.AddCors();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, DbCtx dbctx)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

            dbctx.Database.EnsureCreated();

            if (string.IsNullOrEmpty(MyConfigration.Passcode) == false)
            {
                ConfigurePasscode(app);
            }

            if (MyConfigration.StaticDir != null)
            {
                var fileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), MyConfigration.StaticDir));
                app.UseDefaultFiles(new DefaultFilesOptions
                {
                    FileProvider = fileProvider,
                    DefaultFileNames = new[] { "index.html" }
                });
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = fileProvider,
                });
            }

            if (string.IsNullOrEmpty(MyConfigration.StorageDir) == false)
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), MyConfigration.StorageDir);
                Directory.CreateDirectory(path);
                var fp = new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(path),
                    RequestPath = "/api/storage",
                    ServeUnknownFileTypes = true
                };
                app.UseStaticFiles(fp);
            }

            app.Use((ctx, next) =>
            {
                if (ctx.Request.Path.StartsWithSegments("/api/my", out var remaining))
                {
                    ctx.Request.Path = "/api/users/me" + remaining;
                }
                return next();
            });

            app.UseRouting();

            // app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private void ConfigurePasscode(IApplicationBuilder app)
        {
            app.UseWhen((ctx) => ctx.Request.Cookies["mcloud_passcode"] != MyConfigration.Passcode,
                app => app.Use(async (ctx, next) =>
                {
                    var req = ctx.Request;
                    var resp = ctx.Response;
                    if (req.Method == "POST" && req.Path == "/passcode")
                    {
                        var form = await req.ReadFormAsync();
                        if (form != null && form["passcode"] == MyConfigration.Passcode)
                        {
                            resp.Cookies.Append("mcloud_passcode", form["passcode"], new CookieOptions
                            {
                                Expires = DateTime.Now.AddDays(7)
                            });
                            resp.StatusCode = 302;
                            resp.Headers["Location"] = "/";
                            return;
                        }
                    }

                    resp.StatusCode = 403;
                    var accepts = req.Headers["Accept"];
                    bool acceptHtml = accepts.Any(x => x.Split(',').Contains("text/html"));
                    if (acceptHtml)
                    {
                        var pagePath = Path.Combine(Directory.GetCurrentDirectory(), "passcode.html");
                        resp.ContentType = "text/html";
                        using (var fs = File.OpenRead(pagePath))
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
                    await resp.CompleteAsync();
                }));
        }
    }
}

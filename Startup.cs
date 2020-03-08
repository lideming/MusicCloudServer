using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
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
using SimplePasscode;

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

        public string StorageUrlBase { get; set; }

        public string StorageArg { get; set; }

        public string Passcode { get; set; }
        // "passcode"

        public List<Converter> Converters { get; set; } = new List<Converter>();

        public bool ConverterDebug { get; set; }

        public bool TryResolveStoragePath(string prefixedPath, out string fsPath)
        {
            if (TryGetStoragePath(prefixedPath, out fsPath))
            {
                fsPath = Path.Combine(StorageDir, fsPath);
                return true;
            }
            return false;
        }

        public bool TryGetStoragePath(string prefixedPath, out string relPath)
        {
            if (prefixedPath.StartsWith("storage/"))
            {
                relPath = prefixedPath.Substring("storage/".Length);
                return true;
            }
            relPath = null;
            return false;
        }

        public string ResolveStoragePath(string storagePath)
        {
            if (!TryResolveStoragePath(storagePath, out var r))
                throw new Exception($"Failed to resolve storage path '{storagePath}'");
            return r;
        }

        public string GetStoragePath(string storagePath)
        {
            if (!TryGetStoragePath(storagePath, out var r))
                throw new Exception($"Failed to get storage path '{storagePath}'");
            return r;
        }

        public Converter FindConverter(string name) => Converters.Find(x => x.Name == name);

        public class Converter
        {
            public string Name { get; set; }
            public string Format { get; set; }
            public int Bitrate { get; set; }
            public string CommandLine { get; set; }

            public string GetCommandLine(string inputFile, string outputFile)
                => string.Format(CommandLine, inputFile, outputFile);
        }
    }

    public enum DbType
    {
        SQLite = 0,
        PostgreSQL = 1,
        Pg = 1,
    }

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            MyConfigration = configuration.Get<MCloudConfig>();
            var dbtype = MyConfigration.DbType;
        }

        public IConfiguration Configuration { get; }

        public MCloudConfig MyConfigration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddSingleton<AppService>();
            services.AddSingleton(MyConfigration);
            services.AddSingleton<MessageService>();
            services.AddSingleton<ConvertService>();
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
            services.AddScoped<UserService>();
            services.AddAuthentication("UserAuth")
                .AddScheme<AuthenticationSchemeOptions, UserService.AuthHandler>("UserAuth", null);
            if (MyConfigration.StorageArg?.StartsWith("qcloud:") == true)
            {
                services.AddSingleton<StorageService>(
                    new QcloudStorageService(MyConfigration.StorageArg.Split(':')));
            }
            else
            {
                services.AddSingleton<StorageService>(new LocalStorageService());
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, DbCtx dbctx, ILogger<Startup> logger)
        {
            app.UseForwardedHeaders();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

            dbctx.Database.Migrate();
            AppMigrate(dbctx, logger);

            if (string.IsNullOrEmpty(MyConfigration.Passcode) == false)
            {
                app.UsePasscode(new SimplePasscodeOptions
                {
                    CookieName = "mcloud_passcode",
                    Passcode = MyConfigration.Passcode,
                    Filter = ctx => !ctx.Request.Path.StartsWithSegments("/api/storage")
                                    && !ctx.Request.Path.StartsWithSegments("/.well-known")
                });
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
                    ServeUnknownFileTypes = true
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
                app.Use(async (ctx, next) =>
                {
                    if (ctx.Request.Path.StartsWithSegments("/api/storage"))
                    {
                        ctx.Response.Headers.Add("Cache-Control", "public");
                    }
                    await next();
                });
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

            app.UseWebSockets();

            app.Use((next) => async (ctx) =>
            {
                if (ctx.Request.Path == "/api/ws" && ctx.WebSockets.IsWebSocketRequest)
                {
                    var ws = await ctx.WebSockets.AcceptWebSocketAsync();
                    await ctx.RequestServices.GetService<MessageService>().HandleWebSocket(ws);
                }
                else
                {
                    await next(ctx);
                }
            });

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private void AppMigrate(DbCtx dbctx, ILogger logger)
        {
            dbctx.GetConfig("appver").ContinueWith(async (task) =>
            {
                var val = await task;
                var origVal = val;
                if (val == null)
                {
                    val = "1";
                }
                if (val == "1")
                {
                    int count = 0;
                    foreach (var item in dbctx.Tracks.AsNoTracking())
                    {
                        try
                        {
                            if (item.TryGetStoragePath(MyConfigration, out var path))
                            {
                                item.size = (int)new FileInfo(path).Length;
                                dbctx.Entry(item).State = EntityState.Modified;
                                if (++count % 100 == 0) dbctx.SaveChanges();
                            }
                        }
                        catch (System.Exception ex)
                        {
                            logger.LogWarning(ex, "getting file length from track id {id}", item.id);
                        }
                    }
                    dbctx.SaveChanges();
                    logger.LogInformation("saved file length for {count} files", count);
                    val = "2";
                }
                if (val != origVal)
                {
                    logger.LogInformation("appver changed from {orig} to {val}", origVal, val);
                    await dbctx.SetConfig("appver", val);
                }
            }).Wait();
        }

    }
}

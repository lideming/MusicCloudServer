using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MCloudServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimplePasscode;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.AddServerHeader = false;
});

var myConfig = builder.Configuration.Get<MCloudConfig>();

var services = builder.Services;

// Configure services
services.AddControllers();
services.AddSingleton<AppService>();
services.AddSingleton(myConfig);
services.AddSingleton<MessageService>();
services.AddSingleton<ConvertService>();
services.AddDbContext<DbCtx>(options =>
{
    if (myConfig.DbType == DbType.PostgreSQL)
    {
        options.UseNpgsql(myConfig.DbStr ?? throw new Exception("DbStr is required for PostgreSQL"));
    }
    else if (myConfig.DbType == DbType.SQLite)
    {
        var fileName = myConfig.DbStr ?? "data/data.db";
        Directory.CreateDirectory(Path.GetDirectoryName(fileName));
        options.UseSqlite("Filename=" + fileName);
    }
    else
    {
        throw new Exception("unknown DbType in app configration");
    }
});
services.AddCors();
services.AddScoped<UserService>();
services.AddAuthentication("UserAuth")
    .AddScheme<AuthenticationSchemeOptions, UserService.AuthHandler>("UserAuth", null);
services.AddSingleton<StorageService>(new LocalStorageService());
services.AddSingleton<FileService>();
if (!string.IsNullOrEmpty(myConfig.ForwardedFrom)) {
    services.Configure<ForwardedHeadersOptions>(options => {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Add(IPNetwork.Parse(myConfig.ForwardedFrom));
    });
}

var app  = builder.Build();
var env = app.Environment;

// Preparing the database
using (var scope = app.Services.CreateScope())
{
    var dbctx = scope.ServiceProvider.GetService<DbCtx>();
    var logger = scope.ServiceProvider.GetService<ILogger<Program>>();
    var appService = scope.ServiceProvider.GetService<AppService>();
    var fileService = scope.ServiceProvider.GetService<FileService>();
    await dbctx.Database.MigrateAsync();
    await MigrationService.AppMigrate(appService, dbctx, logger, fileService);
    await AppCheckFirstRun(appService, dbctx, logger);
}

// Configure middlewares
app.Use((ctx, next) => {
    ctx.Response.Headers.Add("Server", "MusicCloudServer");
    return next();
});
app.UseForwardedHeaders();

if (env.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseCors(builder => 
    builder
        .AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()
        .WithExposedHeaders(["x-mcloud-store-fields"])
);

if (string.IsNullOrEmpty(myConfig.Passcode) == false)
{
    app.UsePasscode(new SimplePasscodeOptions
    {
        CookieName = "mcloud_passcode",
        Passcode = myConfig.Passcode,
        Filter = ctx => !ctx.Request.Path.StartsWithSegments("/api/storage")
                        && !ctx.Request.Path.StartsWithSegments("/.well-known")
    });
}

if (myConfig.StaticDir != null)
{
    var fileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), myConfig.StaticDir));
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

if (string.IsNullOrEmpty(myConfig.StorageDir) == false)
{
    string path = Path.Combine(Directory.GetCurrentDirectory(), myConfig.StorageDir);
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
            ctx.Response.Headers.Add("Cache-Control", "public, max-age=31536000, immutable");
            await next();
            if (ctx.Response.StatusCode >= 300 && !ctx.Response.HasStarted) {
                ctx.Response.Headers.Remove("Cache-Control");
            }
        } else {
            await next();
        }
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

app.Use(async (ctx, next) =>
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

app.Run();

async Task AppCheckFirstRun(AppService appService, DbCtx dbctx, ILogger logger)
{
    if (await dbctx.Users.CountAsync() == 0) {
        logger.LogInformation("No user is found, creating the default \"admin\" user.");
        var user = new User{
            username = "admin",
            passwd = Utils.HashPassword("admin"),
            lists = new List<int>(),
            role = UserRole.SuperAdmin
        };
        dbctx.Users.Add(user);
        await dbctx.SaveChangesAsync();
        dbctx.Entry(user).State = EntityState.Detached;
    }
}
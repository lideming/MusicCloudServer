using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MCloudServer
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddDbContext<DbCtx>(options =>
            {
                if (Configuration["dbtype"] == "pg")
                {
                    // "Host=localhost;Database=testdb;Username=test;Password=test123"
                    options.UseNpgsql(Configuration["dbstr"]);
                }
                else
                {
                    var fileName = Configuration["dbstr"] ?? "data/data.db";
                    Directory.CreateDirectory(Path.GetDirectoryName(fileName));
                    options.UseSqlite("Filename=" + fileName);
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

            //using (var scope = app.ApplicationServices.CreateScope())
            //{
            //    var ctx = scope.ServiceProvider.GetService<DbCtx>();
            //    ctx.Database.EnsureCreated();
            //}

            dbctx.Database.EnsureCreated();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}

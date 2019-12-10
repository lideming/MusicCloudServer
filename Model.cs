using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCloudServer
{
    public class DbCtx : DbContext
    {
        public DbCtx(DbContextOptions<DbCtx> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<List> Lists { get; set; }
        public DbSet<Track> Tracks { get; set; }

        // protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        //     => optionsBuilder.UseNpgsql("Host=localhost;Database=testdb;Username=test;Password=test123");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<List>().ToTable("lists")
                .Property(l => l.trackids)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(str => int.Parse(str)).ToList());
            modelBuilder.Entity<User>().ToTable("users").HasIndex(u => u.username).IsUnique();
            modelBuilder.Entity<Track>().ToTable("tracks");
            modelBuilder.Entity<User>()
                .Property(u => u.lists)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(str => int.Parse(str)).ToList());
        }

        /// <summary>
        /// Check authorization info in the request. Return the user or null.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public async Task<User> GetUser(HttpContext ctx)
        {
            var auth = ctx.Request.Headers["Authorization"];
            if (auth.Count != 1) return null;
            var splits = auth[0].Split(' ');
            if (splits.Length != 2) return null;
            var kv = Encoding.UTF8.GetString(Convert.FromBase64String(splits[1])).Split(':');
            if (kv.Length < 2) return null;
            var username = kv[0];
            var passwd = kv[1];
            var user = await Users.FirstOrDefaultAsync(u => u.username == username);
            if (user == null || user.passwd != passwd) return null;
            return user;
        }
    }

    public class User
    {
        [Key]
        public int id { get; set; }
        public string username { get; set; }
        public UserRole role { get; set; }
        public List<int> lists { get; set; }
        public string passwd { get; set; }
    }

    public enum UserRole
    {
        User = 1,
        SuperAdmin = 255
    }

    public class UserRegisterVM
    {
        public int id { get; set; }
        public string username { get; set; }
        public string passwd { get; set; }
    }

    public class UserGetVM
    {
        public int id { get; set; }
        public string username { get; set; }
        public List<TrackListInfoVM> lists { get; set; }
    }
    public class UserPutVM
    {
        public int id { get; set; }
        public string username { get; set; }
        public List<int> listids { get; set; }
    }

    public class List
    {
        [Key]
        public int id { get; set; }
        public string name { get; set; }
        public List<int> trackids { get; set; }

        public TrackListInfoVM GetTrackListInfo() => new TrackListInfoVM { id = id, name = name };
    }

    public class TrackListInfoVM
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class Track
    {
        [Key]
        public int id { get; set; }
        public string name { get; set; }
        public string artist { get; set; }
        public string url { get; set; }
    }
}

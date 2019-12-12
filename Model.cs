using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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
        public DbCtx(DbContextOptions<DbCtx> options, MCloudConfig mCloudConfig) : base(options)
        {
            MCloudConfig = mCloudConfig;
        }

        public DbSet<User> Users { get; set; }
        public DbSet<List> Lists { get; set; }
        public DbSet<Track> Tracks { get; set; }
        public MCloudConfig MCloudConfig { get; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<List>().ToTable("lists");
            modelBuilder.Entity<User>().ToTable("users").HasIndex(u => u.username).IsUnique();
            modelBuilder.Entity<Track>().ToTable("tracks");

            // Workaround for SQLite:
            if (MCloudConfig.DbType == DbType.SQLite)
            {
                ApplyListConversion(modelBuilder.Entity<User>().Property(u => u.lists));
                ApplyListConversion(modelBuilder.Entity<List>().Property(l => l.trackids));
            }
        }

        private static void ApplyListConversion(PropertyBuilder<List<int>> prop)
        {
            prop.HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(str => int.Parse(str)).ToList());
        }

        private static Func<DbCtx, string, Task<User>> findUser
            = EF.CompileAsyncQuery((DbCtx db, string username) =>
                db.Users.FirstOrDefault(u => u.username == username)
            );

        /// <summary>
        /// Check authorization info in the request. Return the user or null.
        /// </summary>
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
            var user = await findUser(this, username);
            if (user == null || user.passwd != passwd) return null;
            return user;
        }

        public IEnumerable<Track> GetTracks(IEnumerable<int> trackids)
        {
            return trackids.Select(i => Tracks.Find(i));
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

    // VM means View Model

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
        public int owner { get; set; }
        public string name { get; set; }
        public List<int> trackids { get; set; }

        public TrackListInfoVM ToTrackListInfo() => new TrackListInfoVM { id = id, name = name };
    }

    public class ListPutVM
    {
        public int id { get; set; }
        public string name { get; set; }
        public List<int> trackids { get; set; }

        public List ToList() => ApplyToList(new List());

        public List ApplyToList(List list){
            list.id = id;
            list.name = name;
            list.trackids = trackids;
            return list;
        }
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
        public int owner { get; set; } // the id of user that uploads this track
        public string name { get; set; }
        public string artist { get; set; }
        public string url { get; set; }
    }

    public class TrackVM
    {
        public int id { get; set; }
        public string name { get; set; }
        public string artist { get; set; }
        public string url { get; set; }

        public static TrackVM FromTrack(Track t)
        {
            return new TrackVM
            {
                id = t.id,
                name = t.name,
                artist = t.artist,
                url = t.url,
            };
        }
    }
}

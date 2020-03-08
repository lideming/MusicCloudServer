using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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

        public MCloudConfig MCloudConfig { get; }

        public DbSet<User> Users { get; set; }
        public DbSet<List> Lists { get; set; }
        public DbSet<Track> Tracks { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<LoginRecord> Logins { get; set; }

        public UserService UserService { get; set; }
        public bool IsLogged => UserService.IsLogged;
        public User User => UserService.User;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<List>().ToTable("lists");
            modelBuilder.Entity<User>().ToTable("users").HasIndex(u => u.username).IsUnique();
            modelBuilder.Entity<Track>().ToTable("tracks");
            modelBuilder.Entity<Comment>().ToTable("comments").HasIndex(c => c.tag);
            modelBuilder.Entity<ConfigItem>().ToTable("config");
            modelBuilder.Entity<LoginRecord>().ToTable("logins");

            // Workaround for SQLite:
            if (MCloudConfig.DbType == DbType.SQLite) {
                ApplyListConversion(modelBuilder.Entity<User>().Property(u => u.lists));
                ApplyListConversion(modelBuilder.Entity<List>().Property(l => l.trackids));
            }
        }

        private static void ApplyListConversion(PropertyBuilder<List<int>> prop)
        {
            prop.HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(str => int.Parse(str)).ToList());
            prop.Metadata.SetValueComparer(new ValueComparer<List<int>>(
                (a, b) => a.SequenceEqual(b),
                v => v.GetHashCode(),
                v => v.ToList()
            ));
        }

        public Task<User> FindUser(string username) => findUser(this, username);
        private static Func<DbCtx, string, Task<User>> findUser
            = EF.CompileAsyncQuery((DbCtx db, string username) =>
                db.Users.FirstOrDefault(u => u.username == username)
            );

        public Task<LoginRecord> FindLogin(string token) => findLogin(this, token);
        private static Func<DbCtx, string, Task<LoginRecord>> findLogin
            = EF.CompileAsyncQuery((DbCtx db, string token) =>
                db.Logins.Include(l => l.User).FirstOrDefault(l => l.token == token)
            );

        /// <summary>
        /// Check authorization info in the request. Return the user or null.
        /// </summary>
        public Task<User> GetUser(HttpContext ctx)
        {
            return Task.FromResult(User);
        }

        public IEnumerable<Track> GetTracks(IEnumerable<int> trackids)
        {
            var ids = trackids.Distinct().ToList();
            var tracks = Tracks.Where(x => ids.Contains(x.id)).ToList();
            return trackids.Select(i => tracks.FirstOrDefault(x => x.id == i)).Where(x => x != null);
        }

        public async Task ChangeAndAutoRetry(Func<Task> func)
        {
            retry:
            await func();
            try {
                await this.SaveChangesAsync();
            } catch (DbUpdateConcurrencyException ex) {
                foreach (var item in ex.Entries) {
                    await item.ReloadAsync();
                }
                goto retry;
            }
        }

        public async Task<bool> FailedSavingChanges()
        {
            try {
                await this.SaveChangesAsync();
            } catch (DbUpdateConcurrencyException ex) {
                foreach (var item in ex.Entries) {
                    await item.ReloadAsync();
                }
                return true;
            }
            return false;
        }

        public async Task<string> GetConfig(string key)
        {
            var item = await this.FindAsync<ConfigItem>(key);
            return item?.Value;
        }

        public async Task SetConfig(string key, string value)
        {
            var item = await this.FindAsync<ConfigItem>(key);
            if (item == null) {
                item = new ConfigItem { Key = key, Value = value };
                this.Add(item);
            } else {
                item.Value = value;
            }
            await this.SaveChangesAsync();
        }
    }

    public class ConfigItem
    {
        [Key]
        public string Key { get; set; }
        public string Value { get; set; }
    }

    public class User
    {
        [Key]
        public int id { get; set; }
        public string username { get; set; }
        public UserRole role { get; set; }
        public List<int> lists { get; set; }
        public string passwd { get; set; }
        public string last_playing { get; set; }

        [ConcurrencyCheck]
        public int version { get; set; }
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
        public string passwd { get; set; }
        public List<int> listids { get; set; }
    }

    public class LoginRecord
    {
        [Key]
        public string token { get; set; }
        public DateTime login_date { get; set; }
        public DateTime last_used { get; set; }

        public int UserId { get; set; }
        public User User { get; set; }
    }

    public class List
    {
        [Key]
        public int id { get; set; }
        public int owner { get; set; }
        public string name { get; set; }

        public List<int> trackids { get; set; }

        public int version { get; set; }

        public TrackListInfoVM ToTrackListInfo() => new TrackListInfoVM { id = id, name = name };
    }

    public static class ListExtensions
    {
        public static IQueryable<TrackListInfoVM> ToTrackListInfo(this IQueryable<List> lists)
        {
            return lists.Select(l => new TrackListInfoVM { id = l.id, name = l.name });
        }
    }

    public class ListPutVM
    {
        public int id { get; set; }
        public string name { get; set; }
        public List<int> trackids { get; set; }

        public List ToList() => ApplyToList(new List());

        public List ApplyToList(List list)
        {
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
        public int size { get; set; }

        public string lyrics { get; set; }

        public bool TryGetStoragePath(AppService app, out string path)
        {
            if (this.url.StartsWith("storage/")) {
                path = Path.Combine(app.Config.StorageDir, this.url.Substring("storage/".Length));
                return true;
            }
            path = null;
            return false;
        }

        public void DeleteFile(AppService app)
        {
            if (TryGetStoragePath(app, out var path)) {
                File.Delete(path);
            }
            if (app.StorageService.Mode != StorageMode.Direct) {
                app.StorageService.DeleteFile(url.Substring("storage/".Length));
            }
        }

        public void ReadTrackInfoFromFile(AppService app)
        {
            if (TryGetStoragePath(app, out var path)) {
                var info = new ATL.Track(path);
                var slash = url.LastIndexOf('/');
                var dot = url.LastIndexOf('.');
                if (info.Title != url.Substring(slash + 1, dot - slash - 1)) {
                    this.artist = info.Artist;
                    this.name = info.Title;
                }
            }
        }

        public bool IsVisibleToUser(User user)
            => user.role == UserRole.SuperAdmin || user.id == this.owner;
        public bool IsWritableByUser(User user)
            => user.role == UserRole.SuperAdmin || user.id == this.owner;
    }

    public class TrackVM
    {
        public int id { get; set; }
        public string name { get; set; }
        public string artist { get; set; }
        public string url { get; set; }

        public string lyrics { get; set; }

        public static TrackVM FromTrack(Track t)
        {
            return new TrackVM {
                id = t.id,
                name = t.name,
                artist = t.artist,
                url = t.url,
                lyrics = t.lyrics,
            };
        }
    }

    public class Comment
    {
        public int id { get; set; }
        public int uid { get; set; }

        [StringLength(20)]
        public string tag { get; set; }
        // like "g", "l/5" or "u/5"

        public DateTime date { get; set; }

        public string content { get; set; }

        public CommentVM ToVM(User owner) => new CommentVM {
            id = this.id,
            uid = this.uid,
            username = owner == null ? "uid" + this.uid : owner.username,
            date = new DateTime(this.date.Ticks, DateTimeKind.Utc),
            content = this.content
        };

        public bool IsWritableByUser(User user)
            => user.role == UserRole.SuperAdmin || user.id == this.uid;
    }

    public class CommentVM
    {
        public int id { get; set; }
        public int uid { get; set; }
        public string username { get; set; }

        public DateTime date { get; set; }

        public string content { get; set; }
    }

    public class TrackLocation
    {
        public int listid { get; set; }
        public int position { get; set; }
        public int trackid { get; set; }

        public override string ToString()
        {
            return $"{listid}/{position}/{trackid}";
        }

        public static TrackLocation Parse(string str)
        {
            if (string.IsNullOrEmpty(str)) return new TrackLocation();
            var splits = str.Split('/');
            return new TrackLocation {
                listid = int.Parse(splits[0]),
                position = int.Parse(splits[1]),
                trackid = int.Parse(splits[2])
            };
        }
    }
}

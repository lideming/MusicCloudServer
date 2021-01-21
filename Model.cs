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
using System.Text.Json;
using System.Threading.Tasks;

namespace MCloudServer
{
    public class DbCtx : DbContext
    {
        public DbCtx(DbContextOptions<DbCtx> options, AppService app) : base(options)
        {
            MCloudConfig = app.Config;
            App = app;
        }

        public AppService App { get; }
        public MCloudConfig MCloudConfig { get; }

        public DbSet<User> Users { get; set; }
        public DbSet<List> Lists { get; set; }
        public DbSet<Track> Tracks { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<LoginRecord> Logins { get; set; }
        public DbSet<PlayRecord> Plays { get; set; }

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

            modelBuilder.Entity<PlayRecord>().ToTable("plays");
            modelBuilder.Entity<PlayRecord>().HasIndex(p => p.uid);
            modelBuilder.Entity<PlayRecord>().HasIndex(p => p.trackid);
            modelBuilder.Entity<PlayRecord>().HasIndex(p => p.listid);
            modelBuilder.Entity<PlayRecord>().HasIndex(p => p.audioprofile);
            modelBuilder.Entity<PlayRecord>().HasIndex(p => p.time);
            modelBuilder.Entity<PlayRecord>().HasOne(p => p.Track).WithMany().HasForeignKey(p => p.trackid);
            modelBuilder.Entity<PlayRecord>().HasOne(p => p.User).WithMany().HasForeignKey(p => p.uid);

            // Workaround for SQLite:
            if (MCloudConfig.DbType == DbType.SQLite)
            {
                ApplyListConversion(modelBuilder.Entity<User>().Property(u => u.lists));
                ApplyListConversion(modelBuilder.Entity<List>().Property(l => l.trackids));
            }

            ApplyListConversion(modelBuilder.Entity<Track>().Property(t => t.files));
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

        private static void ApplyListConversion<T>(PropertyBuilder<List<T>> prop) where T : ICloneable
        {
            prop.HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, null),
                v => string.IsNullOrEmpty(v) ? new List<T>() : JsonSerializer.Deserialize<List<T>>(v, null));
            prop.Metadata.SetValueComparer(new ValueComparer<List<T>>(
                (a, b) => a.SequenceEqual(b),
                v => v.GetHashCode(),
                v => v.Select(x => (T)x.Clone()).ToList()
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

        public IEnumerable<TrackVM> GetTrackVMs(IEnumerable<int> trackids)
        {
            return GetTracks(trackids).Select(x => TrackVM.FromTrack(x, App, false));
        }

        public IEnumerable<Track> GetTracks(IEnumerable<int> trackids)
        {
            var ids = trackids.Distinct().ToList();
            var tracks = Tracks.Where(x => ids.Contains(x.id)).ToList();
            return trackids.Select(i => tracks.FirstOrDefault(x => x.id == i))
                .Where(x => x != null);
        }

        public async Task ChangeAndAutoRetry(Func<Task> func)
        {
        retry:
            await func();
            try
            {
                await this.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                foreach (var item in ex.Entries)
                {
                    await item.ReloadAsync();
                }
                goto retry;
            }
        }

        public async Task<bool> FailedSavingChanges()
        {
            try
            {
                await this.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                foreach (var item in ex.Entries)
                {
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
            if (item == null)
            {
                item = new ConfigItem { Key = key, Value = value };
                this.Add(item);
            }
            else
            {
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

        public bool AllowFileUploadSize(long size)
        {
            if (role == UserRole.SuperAdmin) return true;
            return size <= 256 * 1024 * 1024;
        }
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

    public class List : IOwnership
    {
        [Key]
        public int id { get; set; }
        public int owner { get; set; }
        public string name { get; set; }

        public List<int> trackids { get; set; }

        public Visibility visibility { get; set; }

        [ConcurrencyCheck]
        public int version { get; set; }

        public TrackListInfoVM ToTrackListInfo() => new TrackListInfoVM(this);
    }

    public static class ListExtensions
    {
        public static IQueryable<TrackListInfoVM> ToTrackListInfo(this IQueryable<List> lists)
        {
            return lists.Select(l => new TrackListInfoVM { id = l.id, owner = l.owner, name = l.name, visibility = l.visibility });
        }
    }

    public class ListPutVM
    {
        public int id { get; set; }
        public string name { get; set; }
        public List<int> trackids { get; set; }
        public Visibility? visibility { get; set; }
        public int? version { get; set; }

        public List ToList() => ApplyToList(new List());

        public List ApplyToList(List list)
        {
            list.id = id;
            list.name = name;
            if (visibility != null) list.visibility = visibility.Value;
            if (trackids != null) list.trackids = trackids;
            return list;
        }
    }

    public class TrackListInfoVM
    {
        public int id { get; set; }
        public int owner { get; set; }
        public string name { get; set; }
        public Visibility visibility { get; set; }

        public TrackListInfoVM() { }

        public TrackListInfoVM(List list)
        {
            id = list.id;
            owner = list.owner;
            name = list.name;
            visibility = list.visibility;
        }
    }

    public class Track : IOwnership
    {
        [Key]
        public int id { get; set; }
        public int owner { get; set; } // the id of user that uploads this track
        public Visibility visibility { get; set; }
        public string name { get; set; }
        public string artist { get; set; }
        public string url { get; set; }
        public int size { get; set; }
        public int length { get; set; }

        [ConcurrencyCheck]
        public int version { get; set; }

        public string lyrics { get; set; }

        public List<TrackFile> files { get; set; }

        public bool TryGetStoragePath(AppService app, out string path)
            => app.Config.TryResolveStoragePath(this.url, out path);

        public string ConvUrl(string conv)
            => url + "." + conv;

        public void DeleteFile(AppService app)
        {
            if (TryGetStoragePath(app, out var path))
            {
                File.Delete(path);
            }
            if (app.StorageService.Mode != StorageMode.Direct)
            {
                app.StorageService.DeleteFile(app.Config.GetStoragePath(url));
            }
            if (files != null)
            {
                foreach (var item in files)
                {
                    if (app.Config.TryResolveStoragePath(ConvUrl(item.ConvName), out var fpath))
                    {
                        File.Delete(fpath);
                    }
                    if (app.StorageService.Mode != StorageMode.Direct)
                    {
                        app.StorageService.DeleteFile(app.Config.GetStoragePath(ConvUrl(item.ConvName)));
                    }
                }
            }
        }

        public void ReadTrackInfoFromFile(AppService app)
        {
            if (TryGetStoragePath(app, out var path))
            {
                var info = new ATL.Track(path);
                var slash = url.LastIndexOf('/');
                var dot = url.LastIndexOf('.');
                this.artist = info.Artist;
                if (info.Title != url.Substring(slash + 1, dot - slash - 1))
                {
                    this.name = info.Title;
                }
                if (info.Duration != 0)
                {
                    this.length = info.Duration;
                }
            }
        }
    }

    public enum Visibility
    {
        Private = 0,
        Public = 1
    }

    public interface IOwnership
    {
        Visibility visibility { get; }
        int owner { get; }
    }

    public static class OwnershipExtensions
    {
        public static bool IsVisibleToUser(this IOwnership thiz, User user)
            => thiz.visibility == Visibility.Public || IsWritableByUser(thiz, user);

        public static bool IsWritableByUser(this IOwnership thiz, User user)
            => user != null && (user.role == UserRole.SuperAdmin || user.id == thiz.owner);
    }

    public class TrackFile : ICloneable
    {
        public string ConvName { get; set; }
        public string Format { get; set; }
        public int Bitrate { get; set; }
        public long Size { get; set; }

        public TrackFile Clone() => base.MemberwiseClone() as TrackFile;
        object ICloneable.Clone() => this.Clone();

        public override bool Equals(object obj)
            => obj is TrackFile t
                && ConvName == t.ConvName
                && Format == t.Format;

        public override int GetHashCode()
            => HashCode.Combine(ConvName, Format);
    }

    public class TrackVM
    {
        public int id { get; set; }
        public string name { get; set; }
        public string artist { get; set; }
        public string url { get; set; }
        public int size { get; set; }
        public int length { get; set; }
        public int owner { get; set; }
        public Visibility? visibility { get; set; }
        public int? version { get; set; }

        public string lyrics { get; set; }

        public List<TrackFileVM> files { get; set; }

        public static TrackVM FromTrack(Track t, AppService app, bool withLyrics = false)
        {
            var vm = new TrackVM
            {
                id = t.id,
                name = t.name,
                artist = t.artist,
                url = t.url,
                size = t.size,
                length = t.length,
                owner = t.owner,
                visibility = t.visibility,
                lyrics = string.IsNullOrEmpty(t.lyrics) ? "" : withLyrics ? t.lyrics : null,
                version = t.version
            };
            if (app.Config.Converters?.Count > 0 || t.files?.Count > 0)
            {
                var origBitrate = t.length > 0 ? t.size / t.length / 128 : 0;
                vm.files = new List<TrackFileVM>();
                vm.files.Add(new TrackFileVM
                {
                    bitrate = origBitrate,
                    format = t.url.Contains('.') ? t.url.Substring(t.url.IndexOf('.') + 1) : "",
                    profile = "",
                    size = vm.size
                });
                if (t.files != null)
                {
                    foreach (var item in t.files)
                    {
                        vm.files.Add(new TrackFileVM(item));
                    }
                }
                if (app.Config.Converters != null)
                {
                    foreach (var item in app.Config.Converters)
                    {
                        if (origBitrate / 2 < item.Bitrate) continue;
                        if (t.files?.Any(x => x.ConvName == item.Name) == true) continue;
                        vm.files.Add(new TrackFileVM
                        {
                            bitrate = item.Bitrate,
                            format = item.Format,
                            profile = item.Name,
                            size = -1
                        });
                    }
                }
            }
            return vm;
        }
    }

    public class TrackFileVM
    {
        public string profile { get; set; }
        public long size { get; set; }
        public string format { get; set; }
        public int bitrate { get; set; }

        public TrackFileVM() { }

        public TrackFileVM(TrackFile f)
        {
            profile = f.ConvName;
            format = f.Format;
            bitrate = f.Bitrate;
            size = f.Size;
        }
    }

    public class PlayRecord
    {
        public int id { get; set; }
        public int uid { get; set; }
        public int trackid { get; set; }
        public int listid { get; set; }
        public string audioprofile { get; set; }
        public DateTime time { get; set; }

        public User User { get; set; }
        public Track Track { get; set; }
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

        public CommentVM ToVM(User owner) => new CommentVM
        {
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
            return new TrackLocation
            {
                listid = int.Parse(splits[0]),
                position = int.Parse(splits[1]),
                trackid = int.Parse(splits[2])
            };
        }
    }
}

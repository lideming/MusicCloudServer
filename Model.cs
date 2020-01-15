using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<List>().ToTable("lists");
            modelBuilder.Entity<User>().ToTable("users").HasIndex(u => u.username).IsUnique();
            modelBuilder.Entity<Track>().ToTable("tracks");
            modelBuilder.Entity<Comment>().ToTable("comments");

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
            if (user == null || !ValidatePassword(passwd, user.passwd)) return null;
            return user;
        }

        public IEnumerable<Track> GetTracks(IEnumerable<int> trackids)
        {
            return trackids.Select(i => Tracks.Find(i));
        }

        public static string HashPassword(string passwd)
        {
            var salt = new byte[128 / 8];
            RandomNumberGenerator.Fill(salt);
            var saltedPasswd = KeyDerivation.Pbkdf2(passwd, salt, KeyDerivationPrf.HMACSHA1, 1000, 128 / 8);
            return Convert.ToBase64String(saltedPasswd) + "|" + Convert.ToBase64String(salt);
        }

        public static bool ValidatePassword(string passwd, string saltedBundle)
        {
            var splits = saltedBundle.Split('|');
            var salt = Convert.FromBase64String(splits[1]);
            var saltedPasswd = KeyDerivation.Pbkdf2(passwd, salt, KeyDerivationPrf.HMACSHA1, 1000, 128 / 8);
            return Convert.FromBase64String(splits[0]).SequenceEqual(saltedPasswd);
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

        public string lyrics { get; set; }

        public void ReadTrackInfoFromFile(MCloudConfig config)
        {
            var path = Path.Combine(config.StorageDir,
                                        this.url.Substring("storage/".Length));
            Id3.Id3Tag tag;
            using (var mp3 = new Id3.Mp3(path)) {
                tag = mp3.GetTag(Id3.Id3TagFamily.Version2X);
            }
            this.artist = string.Join(" / ", tag.Artists.Value).Replace("\u0000", "");
            this.name = tag.Title.Value.Replace("\u0000", "");
        }
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

        public CommentVM ToVM() => new CommentVM {
            id = this.id,
            uid = this.uid,
            username = "uid" + this.uid,
            date = this.date,
            content = this.content
        };
    }

    public class CommentVM
    {
        public int id { get; set; }
        public int uid { get; set; }
        public string username { get; set; }

        public DateTime date { get; set; }

        public string content { get; set; }
    }
}

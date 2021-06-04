using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public DbSet<TrackList> Lists { get; set; }
        public DbSet<Track> Tracks { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<LoginRecord> Logins { get; set; }
        public DbSet<PlayRecord> Plays { get; set; }

        public UserService UserService { get; set; }
        public bool IsLogged => UserService.IsLogged;
        public User User => UserService.User;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TrackList>().ToTable("lists");
            modelBuilder.Entity<User>().ToTable("users")
                .HasIndex(u => u.username).IsUnique();
            modelBuilder.Entity<Track>().ToTable("tracks");
            modelBuilder.Entity<Comment>().ToTable("comments")
                .HasIndex(c => c.tag);
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

            // Workaround for SQLite, which does not support list type:
            if (MCloudConfig.DbType == DbType.SQLite)
            {
                ApplyListConversion(modelBuilder.Entity<User>().Property(u => u.lists));
                ApplyListConversion(modelBuilder.Entity<TrackList>().Property(l => l.trackids));
            }

            ApplyListConversion(modelBuilder.Entity<Track>().Property(t => t.files));
        }

        /// <summary>
        /// Make a property of list be stored as comma-separated string
        /// </summary>
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
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace MCloudServer
{
    public class Track : IOwnership
    {
        [Key]
        public int id { get; set; }
        public int owner { get; set; } // the id of user that uploads this track
        public Visibility visibility { get; set; }
        public string name { get; set; }
        public string artist { get; set; }
        public string album { get; set; }
        public string albumArtist { get; set; }
        
        [Obsolete]
        public string url { get; set; }

        [Obsolete]
        public int size { get; set; }

        public int length { get; set; }

        public int? fileRecordId { get; set; }
        public StoredFile fileRecord { get; set; }

        [ConcurrencyCheck]
        public int version { get; set; }

        public string lyrics { get; set; }

        public List<TrackFile> files { get; set; }

        [InverseProperty("Track")]
        public List<TrackFile> trackFiles { get; set; }

        // tracks with same groupId are in the same group
        // it's track id by default.
        public int groupId { get; set; }

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
                this.album = info.Album;
                this.albumArtist = info.AlbumArtist;
            }
        }

        public void ReadAlbumFromFile(AppService app)
        {
            if (TryGetStoragePath(app, out var path))
            {
                var info = new ATL.Track(path);
                this.album = info.Album;
                this.albumArtist = info.AlbumArtist;
            }
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

    public class TrackVM
    {
        public int id { get; set; }
        public string name { get; set; }
        public string artist { get; set; }
        public string album { get; set; }
        public string albumArtist { get; set; }
        public string url { get; set; }
        public int size { get; set; }
        public int length { get; set; }
        public int owner { get; set; }
        public Visibility? visibility { get; set; }
        public int? version { get; set; }

        public string lyrics { get; set; }

        public int? groupId { get; set; }

        public List<TrackFileVM> files { get; set; }

        public static TrackVM FromTrack(Track t, AppService app, bool withLyrics = false)
        {
            var vm = new TrackVM {
                id = t.id,
                name = t.name,
                artist = t.artist,
                album = t.album,
                albumArtist = t.albumArtist,
                url = t.url,
                size = t.size,
                length = t.length,
                owner = t.owner,
                visibility = t.visibility,
                lyrics = string.IsNullOrEmpty(t.lyrics) ? "" : withLyrics ? t.lyrics : null,
                groupId = t.groupId,
                version = t.version
            };
            if (app.Config.Converters?.Count > 0 || t.files?.Count > 0) {
                var origBitrate = t.length > 0 ? t.size / t.length / 128 : 0;
                vm.files = new List<TrackFileVM>();
                vm.files.Add(new TrackFileVM {
                    bitrate = origBitrate,
                    format = t.url.Contains('.') ? t.url.Substring(t.url.IndexOf('.') + 1) : "",
                    profile = "",
                    size = vm.size
                });
                if (t.files != null) {
                    foreach (var item in t.files) {
                        vm.files.Add(new TrackFileVM(item));
                    }
                }
                if (app.Config.Converters != null) {
                    foreach (var item in app.Config.Converters) {
                        if (origBitrate / 2 < item.Bitrate) continue;
                        if (t.files?.Any(x => x.ConvName == item.Name) == true) continue;
                        vm.files.Add(new TrackFileVM {
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

    [Index("TrackID", IsUnique = false)]
    [Table("trackFile")]
    public class TrackFile : ICloneable
    {
        public int Id { get;set; }

        public string ConvName { get; set; }

        public string Format { get; set; }
        public int Bitrate { get; set; }

        [Obsolete]
        public long Size { get; set; }

        public int TrackID { get; set; }
        public Track Track { get; set; }

        public int FileID { get; set; }
        public StoredFile File { get; set; }

        public TrackFile Clone() => base.MemberwiseClone() as TrackFile;
        object ICloneable.Clone() => this.Clone();

        public override bool Equals(object obj)
            => obj is TrackFile t
                && ConvName == t.ConvName
                && Format == t.Format;

        public override int GetHashCode()
            => HashCode.Combine(ConvName, Format);
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

    public class TrackLocationWithProfile : TrackLocation
    {
        public string profile { get; set; }
    }
}

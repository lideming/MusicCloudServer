using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace MCloudServer
{
    public class TrackList : IOwnership
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

    public class TrackListPutVM
    {
        public int id { get; set; }
        public string name { get; set; }
        public List<int> trackids { get; set; }
        public Visibility? visibility { get; set; }
        public int? version { get; set; }

        public TrackList ToList() => ApplyToList(new TrackList());

        public TrackList ApplyToList(TrackList list)
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

        public TrackListInfoVM(TrackList list)
        {
            id = list.id;
            owner = list.owner;
            name = list.name;
            visibility = list.visibility;
        }
    }

    public static class ListExtensions
    {
        public static IQueryable<TrackListInfoVM> ToTrackListInfo(this IQueryable<TrackList> lists)
        {
            return lists.Select(l => new TrackListInfoVM { id = l.id, owner = l.owner, name = l.name, visibility = l.visibility });
        }
    }
}

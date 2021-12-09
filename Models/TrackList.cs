using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace MCloudServer
{
    public class TrackList : IOwnership
    {
        [Key]
        public int id { get; set; }

        [ForeignKey("user")]
        public int owner { get; set; }
        public User user { get; set; }

        public string name { get; set; }

        public List<int> trackids { get; set; }

        public Visibility visibility { get; set; }

        [ForeignKey("pic")]
        public int? picId { get; set; }
        public StoredFile pic { get; set; }

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
        public string ownerName { get; set; }
        public string name { get; set; }
        public string picurl { get; set; }
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
}

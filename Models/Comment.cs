using System;
using System.ComponentModel.DataAnnotations;

namespace MCloudServer
{
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
}

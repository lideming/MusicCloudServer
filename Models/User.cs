using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MCloudServer
{
    public class User
    {
        [Key]
        public int id { get; set; }
        public string username { get; set; }
        public UserRole role { get; set; }
        public List<int> lists { get; set; }
        public string passwd { get; set; }
        public string last_playing { get; set; }

        public int? avatarId { get; set; }
        public StoredFile avatar { get; set; }

        [ConcurrencyCheck]
        public int version { get; set; }

        public bool AllowFileUploadSize(long size)
        {
            if (role == UserRole.SuperAdmin) return true;
            return size <= 256 * 1024 * 1024;
        }
    }

    [Table("userSocialLinks")]
    [Index("userId")]
    [Index("provider", "idFromProvider", IsUnique = true)]
    public class UserSocialLink {
        [Key]
        public int id { get; set; }

        public int userId { get; set; }
        public User user { get; set; }

        public string provider { get; set; }
        public string accessToken { get; set; }
        public string refreshToken { get; set; }
        public string idFromProvider { get; set; }
        public string nameFromProvider { get; set; }
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

    public class UserStoreItem
    {
        // Composite key:
        public int userId { get; set; }
        public string key { get; set; }

        public byte[] value { get; set; }

        public Visibility visibility { get; set; }

        [ConcurrencyCheck]
        public int revision { get; set; }
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
        public string proof { get; set; }
    }

    public class ChallengeResponse
    {
        public string challenge { get; set; }
    }

    public class LoginRequest 
    {
        public string username { get; set; }
        public string password { get; set; }
        public string proof { get; set; }
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
}

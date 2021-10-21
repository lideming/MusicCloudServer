using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

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

    public class LoginRecord
    {
        [Key]
        public string token { get; set; }
        public DateTime login_date { get; set; }
        public DateTime last_used { get; set; }

        public int UserId { get; set; }
        public User User { get; set; }
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
}

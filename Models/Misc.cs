using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MCloudServer
{

    public class ConfigItem
    {
        [Key]
        public string Key { get; set; }
        public string Value { get; set; }
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
}

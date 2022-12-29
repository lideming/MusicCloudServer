using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

#nullable enable

namespace MCloudServer
{
    public class Utils
    {
        public static string SignTag(string str, string key)
        {
            return SignTag(str, Encoding.UTF8.GetBytes(key));
        }

        public static string SignTag(string str, byte[] key)
        {
            using (var hmac = new HMACSHA256(key))
            {
                return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(str)));
            }
        }

        public static string SignToken(string[] strs, byte[] key, TimeSpan ttl)
        {
            var payload = string.Join("|", strs) + "|" + Math.Floor((DateTime.UtcNow + ttl).Subtract(new DateTime(1970, 1, 1)).TotalSeconds);
            var sign = SignTag(payload, key);
            return payload + "|" + sign;
        }

        public static string[] ExtractToken(string token, byte[] key) {
            var parts = token.Split('|');
            if (parts.Length < 3) throw new Exception("Invalid token");
            var now = Math.Floor((DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds);
            if (now > double.Parse(parts[parts.Length - 2])) throw new Exception("Expired token");
            var signedParts = parts.Take(parts.Length - 1);
            var sign = SignTag(string.Join("|", signedParts), key);
            if (sign != parts[parts.Length - 1]) throw new Exception("Invalid signature: " + parts[parts.Length - 1]);
            return parts.Take(parts.Length - 2).ToArray();
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

        public static int? ParseIntNullable(string? str)
        {
            if (str == null) return null;
            return int.Parse(str);
        }

        public static async Task<byte[]> ReadAllAsBytes(Stream stream) {
            using (var ms = new MemoryStream()) {
                await stream.CopyToAsync(ms);
                return ms.ToArray();
            }
        }
    }
}

#nullable restore

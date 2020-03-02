using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

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
            using (var hmac = new HMACSHA1(key))
            {
                return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(str)));
            }
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
}
using System;
using System.Security.Cryptography;
using System.Text;

namespace MCloudServer
{
    public class ProofOfWork
    {
        private const int DEFAULT_DIFFICULTY = 5; // Default number of leading zeros required
        private const int CHALLENGE_VALID_MINUTES = 5; // Challenge valid time window

        public static int CurrentDifficulty { get; set; } = DEFAULT_DIFFICULTY;

        public static string GenerateChallenge(string username)
        {
            // Use timestamp (rounded to minutes) to generate challenge
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
            return $"{username}:{timestamp}";
        }

        public static bool VerifyProof(string proof, string username, string password)
        {
            // Check last few minutes to allow for network delay and time differences
            var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
            
            // Try verifying with timestamps in the valid time window
            for (var minutes = 0; minutes < CHALLENGE_VALID_MINUTES; minutes++)
            {
                var challenge = $"{username}:{currentTimestamp - minutes}";
                var input = $"{challenge}:{password}:{proof}";
                
                using (var sha256 = SHA256.Create())
                {
                    var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                    var hash = Convert.ToHexString(hashBytes).ToLower();

                    // Check if hash starts with required number of zeros
                    var isValid = true;
                    for (var j = 0; j < CurrentDifficulty; j++)
                    {
                        if (hash[j] != '0')
                        {
                            isValid = false;
                            break;
                        }
                    }
                    if (isValid)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}

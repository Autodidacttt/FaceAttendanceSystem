using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace FaceAttendanceSystem.Services
{
    /// <summary>
    /// PBKDF2-HMAC-SHA256 password hashing.
    /// Stored format: PBKDF2$SHA256$&lt;iterations&gt;$&lt;salt base64&gt;$&lt;hash base64&gt;
    /// </summary>
    public static class PasswordHasher
    {
        public const int Iterations = 100000;
        public const int SaltSize = 16;
        public const int HashSize = 32;
        private const string Prefix = "PBKDF2$SHA256$";

        public static string Hash(string password)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));

            var salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            var hash = Derive(password, salt, Iterations, HashSize);
            return Prefix + Iterations.ToString(CultureInfo.InvariantCulture) + "$" +
                   Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
        }

        public static bool Verify(string password, string stored)
        {
            if (password == null || string.IsNullOrEmpty(stored)) return false;

            var parts = stored.Split('$');
            if (parts.Length != 5 || parts[0] != "PBKDF2" || parts[1] != "SHA256") return false;

            int iterations;
            if (!int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out iterations) ||
                iterations < 1)
            {
                return false;
            }

            byte[] salt, expected;
            try
            {
                salt = Convert.FromBase64String(parts[3]);
                expected = Convert.FromBase64String(parts[4]);
            }
            catch (FormatException)
            {
                return false;
            }

            if (salt.Length == 0 || expected.Length == 0) return false;

            var actual = Derive(password, salt, iterations, expected.Length);
            return FixedTimeEquals(actual, expected);
        }

        private static byte[] Derive(string password, byte[] salt, int iterations, int length)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(length);
            }
        }

        /// <summary>Compares two byte arrays in time that depends only on their length.</summary>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;

            var diff = 0;
            for (var i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }

        /// <summary>Random temporary password with upper, lower, digit and symbol characters.</summary>
        public static string GenerateTemporaryPassword(int length = 12)
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghijkmnpqrstuvwxyz";
            const string digits = "23456789";
            const string symbols = "!@#$%*?";
            const string all = upper + lower + digits + symbols;

            var chars = new char[length];
            var buffer = new byte[4];
            using (var rng = RandomNumberGenerator.Create())
            {
                Func<string, char> pick = set =>
                {
                    rng.GetBytes(buffer);
                    return set[(int)(BitConverter.ToUInt32(buffer, 0) % (uint)set.Length)];
                };

                chars[0] = pick(upper);
                chars[1] = pick(lower);
                chars[2] = pick(digits);
                chars[3] = pick(symbols);
                for (var i = 4; i < length; i++) chars[i] = pick(all);

                // Fisher-Yates shuffle so the required classes are not always first.
                for (var i = length - 1; i > 0; i--)
                {
                    rng.GetBytes(buffer);
                    var j = (int)(BitConverter.ToUInt32(buffer, 0) % (uint)(i + 1));
                    var tmp = chars[i];
                    chars[i] = chars[j];
                    chars[j] = tmp;
                }
            }
            return new string(chars);
        }
    }
}

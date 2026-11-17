using System;
using System.Security.Cryptography;

namespace DeviceMonitor.Services
{
    /// <summary>
    /// 密码哈希：PBKDF2-SHA256，每用户独立 16 字节盐，迭代 10 万次。
    /// 库里只存 Salt + Hash，永远不存明文（也不要退回 MD5/SHA1）。
    /// </summary>
    public static class PasswordHasher
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int Iterations = 100_000;

        public static string CreateSalt()
            => Convert.ToBase64String(RandomNumberGenerator.GetBytes(SaltSize));

        public static string Hash(string password, string saltBase64)
        {
            var salt = Convert.FromBase64String(saltBase64);
            var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
            return Convert.ToBase64String(key);
        }

        /// <summary>定长比较，避免用 == 提前返回泄漏时间信息。</summary>
        public static bool Verify(string password, string saltBase64, string hashBase64)
        {
            try
            {
                var expected = Convert.FromBase64String(hashBase64);
                var actual = Convert.FromBase64String(Hash(password, saltBase64));
                return CryptographicOperations.FixedTimeEquals(expected, actual);
            }
            catch
            {
                return false;
            }
        }
    }
}

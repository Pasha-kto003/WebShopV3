using System.Security.Cryptography;
using System.Text;

namespace WebShopV3.Json.Services
{
    public interface IPasswordHasher
    {
        string HashPassword(string password);
        bool VerifyPassword(string hashedPassword, string providedPassword);
        string GetPasswordHashCheck(string password);
    }

    public class PasswordHasherService : IPasswordHasher
    {
        private const int SaltSize = 16; // 128 bit
        private const int KeySize = 32;  // 256 bit
        private const int Iterations = 100000;
        private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

        private const char SaltDelimiter = ':';

        public string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be empty");

            using var rng = RandomNumberGenerator.Create();
            var salt = new byte[SaltSize];
            rng.GetBytes(salt);

            using var pbkdf2 = new Rfc2898DeriveBytes(
                password,
                salt,
                Iterations,
                Algorithm);

            var key = pbkdf2.GetBytes(KeySize);

            // Объединяем соль и ключ
            var result = new byte[SaltSize + KeySize];
            Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
            Buffer.BlockCopy(key, 0, result, SaltSize, KeySize);

            return Convert.ToBase64String(result);
        }

        public string GetPasswordHashCheck(string password)
        {
            // Создаем короткий хэш для проверки (не для аутентификации)
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hash).Substring(0, 10); // Берем первые 10 символов
        }

        public bool VerifyPassword(string hashedPassword, string providedPassword)
        {
            if (string.IsNullOrEmpty(hashedPassword))
                return false;

            try
            {
                // Тут декод
                var hashBytes = Convert.FromBase64String(hashedPassword);

                var salt = new byte[SaltSize];
                Buffer.BlockCopy(hashBytes, 0, salt, 0, SaltSize);

                var storedKey = new byte[KeySize];
                Buffer.BlockCopy(hashBytes, SaltSize, storedKey, 0, KeySize);

                using var pbkdf2 = new Rfc2898DeriveBytes(
                    providedPassword,
                    salt,
                    Iterations,
                    Algorithm);

                var providedKey = pbkdf2.GetBytes(KeySize);

                return CryptographicOperations.FixedTimeEquals(storedKey, providedKey);
            }
            catch (FormatException)
            {
                return hashedPassword == providedPassword;
            }
        }

        public bool IsPasswordHashed(string password)
        {
            if (string.IsNullOrEmpty(password))
                return false;

            try
            {
                var bytes = Convert.FromBase64String(password);
                return bytes.Length == (SaltSize + KeySize);
            }
            catch
            {
                return false;
            }
        }
    }
}

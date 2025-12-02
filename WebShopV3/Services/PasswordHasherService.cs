using System.Security.Cryptography;
using System.Text;

namespace WebShopV3.Services
{
    public interface IPasswordHasher
    {
        string HashPassword(string password);
        bool VerifyPassword(string hashedPassword, string providedPassword);
    }

    public class PasswordHasherService
    {
    }
}

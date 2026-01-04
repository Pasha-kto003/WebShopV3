using Microsoft.Extensions.Logging;
using Moq;
using WebShopV3.Services;
using Xunit;

namespace WebShopV3.Tests.Services
{
    public class PasswordHasherServiceTests
    {
        private readonly PasswordHasherService _passwordHasher;

        public PasswordHasherServiceTests()
        {
            var loggerMock = new Mock<ILogger<PasswordHasherService>>();
            _passwordHasher = new PasswordHasherService();
        }

        [Fact]
        public void HashPassword_ShouldNotBeNullOrEmpty()
        {
            // Arrange
            var password = "TestPassword123!";

            // Act
            var hash = _passwordHasher.HashPassword(password);

            // Assert
            Assert.NotNull(hash);
            Assert.NotEmpty(hash);
        }

        [Fact]
        public void HashPassword_ShouldProduceDifferentHashesForSamePassword()
        {
            // Arrange
            var password = "TestPassword123!";

            // Act
            var hash1 = _passwordHasher.HashPassword(password);
            var hash2 = _passwordHasher.HashPassword(password);

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void VerifyPassword_ShouldReturnTrueForCorrectPassword()
        {
            // Arrange
            var password = "TestPassword123!";
            var hash = _passwordHasher.HashPassword(password);

            // Act
            var result = _passwordHasher.VerifyPassword(hash, password);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void VerifyPassword_ShouldReturnFalseForIncorrectPassword()
        {
            // Arrange
            var password = "TestPassword123!";
            var wrongPassword = "WrongPassword456!";
            var hash = _passwordHasher.HashPassword(password);

            // Act
            var result = _passwordHasher.VerifyPassword(hash, wrongPassword);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void VerifyPassword_ShouldReturnFalseForEmptyPassword()
        {
            // Arrange
            var hash = _passwordHasher.HashPassword("SomePassword");

            // Act
            var result = _passwordHasher.VerifyPassword(hash, "");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HashPassword_ShouldThrowForEmptyPassword()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentException>(() => _passwordHasher.HashPassword(""));
        }

        [Fact]
        public void IsPasswordHashed_ShouldReturnTrueForHashedPassword()
        {
            // Arrange
            var password = "TestPassword123!";
            var hash = _passwordHasher.HashPassword(password);

            // Act
            var result = _passwordHasher.IsPasswordHashed(hash);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsPasswordHashed_ShouldReturnFalseForPlainText()
        {
            // Arrange
            var plainText = "TestPassword123!";

            // Act
            var result = _passwordHasher.IsPasswordHashed(plainText);

            // Assert
            Assert.False(result);
        }
    }
}
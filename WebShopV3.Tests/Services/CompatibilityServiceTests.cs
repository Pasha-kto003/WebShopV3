using Microsoft.EntityFrameworkCore;
using WebShopV3.Models;
using WebShopV3.Services;
using Xunit;

namespace WebShopV3.Tests.Services
{
    public class CompatibilityServiceTests
    {
        private readonly CompatibilityService _compatibilityService;
        private readonly ApplicationDbContext _context;

        public CompatibilityServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "CompatibilityTestDb")
                .Options;
            
            _context = new ApplicationDbContext(options);
            _compatibilityService = new CompatibilityService(_context);
        }

        [Fact]
        public void CheckCompatibility_ShouldReturnCompatibleForMatchingComponents()
        {
            // Arrange
            var components = new List<Component>
            {
                new Component { Id = 1, Name = "ASUS B550", Type = "MB", Socket = "AM4", MemoryType = "DDR4", 
                    FormFactor = "ATX", MaxMemory = 128, MemorySlots = 4 },
                new Component { Id = 2, Name = "Ryzen 7 5800X", Type = "CPU", Socket = "AM4" },
                new Component { Id = 3, Name = "DDR4 16GB", Type = "RAM", MemoryType = "DDR4" }
            };

            // Act
            var result = _compatibilityService.CheckCompatibility(components);

            // Assert
            Assert.True(result.IsCompatible);
            Assert.Empty(result.Errors);
            Assert.Contains("Все компоненты совместимы!", result.SuccessMessage);
        }

        [Fact]
        public void CheckCompatibility_ShouldReturnIncompatibleForDifferentSockets()
        {
            // Arrange
            var components = new List<Component>
            {
                new Component { Id = 1, Name = "ASUS B550", Type = "MB", Socket = "AM4", MemoryType = "DDR4" },
                new Component { Id = 2, Name = "Intel i7", Type = "CPU", Socket = "LGA1700" }
            };

            // Act
            var result = _compatibilityService.CheckCompatibility(components);

            // Assert
            Assert.False(result.IsCompatible);
            Assert.NotEmpty(result.Errors);
            Assert.Contains("не совместим", result.Errors[0]);
        }

        [Fact]
        public void CheckCompatibility_ShouldReturnErrorForMissingMotherboard()
        {
            // Arrange
            var components = new List<Component>
            {
                new Component { Id = 1, Name = "Ryzen 7", Type = "CPU", Socket = "AM4" }
            };

            // Act
            var result = _compatibilityService.CheckCompatibility(components);

            // Assert
            Assert.False(result.IsCompatible);
            Assert.Contains("Не выбрана материнская плата", result.Errors[0]);
        }

        [Fact]
        public void CheckCompatibility_ShouldReturnErrorForMemoryTypeMismatch()
        {
            // Arrange
            var components = new List<Component>
            {
                new Component { Id = 1, Name = "ASUS B550", Type = "MB", Socket = "AM4", MemoryType = "DDR4" },
                new Component { Id = 2, Name = "DDR5 16GB", Type = "RAM", MemoryType = "DDR5" }
            };

            // Act
            var result = _compatibilityService.CheckCompatibility(components);

            // Assert
            Assert.False(result.IsCompatible);
            Assert.Contains("тип памяти", result.Errors[0]);
        }

        [Fact]
        public void IsFormFactorCompatible_ShouldReturnTrueForCompatibleFormFactors()
        {
            // Arrange
            var service = new CompatibilityService(_context);
            
            // Act & Assert
            Assert.True(IsFormFactorCompatiblePrivate("ATX", "ATX"));
            Assert.True(IsFormFactorCompatiblePrivate("mATX", "ATX"));
            Assert.True(IsFormFactorCompatiblePrivate("ITX", "ATX"));
        }

        [Fact]
        public void IsFormFactorCompatible_ShouldReturnFalseForIncompatibleFormFactors()
        {
            // Arrange
            var service = new CompatibilityService(_context);
            
            // Act & Assert
            Assert.False(IsFormFactorCompatiblePrivate("ATX", "ITX"));
            Assert.False(IsFormFactorCompatiblePrivate("mATX", "ITX"));
        }

        // Вспомогательный метод для тестирования приватного метода через reflection
        private bool IsFormFactorCompatiblePrivate(string mbFormFactor, string caseFormFactor)
        {
            var method = typeof(CompatibilityService)
                .GetMethod("IsFormFactorCompatible", 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance);
            
            var service = new CompatibilityService(_context);
            return (bool)method.Invoke(service, new object[] { mbFormFactor, caseFormFactor });
        }
    }
}
using Microsoft.EntityFrameworkCore;
using Moq;
using WebShopV3.Models;
using WebShopV3.Models.DTO;
using WebShopV3.Services;
using Xunit;

namespace WebShopV3.Tests.Services
{
    public class ComparisonServiceTests
    {
        private readonly ComparisonService _service;
        private readonly Mock<ApplicationDbContext> _contextMock;

        public ComparisonServiceTests()
        {
            _contextMock = new Mock<ApplicationDbContext>(new DbContextOptions<ApplicationDbContext>());
            _service = new ComparisonService(_contextMock.Object);
        }
        

        [Fact]
        public async Task GetBestComputer_ShouldReturnNull_WhenEmptyList()
        {
            // Arrange
            var emptyList = new List<Computer>();

            // Act
            var result = await _service.GetBestComputer(emptyList);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetBestSpecifications_ShouldReturnBestValues()
        {
            // Arrange
            var computers = new List<ComputerComparisonDto>
            {
                new ComputerComparisonDto
                {
                    Id = 1,
                    AllCharacteristics = new Dictionary<string, string>
                    {
                        { "CPU - Тактовая частота", "4.7" },
                        { "RAM - Объем памяти", "32" }
                    },
                    TotalScore = 1500
                },
                new ComputerComparisonDto
                {
                    Id = 2,
                    AllCharacteristics = new Dictionary<string, string>
                    {
                        { "CPU - Тактовая частота", "3.5" },
                        { "RAM - Объем памяти", "64" }
                    },
                    TotalScore = 1200
                }
            };

            // Act
            var bestSpecs = _service.GetBestSpecifications(computers);

            // Assert
            Assert.Equal(2, bestSpecs.Count);

            // Проверяем лучшую частоту процессора
            var cpuBest = bestSpecs["CPU - Тактовая частота"];
            Assert.Equal("4.7", cpuBest.bestValue);
            Assert.Equal(0, cpuBest.bestComputerIndex);

            // Проверяем лучший объем памяти
            var ramBest = bestSpecs["RAM - Объем памяти"];
            Assert.Equal("64", ramBest.bestValue);
            Assert.Equal(1, ramBest.bestComputerIndex);
        }

        [Fact]
        public void GetBestSpecifications_ShouldHandleEmptyList()
        {
            // Arrange
            var emptyList = new List<ComputerComparisonDto>();

            // Act
            var result = _service.GetBestSpecifications(emptyList);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void GetBestSpecifications_ShouldHandleMissingCharacteristics()
        {
            // Arrange
            var computers = new List<ComputerComparisonDto>
            {
                new ComputerComparisonDto
                {
                    Id = 1,
                    AllCharacteristics = new Dictionary<string, string>
                    {
                        { "CPU - Тактовая частота", "4.7" }
                    }
                },
                new ComputerComparisonDto
                {
                    Id = 2,
                    AllCharacteristics = new Dictionary<string, string>
                    {
                        { "CPU - Тактовая частота", "3.5" },
                        { "RAM - Объем памяти", "64" }
                    }
                }
            };

            // Act
            var bestSpecs = _service.GetBestSpecifications(computers);

            // Assert
            Assert.Equal(2, bestSpecs.Count);
            Assert.Contains("CPU - Тактовая частота", bestSpecs.Keys);
            Assert.Contains("RAM - Объем памяти", bestSpecs.Keys);

            // Для RAM лучшим должен быть компьютер 2
            var ramBest = bestSpecs["RAM - Объем памяти"];
            Assert.Equal("64", ramBest.bestValue);
            Assert.Equal(1, ramBest.bestComputerIndex);
        }

        [Fact]
        public void GetBestSpecifications_ShouldHandleNonNumericValues()
        {
            // Arrange
            var computers = new List<ComputerComparisonDto>
            {
                new ComputerComparisonDto
                {
                    Id = 1,
                    AllCharacteristics = new Dictionary<string, string>
                    {
                        { "GPU - Модель", "RTX 4070" },
                        { "Тип накопителя", "NVMe SSD" }
                    }
                },
                new ComputerComparisonDto
                {
                    Id = 2,
                    AllCharacteristics = new Dictionary<string, string>
                    {
                        { "GPU - Модель", "RTX 4080" },
                        { "Тип накопителя", "SATA SSD" }
                    }
                }
            };

            // Act
            var bestSpecs = _service.GetBestSpecifications(computers);

            // Assert
            Assert.Equal(2, bestSpecs.Count);

            // RTX 4080 должен быть лучше RTX 4070
            var gpuBest = bestSpecs["GPU - Модель"];
            Assert.Equal("RTX 4080", gpuBest.bestValue);
            Assert.Equal(1, gpuBest.bestComputerIndex);
        }

        private Mock<DbSet<T>> CreateMockDbSet<T>(List<T> data) where T : class
        {
            var queryable = data.AsQueryable();
            var mockSet = new Mock<DbSet<T>>();

            mockSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(queryable.Provider);
            mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
            mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
            mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(queryable.GetEnumerator());

            // Для async операций
            mockSet.As<IAsyncEnumerable<T>>()
                .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
                .Returns(new TestAsyncEnumerator<T>(queryable.GetEnumerator()));

            // Настраиваем Where с Include
            mockSet.As<IQueryable<T>>()
                .Setup(m => m.Where(It.IsAny<System.Linq.Expressions.Expression<Func<T, bool>>>()))
                .Returns((System.Linq.Expressions.Expression<Func<T, bool>> predicate) =>
                {
                    var compiled = predicate.Compile();
                    return queryable.Where(compiled).AsQueryable();
                });

            return mockSet;
        }

        internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
        {
            private readonly IEnumerator<T> _inner;

            public TestAsyncEnumerator(IEnumerator<T> inner)
            {
                _inner = inner;
            }

            public T Current => _inner.Current;

            public ValueTask DisposeAsync()
            {
                _inner.Dispose();
                return ValueTask.CompletedTask;
            }

            public ValueTask<bool> MoveNextAsync()
            {
                return ValueTask.FromResult(_inner.MoveNext());
            }
        }
    }
}
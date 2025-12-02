using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebShopV3.Services;

namespace WebShopV3.Models
{

    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context, IPasswordHasher passwordHasher)
        {
            // Добавляем данные напрямую в базу
            if (!context.Characteristics.Any())
            {
                context.Characteristics.AddRange(
                    new Characteristic { Id = 1, Name = "Тактовая частота", Unit = "ГГц", Description = "Базовая частота процессора" },
                    new Characteristic { Id = 1, Name = "Тактовая частота", Unit = "ГГц", Description = "Базовая частота процессора" },
                    new Characteristic { Id = 2, Name = "Количество ядер", Unit = "шт", Description = "Количество физических ядер" },
                    new Characteristic { Id = 3, Name = "Объем памяти", Unit = "ГБ", Description = "Объем оперативной памяти" },
                    new Characteristic { Id = 4, Name = "Частота памяти", Unit = "МГц", Description = "Частота оперативной памяти" },
                    new Characteristic { Id = 5, Name = "Объем видеопамяти", Unit = "ГБ", Description = "Объем памяти видеокарты" },
                    new Characteristic { Id = 6, Name = "Тип памяти", Unit = "", Description = "Тип памяти (DDR4, GDDR6, etc.)" },
                    new Characteristic { Id = 7, Name = "Емкость накопителя", Unit = "ГБ", Description = "Объем storage накопителя" },
                    new Characteristic { Id = 8, Name = "Скорость чтения", Unit = "МБ/с", Description = "Скорость чтения накопителя" },
                    new Characteristic { Id = 9, Name = "Мощность", Unit = "Вт", Description = "Мощность блока питания" },
                    new Characteristic { Id = 10, Name = "Разъемы", Unit = "", Description = "Доступные разъемы и порты" }
                );
            }

            if (!context.Components.Any())
            {
                context.Components.AddRange(
                    new Component { Id = 1, Name = "Intel Core i7-12700K", Description = "Процессор Intel Core i7", Price = 35000m, Quantity = 10, Type = "CPU", Specifications = "12 ядер, 20 потоков, 3.6 ГГц" },
                    new Component
                    {
                        Id = 3,
                        Name = "Kingston Fury 32GB DDR5",
                        Description = "Оперативная память Kingston Fury Beast",
                        Price = 12000m,
                        Quantity = 15,
                        Type = "RAM",
                        Specifications = "DDR5 5200MHz, 32GB (2x16GB)"
                    }, new Component
                    {
                        Id = 3,
                        Name = "Kingston Fury 32GB DDR5",
                        Description = "Оперативная память Kingston Fury Beast",
                        Price = 12000m,
                        Quantity = 15,
                        Type = "RAM",
                        Specifications = "DDR5 5200MHz, 32GB (2x16GB)"
                    }
                );
            }
            var admin = new User
            {
                Username = "admin",
                Email = "admin@example.com",
                FirstName = "Администратор",
                LastName = "Системы",
                PasswordHash = passwordHasher.HashPassword("Admin123!"),
                UserTypeId = 1, // Админ
                CreatedAt = DateTime.Now
            };

            // Создаем тестового пользователя
            var user = new User
            {
                Username = "user",
                Email = "user@example.com",
                FirstName = "Тестовый",
                LastName = "Пользователь",
                PasswordHash = passwordHasher.HashPassword("User123!"),
                UserTypeId = 3, // Пользователь
                CreatedAt = DateTime.Now
            };

            context.Users.AddRange(admin, user);
            context.SaveChanges();
        }
    }


    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Computer> Computers { get; set; }
        public DbSet<Component> Components { get; set; }
        public DbSet<ComputerComponent> ComputerComponents { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<ComputerOrder> ComputerOrders { get; set; }
        public DbSet<OrderType> OrderTypes { get; set; }
        public DbSet<Status> Statuses { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserType> UserTypes { get; set; }
        public DbSet<Characteristic> Characteristics { get; set; }
        public DbSet<ComponentCharacteristic> ComponentCharacteristics { get; set; }
        public DbSet<ComponentOrder> ComponentOrders { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ComponentCharacteristic>()
            .HasKey(cc => new { cc.ComponentId, cc.CharacteristicId });

            modelBuilder.Entity<ComponentCharacteristic>()
                .HasOne(cc => cc.Component)
                .WithMany(c => c.ComponentCharacteristics)
                .HasForeignKey(cc => cc.ComponentId);

            modelBuilder.Entity<ComponentCharacteristic>()
                .HasOne(cc => cc.Characteristic)
                .WithMany(ch => ch.ComponentCharacteristics)
                .HasForeignKey(cc => cc.CharacteristicId);

            // Конфигурация для ComputerComponent
            modelBuilder.Entity<ComputerComponent>()
                .HasKey(cc => new { cc.ComputerId, cc.ComponentId });

            modelBuilder.Entity<ComputerComponent>()
                .HasOne(cc => cc.Computer)
                .WithMany(c => c.ComputerComponents)
                .HasForeignKey(cc => cc.ComputerId);

            modelBuilder.Entity<ComputerComponent>()
                .HasOne(cc => cc.Component)
                .WithMany(c => c.ComputerComponents)
                .HasForeignKey(cc => cc.ComponentId);

            // Конфигурация для ComputerOrder
            modelBuilder.Entity<ComputerOrder>()
                .HasKey(co => new { co.ComputerId, co.OrderId });

            modelBuilder.Entity<ComputerOrder>()
                .HasOne(co => co.Computer)
                .WithMany(c => c.ComputerOrders)
                .HasForeignKey(co => co.ComputerId);

            modelBuilder.Entity<ComputerOrder>()
                .HasOne(co => co.Order)
                .WithMany(o => o.ComputerOrders)
                .HasForeignKey(co => co.OrderId);

            modelBuilder.Entity<ComponentOrder>()
            .HasKey(co => new { co.OrderId, co.ComponentId });

            modelBuilder.Entity<ComponentOrder>()
                .HasOne(co => co.Order)
                .WithMany(o => o.ComponentOrders)
                .HasForeignKey(co => co.OrderId);

            modelBuilder.Entity<ComponentOrder>()
                .HasOne(co => co.Component)
                .WithMany()
                .HasForeignKey(co => co.ComponentId);

            
        }
    }
}

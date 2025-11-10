using Microsoft.EntityFrameworkCore;

namespace WebShopV3.Models
{

    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context)
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

            // Заполнение начальными данными
            //modelBuilder.Entity<Component>().HasData(
            //    new Component
            //    {
            //        Id = 1,
            //        Name = "Intel Core i7-12700K",
            //        Description = "Процессор Intel Core i7 12-го поколения",
            //        Price = 35000m,
            //        Quantity = 10,
            //        Type = "CPU",
            //        Specifications = "12 ядер, 20 потоков, 3.6 ГГц"
            //    },
            //    new Component
            //    {
            //        Id = 2,
            //        Name = "NVIDIA RTX 4070",
            //        Description = "Видеокарта NVIDIA GeForce RTX 4070",
            //        Price = 65000m,
            //        Quantity = 5,
            //        Type = "GPU",
            //        Specifications = "12GB GDDR6X, 5888 ядер"
            //    },
            //    new Component
            //    {
            //        Id = 3,
            //        Name = "Kingston Fury 32GB DDR5",
            //        Description = "Оперативная память Kingston Fury Beast",
            //        Price = 12000m,
            //        Quantity = 15,
            //        Type = "RAM",
            //        Specifications = "DDR5 5200MHz, 32GB (2x16GB)"
            //    },
            //    new Component
            //    {
            //        Id = 4,
            //        Name = "Samsung 980 Pro 1TB",
            //        Description = "SSD накопитель Samsung 980 Pro",
            //        Price = 15000m,
            //        Quantity = 8,
            //        Type = "SSD",
            //        Specifications = "NVMe M.2, 7000MB/s read"
            //    },
            //    new Component
            //    {
            //        Id = 5,
            //        Name = "Seasonic Focus GX-750",
            //        Description = "Блок питания Seasonic 750W",
            //        Price = 8000m,
            //        Quantity = 12,
            //        Type = "PSU",
            //        Specifications = "750W, 80+ Gold, fully modular"
            //    }
            //);

            //// Данные для связей компонентов и характеристик
            //modelBuilder.Entity<ComponentCharacteristic>().HasData(
            //    // Характеристики для процессора (ID: 1)
            //    new ComponentCharacteristic { ComponentId = 1, CharacteristicId = 1, Value = "3.6" },      // Тактовая частота
            //    new ComponentCharacteristic { ComponentId = 1, CharacteristicId = 2, Value = "12" },      // Количество ядер

            //    // Характеристики для видеокарты (ID: 2)
            //    new ComponentCharacteristic { ComponentId = 2, CharacteristicId = 5, Value = "12" },      // Объем видеопамяти
            //    new ComponentCharacteristic { ComponentId = 2, CharacteristicId = 6, Value = "GDDR6X" },  // Тип памяти

            //    // Характеристики для оперативной памяти (ID: 3)
            //    new ComponentCharacteristic { ComponentId = 3, CharacteristicId = 3, Value = "32" },      // Объем памяти
            //    new ComponentCharacteristic { ComponentId = 3, CharacteristicId = 4, Value = "5200" },    // Частота памяти
            //    new ComponentCharacteristic { ComponentId = 3, CharacteristicId = 6, Value = "DDR5" },    // Тип памяти

            //    // Характеристики для SSD (ID: 4)
            //    new ComponentCharacteristic { ComponentId = 4, CharacteristicId = 7, Value = "1000" },    // Емкость накопителя
            //    new ComponentCharacteristic { ComponentId = 4, CharacteristicId = 8, Value = "7000" },    // Скорость чтения

            //    // Характеристики для блока питания (ID: 5)
            //    new ComponentCharacteristic { ComponentId = 5, CharacteristicId = 9, Value = "750" }      // Мощность
            //);

            //modelBuilder.Entity<Characteristic>().HasData(
            //        new Characteristic { Id = 1, Name = "Тактовая частота", Unit = "ГГц", Description = "Базовая частота процессора" },
            //        new Characteristic { Id = 2, Name = "Количество ядер", Unit = "шт", Description = "Количество физических ядер" },
            //        new Characteristic { Id = 3, Name = "Объем памяти", Unit = "ГБ", Description = "Объем оперативной памяти" },
            //        new Characteristic { Id = 4, Name = "Частота памяти", Unit = "МГц", Description = "Частота оперативной памяти" },
            //        new Characteristic { Id = 5, Name = "Объем видеопамяти", Unit = "ГБ", Description = "Объем памяти видеокарты" },
            //        new Characteristic { Id = 6, Name = "Тип памяти", Unit = "", Description = "Тип памяти (DDR4, GDDR6, etc.)" },
            //        new Characteristic { Id = 7, Name = "Емкость накопителя", Unit = "ГБ", Description = "Объем storage накопителя" },
            //        new Characteristic { Id = 8, Name = "Скорость чтения", Unit = "МБ/с", Description = "Скорость чтения накопителя" },
            //        new Characteristic { Id = 9, Name = "Мощность", Unit = "Вт", Description = "Мощность блока питания" },
            //        new Characteristic { Id = 10, Name = "Разъемы", Unit = "", Description = "Доступные разъемы и порты" }
            //    );

            //    modelBuilder.Entity<UserType>().HasData(
            //        new UserType { Id = 1, Name = "Админ" },
            //        new UserType { Id = 2, Name = "Пользователь" },
            //        new UserType { Id = 3, Name = "Менеджер" }
            //    );

            //    modelBuilder.Entity<OrderType>().HasData(
            //        new OrderType { Id = 1, Name = "Продажа" },
            //        new OrderType { Id = 2, Name = "Привоз" }
            //    );

            //    modelBuilder.Entity<Status>().HasData(
            //        new Status { Id = 1, Name = "Выполнен" },
            //        new Status { Id = 2, Name = "В ожидании" },
            //        new Status { Id = 3, Name = "Отменен" }
            //    );

            //    modelBuilder.Entity<User>().HasData(
            //        new User
            //        {
            //            Id = 1,
            //            Username = "admin",
            //            Email = "admin@example.com",
            //            PasswordHash = "hashed_admin_123", // Статическое значение
            //            FirstName = "Admin",
            //            LastName = "User",
            //            Phone = "1234567890",
            //            UserTypeId = 1
            //        },
            //        new User
            //        {
            //            Id = 2,
            //            Username = "manager",
            //            Email = "manager@example.com",
            //            PasswordHash = "hashed_manager_123", // Статическое значение
            //            FirstName = "Manager",
            //            LastName = "User",
            //            Phone = "1234567891",
            //            UserTypeId = 3
            //        },
            //        new User
            //        {
            //            Id = 3,
            //            Username = "user",
            //            Email = "user@example.com",
            //            PasswordHash = "hashed_user_123", // Статическое значение
            //            FirstName = "Regular",
            //            LastName = "User",
            //            Phone = "1234567892",
            //            UserTypeId = 2
            //        }
            //    );
        }
    }
}

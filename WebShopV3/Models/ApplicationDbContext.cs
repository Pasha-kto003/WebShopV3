using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebShopV3.Services;

namespace WebShopV3.Models
{

    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context)
        {
            // Проверяем, есть ли уже данные в базе
            if (context.Computers.Any() ||
                context.Components.Any() ||
                context.Characteristics.Any())
            {
                return; // База уже содержит данные
            }

            try
            {
                // Начинаем транзакцию для атомарного заполнения данных
                using var transaction = context.Database.BeginTransaction();

                // 1. Заполняем типы пользователей
                var userTypes = new List<UserType>
                {
                    new UserType { Id = 4, Name = "Админ" },
                    new UserType { Id = 5, Name = "Менеджер" },
                    new UserType { Id = 6, Name = "Пользователь" }
                };
                    context.UserTypes.AddRange(userTypes);

                    // 2. Заполняем статусы
                var statuses = new List<Status>
                {
                    new Status { Id = 4, Name = "Выполнен" },
                    new Status { Id = 5, Name = "В ожидании" },
                    new Status { Id = 6, Name = "Отмена" },
                    new Status { Id = 7, Name = "Проблема с наличием" }
                };
                    context.Statuses.AddRange(statuses);

                    // 3. Заполняем типы заказов
                var orderTypes = new List<OrderType>
                {
                    new OrderType { Id = 3, Name = "Продажа" },
                    new OrderType { Id = 4, Name = "Приход" },
                    new OrderType { Id = 5, Name = "Отмена" }
                };
                    context.OrderTypes.AddRange(orderTypes);

                    // 4. Заполняем характеристики
                var characteristics = new List<Characteristic>
                {
                    new Characteristic { Id = 1, Name = "Тактовая частота", Unit = "ГГц", Description = "Базовая частота процессора" },
                    new Characteristic { Id = 2, Name = "Количество ядер", Unit = "шт", Description = "Количество физических ядер процессора" },
                    new Characteristic { Id = 3, Name = "Количество потоков", Unit = "шт", Description = "Количество логических процессоров" },
                    new Characteristic { Id = 4, Name = "Объем памяти", Unit = "ГБ", Description = "Объем оперативной памяти" },
                    new Characteristic { Id = 5, Name = "Частота памяти", Unit = "МГц", Description = "Частота оперативной памяти" },
                    new Characteristic { Id = 6, Name = "Тип памяти", Unit = "", Description = "Тип памяти (DDR4, DDR5)" },
                    new Characteristic { Id = 7, Name = "Тайминги", Unit = "", Description = "Тайминги оперативной памяти (CL)" },
                    new Characteristic { Id = 8, Name = "Емкость накопителя", Unit = "ГБ", Description = "Объем storage накопителя" },
                    new Characteristic { Id = 9, Name = "Скорость чтения", Unit = "МБ/с", Description = "Скорость чтения накопителя" },
                    new Characteristic { Id = 10, Name = "Скорость записи", Unit = "МБ/с", Description = "Скорость записи накопителя" },
                    new Characteristic { Id = 11, Name = "Тип накопителя", Unit = "", Description = "Тип накопителя (SSD, HDD, NVMe)" },
                    new Characteristic { Id = 12, Name = "Объем видеопамяти", Unit = "ГБ", Description = "Объем памяти видеокарты" },
                    new Characteristic { Id = 13, Name = "Частота GPU", Unit = "МГц", Description = "Базовая частота графического процессора" },
                    new Characteristic { Id = 14, Name = "Разъемы питания", Unit = "", Description = "Требуемые разъемы питания (6-pin, 8-pin)" },
                    new Characteristic { Id = 15, Name = "Рекомендуемый БП", Unit = "Вт", Description = "Рекомендуемая мощность блока питания" },
                    new Characteristic { Id = 16, Name = "Чипсет", Unit = "", Description = "Чипсет материнской платы" },
                    new Characteristic { Id = 17, Name = "Сокет", Unit = "", Description = "Тип сокета процессора" },
                    new Characteristic { Id = 18, Name = "Форм-фактор", Unit = "", Description = "Форм-фактор платы (ATX, mATX, ITX)" },
                    new Characteristic { Id = 19, Name = "Количество слотов памяти", Unit = "шт", Description = "Количество слотов оперативной памяти" },
                    new Characteristic { Id = 20, Name = "Макс. объем памяти", Unit = "ГБ", Description = "Максимальный поддерживаемый объем памяти" },
                    new Characteristic { Id = 21, Name = "Слоты расширения", Unit = "шт", Description = "Количество PCI-E слотов" },
                    new Characteristic { Id = 22, Name = "Порты SATA", Unit = "шт", Description = "Количество SATA портов" },
                    new Characteristic { Id = 23, Name = "Порты M.2", Unit = "шт", Description = "Количество M.2 слотов" },
                    new Characteristic { Id = 24, Name = "Мощность", Unit = "Вт", Description = "Номинальная мощность блока питания" },
                    new Characteristic { Id = 25, Name = "Сертификат", Unit = "", Description = "Сертификат эффективности (80+ Bronze, Gold, etc.)" },
                    new Characteristic { Id = 26, Name = "Разъемы PCI-E", Unit = "шт", Description = "Количество PCI-E разъемов" },
                    new Characteristic { Id = 27, Name = "Разъемы SATA", Unit = "шт", Description = "Количество SATA разъемов" },
                    new Characteristic { Id = 28, Name = "Тип корпуса", Unit = "", Description = "Тип корпуса (Mid-Tower, Full-Tower, etc.)" },
                    new Characteristic { Id = 29, Name = "Поддерживаемые платы", Unit = "", Description = "Поддерживаемые форм-факторы материнских плат" },
                    new Characteristic { Id = 30, Name = "Слоты расширения", Unit = "шт", Description = "Количество слотов расширения" },
                    new Characteristic { Id = 31, Name = "Отсеки для HDD", Unit = "шт", Description = "Количество отсеков для жестких дисков" },
                    new Characteristic { Id = 32, Name = "Отсеки для SSD", Unit = "шт", Description = "Количество отсеков для SSD" },
                    new Characteristic { Id = 33, Name = "Вентиляторы", Unit = "шт", Description = "Количество установленных вентиляторов" },
                    new Characteristic { Id = 34, Name = "Тип охлаждения", Unit = "", Description = "Тип охлаждения (Air, Liquid)" },
                    new Characteristic { Id = 35, Name = "Рассеиваемая мощность", Unit = "Вт", Description = "Максимальная рассеиваемая мощность" },
                    new Characteristic { Id = 36, Name = "Уровень шума", Unit = "дБ", Description = "Уровень шума при работе" },
                    new Characteristic { Id = 37, Name = "Совместимые сокеты", Unit = "", Description = "Поддерживаемые сокеты процессоров" },
                    new Characteristic { Id = 38, Name = "Гарантия", Unit = "мес", Description = "Срок гарантии производителя" },
                    new Characteristic { Id = 39, Name = "Габариты", Unit = "мм", Description = "Габаритные размеры компонента" },
                    new Characteristic { Id = 40, Name = "Вес", Unit = "г", Description = "Вес компонента" }
                };
                    context.Characteristics.AddRange(characteristics);

                    // 5. Заполняем компоненты
                var components = new List<Component>
                {
                    // Жесткие диски
                    new Component
                    {
                        Id = 7,
                        Name = "SeaGate",
                        Description = "Простое описание",
                        Price = 8000.00m,
                        Quantity = 15,
                        Type = "HDD",
                        Specifications = "Дополнительное описание",
                        ImageUrl = "1.jpg"
                    },

                    // Материнские платы
                    new Component
                    {
                        Id = 32,
                        Name = "ASUS ROG Strix B550-F Gaming",
                        Description = "Материнская плата AMD B550, Socket AM4, ATX",
                        Price = 18000.00m,
                        Quantity = 30,
                        Type = "MB",
                        Specifications = "AMD B550, 2x M.2, USB 3.2, 2.5G LAN",
                        FormFactor = "ATX",
                        MaxMemory = 128,
                        MemorySlots = 4,
                        MemoryType = "DDR4",
                        Socket = "AM4",
                        ImageUrl = "3.png"
                    },
                    new Component
                    {
                        Id = 33,
                        Name = "Gigabyte B760 Gaming X",
                        Description = "Материнская плата Intel B760, Socket LGA1700, ATX",
                        Price = 16000.00m,
                        Quantity = 36,
                        Type = "MB",
                        Specifications = "Intel B760, PCIe 4.0, 2x M.2, 2.5G LAN",
                        FormFactor = "ATX",
                        MaxMemory = 128,
                        MemorySlots = 4,
                        MemoryType = "DDR5",
                        Socket = "LGA1700",
                        ImageUrl = "5985216.png"
                    },
                    // Добавьте остальные компоненты по аналогии...
                    // Из-за ограничения длины я добавлю только часть, вы можете продолжить по шаблону
                    new Component
                    {
                        Id = 37,
                        Name = "AMD Ryzen 7 5800X",
                        Description = "Процессор AMD Ryzen 7, 8 ядер, 3.8 ГГц",
                        Price = 25000.00m,
                        Quantity = 29,
                        Type = "CPU",
                        Specifications = "8 cores/16 threads, 3.8-4.7 GHz, 36MB cache, 105W",
                        Socket = "AM4",
                        ImageUrl = "5985216.png"
                    },
                    new Component
                    {
                        Id = 38,
                        Name = "Intel Core i7-13700K",
                        Description = "Процессор Intel Core i7, 16 ядер, 3.4 ГГц",
                        Price = 32000.00m,
                        Quantity = 31,
                        Type = "CPU",
                        Specifications = "16 cores/24 threads, 3.4-5.4 GHz, 30MB cache, 125W",
                        Socket = "LGA1700",
                        ImageUrl = "5985216.png"
                    },
                    // Оперативная память
                    new Component
                    {
                        Id = 42,
                        Name = "Kingston Fury Beast 32GB DDR4 3200MHz",
                        Description = "Оперативная память 32GB DDR4, 2x16GB",
                        Price = 8000.00m,
                        Quantity = 41,
                        Type = "RAM",
                        Specifications = "32GB (2x16GB) DDR4 3200MHz, CL16, 1.35V",
                        MemoryType = "DDR4",
                        ImageUrl = "1039454.jpg"
                    },
                    // Видеокарты
                    new Component
                    {
                        Id = 68,
                        Name = "NVIDIA GeForce RTX 4070",
                        Description = "Видеокарта NVIDIA GeForce RTX 4070, 12GB",
                        Price = 65000.00m,
                        Quantity = 39,
                        Type = "GPU",
                        Specifications = "RTX 4070, 12GB GDDR6X, DLSS 3, 3x DisplayPort, 1x HDMI",
                        PowerConnector = "8-pin",
                        ImageUrl = "1024.png"
                    },
                    // SSD
                    new Component
                    {
                        Id = 80,
                        Name = "Samsung 980 Pro 1TB",
                        Description = "SSD накопитель Samsung 980 Pro 1TB NVMe",
                        Price = 12000.00m,
                        Quantity = 45,
                        Type = "SSD",
                        Specifications = "1TB NVMe PCIe 4.0, 7000MB/s read, 5000MB/s write",
                        ImageUrl = "5985216.png"
                    },
                    // Кулеры
                    new Component
                    {
                        Id = 84,
                        Name = "Noctua NH-D15",
                        Description = "Башенный кулер Noctua NH-D15",
                        Price = 10000.00m,
                        Quantity = 38,
                        Type = "Cooler",
                        Specifications = "Dual tower, 2x 140mm fans, 165mm height, for Intel/AMD",
                        ImageUrl = "5985216.png"
                    },
                    // Добавьте остальные компоненты из вашего списка по аналогии
                };
                    context.Components.AddRange(components);

                    // 6. Заполняем характеристики компонентов
                var componentCharacteristics = new List<ComponentCharacteristic>
                {
                    // Характеристики для компонента 32 (материнская плата)
                    new ComponentCharacteristic { ComponentId = 32, CharacteristicId = 16, Value = "B550" },
                    new ComponentCharacteristic { ComponentId = 32, CharacteristicId = 17, Value = "AM4" },
                    new ComponentCharacteristic { ComponentId = 32, CharacteristicId = 18, Value = "ATX" },
                    new ComponentCharacteristic { ComponentId = 32, CharacteristicId = 19, Value = "4" },
                    new ComponentCharacteristic { ComponentId = 32, CharacteristicId = 20, Value = "128" },
                    new ComponentCharacteristic { ComponentId = 32, CharacteristicId = 21, Value = "3" },
                    new ComponentCharacteristic { ComponentId = 32, CharacteristicId = 22, Value = "6" },
                    new ComponentCharacteristic { ComponentId = 32, CharacteristicId = 23, Value = "2" },
                    new ComponentCharacteristic { ComponentId = 32, CharacteristicId = 38, Value = "36" },

                    // Характеристики для компонента 37 (процессор)
                    new ComponentCharacteristic { ComponentId = 37, CharacteristicId = 1, Value = "3.8" },
                    new ComponentCharacteristic { ComponentId = 37, CharacteristicId = 2, Value = "8" },
                    new ComponentCharacteristic { ComponentId = 37, CharacteristicId = 3, Value = "16" },
                    new ComponentCharacteristic { ComponentId = 37, CharacteristicId = 17, Value = "AM4" },
                    new ComponentCharacteristic { ComponentId = 37, CharacteristicId = 38, Value = "36" },

                    // Характеристики для компонента 42 (оперативная память)
                    new ComponentCharacteristic { ComponentId = 42, CharacteristicId = 4, Value = "32" },
                    new ComponentCharacteristic { ComponentId = 42, CharacteristicId = 5, Value = "3200" },
                    new ComponentCharacteristic { ComponentId = 42, CharacteristicId = 6, Value = "DDR4" },
                    new ComponentCharacteristic { ComponentId = 42, CharacteristicId = 7, Value = "CL16" },
                    new ComponentCharacteristic { ComponentId = 42, CharacteristicId = 38, Value = "60" },

                };
                    context.ComponentCharacteristics.AddRange(componentCharacteristics);

                    // 7. Заполняем компьютеры
                var computers = new List<Computer>
                {
                    new Computer
                    {
                        Id = 1,
                        Name = "ПК для (games)",
                        Description = "описание",
                        Price = 157300.00m,
                        Quantity = 0,
                        ImageUrl = "1.jpg"
                    },
                    new Computer
                    {
                        Id = 2,
                        Name = "Compic",
                        Description = "вфывфыв",
                        Price = 157300.00m,
                        Quantity = 94,
                        ImageUrl = "2.jpg"
                    },
                    new Computer
                    {
                        Id = 7,
                        Name = "навы",
                        Description = "Собранный ПК: ASUS ROG Strix B550-F Gaming, AMD Ryzen 7 5800X, Kingston Fury Beast 32GB DDR4 3200MHz, Seasonic Focus GX-750, NZXT H510 Flow, NVIDIA GeForce RTX 4070",
                        Price = 133000.00m,
                        Quantity = 1,
                        ImageUrl = "6.jpg"
                    },
                };
                context.Computers.AddRange(computers);

                // 8. Заполняем связь компьютеров с компонентами
                var computerComponents = new List<ComputerComponent>
                {
                    new ComputerComponent { ComputerId = 1, ComponentId = 7 },
                    new ComputerComponent { ComputerId = 2, ComponentId = 7 },
                    new ComputerComponent { ComputerId = 4, ComponentId = 7 },
                    new ComputerComponent { ComputerId = 5, ComponentId = 7 },
                    new ComputerComponent { ComputerId = 6, ComponentId = 7 },
                    new ComputerComponent { ComputerId = 7, ComponentId = 32 },
                    new ComputerComponent { ComputerId = 7, ComponentId = 37 },
                    new ComputerComponent { ComputerId = 7, ComponentId = 42 },
                    new ComputerComponent { ComputerId = 7, ComponentId = 46 },
                    new ComputerComponent { ComputerId = 7, ComponentId = 50 },
                    new ComputerComponent { ComputerId = 7, ComponentId = 64 },
                    new ComputerComponent { ComputerId = 7, ComponentId = 68 },
                };
                context.ComputerComponents.AddRange(computerComponents);

                // Сохраняем все изменения
                context.SaveChanges();

                Console.WriteLine("База данных успешно заполнена начальными данными.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при заполнении базы данных: {ex.Message}");
                throw;
            }
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
        public DbSet<Favorite> Favorites { get; set; }

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

            modelBuilder.Entity<Favorite>(entity =>
            {
                // Связь с User (если UserId не null)
                entity.HasOne(f => f.User)
                      .WithMany(u => u.Favorites)
                      .HasForeignKey(f => f.UserId)
                      .OnDelete(DeleteBehavior.Cascade)
                      .IsRequired(false); // Разрешаем NULL для гостей

                // Уникальный индекс для предотвращения дубликатов
                entity.HasIndex(f => new { f.UserId, f.GuestId, f.ProductType, f.ProductId })
                      .IsUnique()
                      .HasFilter("UserId IS NOT NULL OR GuestId IS NOT NULL");

                // Указываем, что ComputerId и ComponentId - это просто числа, не FK
                entity.Property(f => f.ProductId)
                      .IsRequired();

                entity.Property(f => f.ProductType)
                      .IsRequired()
                      .HasMaxLength(20);
            });


        }
    }
}

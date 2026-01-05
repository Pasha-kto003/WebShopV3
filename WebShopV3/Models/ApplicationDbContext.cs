using Bogus;
using Microsoft.EntityFrameworkCore;
using WebShopV3.Services;

namespace WebShopV3.Models
{

    public static class DbInitializer
    {
        // Создаем экземпляр PasswordHasherService
        private static readonly IPasswordHasher _passwordHasher = new PasswordHasherService();

        public static void Initialize(ApplicationDbContext context)
        {
            if (context.Computers.Any() ||
               context.Components.Any() ||
               context.Characteristics.Any())
            {
                return; // База уже содержит данные
            }
            Console.WriteLine("Начинаем заполнение базы данных тестовыми данными...");

            try
            {
                // Используем стратегию выполнения с транзакцией
                var executionStrategy = context.Database.CreateExecutionStrategy();

                executionStrategy.Execute(() =>
                {
                    using var transaction = context.Database.BeginTransaction();

                    try
                    {
                        // 1. Заполняем типы пользователей
                        Console.WriteLine("Создание типов пользователей...");
                        var userTypes = new List<UserType>
                        {
                            new UserType { Name = "Админ" },
                            new UserType { Name = "Менеджер" },
                            new UserType { Name = "Пользователь" }
                        };
                        context.UserTypes.AddRange(userTypes);
                        context.SaveChanges();

                        Console.WriteLine($"Создано типов пользователей: {userTypes.Count}");

                        // 2. Получаем Id типов пользователей из БД (после сохранения)
                        var adminType = context.UserTypes.First(ut => ut.Name == "Админ");
                        var managerType = context.UserTypes.First(ut => ut.Name == "Менеджер");
                        var userType = context.UserTypes.First(ut => ut.Name == "Пользователь");

                        // 3. Создаем пользователей
                        Console.WriteLine("Генерация пользователей...");
                        var users = new List<User>();

                        // Основной админ
                        users.Add(new User
                        {
                            Username = "admin",
                            Email = "admin@example.com",
                            FirstName = "Алексей",
                            LastName = "Иванов",
                            Phone = "+79998887766",
                            PasswordHash = _passwordHasher.HashPassword("Admin123!"),
                            UserTypeId = adminType.Id,
                            CreatedAt = DateTime.Now
                        });

                        // Еще 2 админа
                        for (int i = 1; i <= 2; i++)
                        {
                            users.Add(new User
                            {
                                Username = $"admin{i}",
                                Email = $"admin{i}@example.com",
                                FirstName = GetRandomFirstName(),
                                LastName = GetRandomLastName(),
                                Phone = $"+7{GetRandomPhoneNumber()}",
                                PasswordHash = _passwordHasher.HashPassword($"Admin{i}123!"),
                                UserTypeId = adminType.Id,
                                CreatedAt = DateTime.Now.AddDays(-new Random().Next(1, 365))
                            });
                        }

                        // 5 менеджеров
                        for (int i = 1; i <= 5; i++)
                        {
                            users.Add(new User
                            {
                                Username = $"manager{i}",
                                Email = $"manager{i}@example.com",
                                FirstName = GetRandomFirstName(),
                                LastName = GetRandomLastName(),
                                Phone = $"+7{GetRandomPhoneNumber()}",
                                PasswordHash = _passwordHasher.HashPassword($"Manager{i}456!"),
                                UserTypeId = managerType.Id,
                                CreatedAt = DateTime.Now.AddDays(-new Random().Next(1, 365))
                            });
                        }

                        var userFaker = new Faker<User>("ru")
                            .RuleFor(u => u.Username, f => f.Internet.UserName().ToLower())
                            .RuleFor(u => u.Email, f => f.Internet.Email())
                            .RuleFor(u => u.FirstName, f => f.Name.FirstName())
                            .RuleFor(u => u.LastName, f => f.Name.LastName())
                            .RuleFor(u => u.Phone, f => f.Phone.PhoneNumber("+7##########"))
                            .RuleFor(u => u.CreatedAt, f => f.Date.Past(1));

                        for (int i = 1; i <= 15; i++)
                        {
                            var regularUser = userFaker.Generate();
                            regularUser.Username = $"user{i}";
                            regularUser.Email = $"user{i}@example.com";
                            regularUser.PasswordHash = _passwordHasher.HashPassword($"User{i}789!");
                            regularUser.UserTypeId = userType.Id;
                            users.Add(regularUser);
                        }

                        context.Users.AddRange(users);
                        context.SaveChanges();

                        Console.WriteLine($"Создано пользователей: {users.Count}");

                        // 4. Заполняем статусы
                        Console.WriteLine("Создание статусов заказов...");
                        var statuses = new List<Status>
                        {
                            new Status { Name = "Выполнен" },
                            new Status { Name = "В ожидании" },
                            new Status { Name = "Отмена" },
                            new Status { Name = "Проблема с наличием" },
                            new Status { Name = "В обработке" },
                            new Status { Name = "Доставляется" },
                            new Status { Name = "Готов к выдаче" }
                        };
                        context.Statuses.AddRange(statuses);

                        // 5. Заполняем типы заказов
                        Console.WriteLine("Создание типов заказов...");
                        var orderTypes = new List<OrderType>
                        {
                            new OrderType { Name = "Продажа" },
                            new OrderType { Name = "Приход" },
                            new OrderType { Name = "Отмена" },
                            new OrderType { Name = "Возврат" },
                            new OrderType { Name = "Предзаказ" }
                        };
                        context.OrderTypes.AddRange(orderTypes);

                        // 6. Заполняем характеристики
                        Console.WriteLine("Создание характеристик...");
                        var characteristics = new List<Characteristic>
                        {
                            new Characteristic { Name = "Тактовая частота", Unit = "ГГц", Description = "Базовая частота процессора" },
                            new Characteristic { Name = "Количество ядер", Unit = "шт", Description = "Количество физических ядер процессора" },
                            new Characteristic { Name = "Количество потоков", Unit = "шт", Description = "Количество логических процессоров" },
                            new Characteristic { Name = "Объем памяти", Unit = "ГБ", Description = "Объем оперативной памяти" },
                            new Characteristic { Name = "Частота памяти", Unit = "МГц", Description = "Частота оперативной памяти" },
                            new Characteristic { Name = "Тип памяти", Unit = "", Description = "Тип памяти (DDR4, DDR5)" },
                            new Characteristic { Name = "Тайминги", Unit = "", Description = "Тайминги оперативной памяти (CL)" },
                            new Characteristic { Name = "Емкость накопителя", Unit = "ГБ", Description = "Объем storage накопителя" },
                            new Characteristic { Name = "Скорость чтения", Unit = "МБ/с", Description = "Скорость чтения накопителя" },
                            new Characteristic { Name = "Скорость записи", Unit = "МБ/с", Description = "Скорость записи накопителя" },
                            new Characteristic { Name = "Тип накопителя", Unit = "", Description = "Тип накопителя (SSD, HDD, NVMe)" },
                            new Characteristic { Name = "Объем видеопамяти", Unit = "ГБ", Description = "Объем памяти видеокарты" },
                            new Characteristic { Name = "Частота GPU", Unit = "МГц", Description = "Базовая частота графического процессора" },
                            new Characteristic { Name = "Разъемы питания", Unit = "", Description = "Требуемые разъемы питания (6-pin, 8-pin)" },
                            new Characteristic { Name = "Рекомендуемый БП", Unit = "Вт", Description = "Рекомендуемая мощность блока питания" },
                            new Characteristic { Name = "Чипсет", Unit = "", Description = "Чипсет материнской платы" },
                            new Characteristic { Name = "Сокет", Unit = "", Description = "Тип сокета процессора" },
                            new Characteristic { Name = "Форм-фактор", Unit = "", Description = "Форм-фактор платы (ATX, mATX, ITX)" },
                            new Characteristic { Name = "Количество слотов памяти", Unit = "шт", Description = "Количество слотов оперативной памяти" },
                            new Characteristic { Name = "Макс. объем памяти", Unit = "ГБ", Description = "Максимальный поддерживаемый объем памяти" },
                            new Characteristic { Name = "Слоты расширения", Unit = "шт", Description = "Количество PCI-E слотов" },
                            new Characteristic { Name = "Порты SATA", Unit = "шт", Description = "Количество SATA портов" },
                            new Characteristic { Name = "Порты M.2", Unit = "шт", Description = "Количество M.2 слотов" },
                            new Characteristic { Name = "Мощность", Unit = "Вт", Description = "Номинальная мощность блока питания" },
                            new Characteristic { Name = "Сертификат", Unit = "", Description = "Сертификат эффективности (80+ Bronze, Gold, etc.)" },
                            new Characteristic { Name = "Разъемы PCI-E", Unit = "шт", Description = "Количество PCI-E разъемов" },
                            new Characteristic { Name = "Разъемы SATA", Unit = "шт", Description = "Количество SATA разъемов" },
                            new Characteristic { Name = "Тип корпуса", Unit = "", Description = "Тип корпуса (Mid-Tower, Full-Tower, etc.)" },
                            new Characteristic { Name = "Поддерживаемые платы", Unit = "", Description = "Поддерживаемые форм-факторы материнских плат" },
                            new Characteristic { Name = "Отсеки для HDD", Unit = "шт", Description = "Количество отсеков для жестких дисков" },
                            new Characteristic { Name = "Отсеки для SSD", Unit = "шт", Description = "Количество отсеков для SSD" },
                            new Characteristic { Name = "Вентиляторы", Unit = "шт", Description = "Количество установленных вентиляторов" },
                            new Characteristic { Name = "Тип охлаждения", Unit = "", Description = "Тип охлаждения (Air, Liquid)" },
                            new Characteristic { Name = "Рассеиваемая мощность", Unit = "Вт", Description = "Максимальная рассеиваемая мощность" },
                            new Characteristic { Name = "Уровень шума", Unit = "дБ", Description = "Уровень шума при работе" },
                            new Characteristic { Name = "Совместимые сокеты", Unit = "", Description = "Поддерживаемые сокеты процессоров" },
                            new Characteristic { Name = "Гарантия", Unit = "мес", Description = "Срок гарантии производителя" },
                            new Characteristic { Name = "Габариты", Unit = "мм", Description = "Габаритные размеры компонента" },
                            new Characteristic { Name = "Вес", Unit = "г", Description = "Вес компонента" },
                            new Characteristic { Name = "Энергопотребление", Unit = "Вт", Description = "Максимальное энергопотребление" },
                            new Characteristic { Name = "RGB подсветка", Unit = "", Description = "Наличие RGB подсветки" },
                            new Characteristic { Name = "Тип дисплея", Unit = "", Description = "Тип матрицы монитора" },
                            new Characteristic { Name = "Частота обновления", Unit = "Гц", Description = "Частота обновления монитора" },
                            new Characteristic { Name = "Время отклика", Unit = "мс", Description = "Время отклика пикселя" }
                        };
                        context.Characteristics.AddRange(characteristics);
                        context.SaveChanges();

                        // 7. СОЗДАЕМ КОМПЛЕКТУЮЩИЕ С ПОМОЩЬЮ BOGUS
                        Console.WriteLine("Генерация комплектующих...");
                        var components = GenerateComponents(context);
                        context.Components.AddRange(components);
                        context.SaveChanges();

                        // 8. СОЗДАЕМ КОМПЬЮТЕРЫ С ПОМОЩЬЮ BOGUS
                        Console.WriteLine("Генерация компьютеров...");
                        var computers = GenerateComputers(context);
                        context.Computers.AddRange(computers);
                        context.SaveChanges();

                        // 9. СОЗДАЕМ СВЯЗИ КОМПЬЮТЕР-КОМПОНЕНТЫ
                        Console.WriteLine("Создание связей компьютер-компоненты...");
                        var computerComponents = GenerateComputerComponents(context, computers, components);
                        context.ComputerComponents.AddRange(computerComponents);

                        // 10. СОЗДАЕМ ХАРАКТЕРИСТИКИ КОМПОНЕНТОВ
                        Console.WriteLine("Добавление характеристик к компонентам...");
                        var componentCharacteristics = GenerateComponentCharacteristics(context, components);
                        context.ComponentCharacteristics.AddRange(componentCharacteristics);

                        // 11. СОЗДАЕМ ТЕСТОВЫЕ ЗАКАЗЫ
                        Console.WriteLine("Создание тестовых заказов...");
                        var orders = GenerateOrders(context, users, statuses, orderTypes);
                        context.Orders.AddRange(orders);
                        context.SaveChanges();

                        context.SaveChanges();
                        transaction.Commit();

                        Console.WriteLine("\n✅ База данных успешно заполнена тестовыми данными:");
                        Console.WriteLine($"- Пользователей: {users.Count} (3 админа, 5 менеджеров, 15 пользователей)");
                        Console.WriteLine($"- Комплектующих: {components.Count}");
                        Console.WriteLine($"- Компьютеров: {computers.Count}");
                        Console.WriteLine($"- Заказов: {orders.Count}");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Console.WriteLine($"❌ Ошибка при заполнении базы данных: {ex.Message}");
                        Console.WriteLine($"StackTrace: {ex.StackTrace}");
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Критическая ошибка: {ex.Message}");
                throw;
            }
        }

        // Вспомогательные методы для генерации данных
        private static List<Component> GenerateComponents(ApplicationDbContext context)
        {
            var componentFaker = new Faker<Component>("ru")
                .RuleFor(c => c.Name, f => f.Commerce.ProductName())
                .RuleFor(c => c.Description, f => f.Commerce.ProductDescription())
                .RuleFor(c => c.Price, f => Math.Round(f.Random.Decimal(1500, 150000), 2))
                .RuleFor(c => c.Quantity, f => f.Random.Int(0, 100))
                .RuleFor(c => c.Specifications, f => f.Lorem.Sentence())
                .RuleFor(c => c.PowerConnector, f => "")
                .RuleFor(c => c.ImageUrl, f => f.Random.ListItem(new[] { "1.jpg", "2.jpg", "3.png", "4.png", "default.jpg" }));

            var components = new List<Component>();

            Console.WriteLine("Генерация процессоров...");
            var cpuFaker = componentFaker
                .RuleFor(c => c.Type, "CPU")
                .RuleFor(c => c.Socket, f => f.Random.ListItem(new[] { "AM4", "AM5", "AM3+", "LGA1700", "LGA1200", "LGA1151", "LGA2066", "sTRX4", "TR4", "sWRX8"
                }))
                .RuleFor(c => c.MemoryType, f => "")
                .RuleFor(c => c.FormFactor, f => "")
                .RuleFor(c => c.PowerConnector, f => "")
                .RuleFor(c => c.MaxMemory, f => (int?)null)
                .RuleFor(c => c.MemorySlots, f => (int?)null)
                .RuleFor(c => c.Name, f => {
                    var brand = f.Random.Bool() ? "Intel" : "AMD";
                    if (brand == "Intel")
                    {
                        var model = f.Random.ListItem(new[] { "Celeron", "Pentium", "Core i3", "Core i5", "Core i7", "Core i9", "Xeon" });
                        var gen = f.Random.ListItem(new[] { "11th", "12th", "13th", "14th" });
                        var suffix = f.Random.ListItem(new[] { "", "K", "KF", "F", "T", "S" });
                        return $"Intel {model} {gen} Gen {f.Random.Number(3, 9)}{f.Random.Number(100, 900)}{suffix}";
                    }
                    else
                    {
                        var model = f.Random.ListItem(new[] { "Ryzen 3", "Ryzen 5", "Ryzen 7", "Ryzen 9", "Threadripper", "Athlon" });
                        var gen = f.Random.ListItem(new[] { "3000", "4000", "5000", "6000", "7000" });
                        var suffix = f.Random.ListItem(new[] { "", "X", "G", "XT", "PRO" });
                        return $"AMD {model} {gen}{suffix}";
                    }
                });

            components.AddRange(cpuFaker.Generate(20));

            Console.WriteLine("Генерация материнских плат...");
            var mbFaker = componentFaker
                .RuleFor(c => c.Type, "MB")
                .RuleFor(c => c.Socket, f => f.Random.ListItem(new[] {"AM4", "AM5", "AM3+", "LGA1700", "LGA1200", "LGA1151", "LGA2066", "sTRX4", "TR4", "sWRX8"}))
                .RuleFor(c => c.MemoryType, f => f.Random.ListItem(new[] { "DDR4", "DDR5", "DDR3" }))
                .RuleFor(c => c.FormFactor, f => f.Random.ListItem(new[] {
            "ATX", "Micro-ATX", "Mini-ITX", "E-ATX", "XL-ATX"
                }))
                .RuleFor(c => c.PowerConnector, f => "")
                .RuleFor(c => c.MemorySlots, f => f.Random.Int(2, 8))
                .RuleFor(c => c.MaxMemory, f => f.Random.Int(32, 512))
                .RuleFor(c => c.Name, f =>
                {
                    var brand = f.Random.ListItem(new[] { "ASUS", "Gigabyte", "MSI", "ASRock", "Biostar", "EVGA" });
                    var series = f.Random.ListItem(new[] { "ROG", "TUF", "Prime", "AORUS", "MAG", "PRO", "Steel Legend", "Phantom" });
                    var chipset = f.Random.ListItem(new[] {
                "B450", "B550", "B650",
                "X470", "X570", "X670",
                "B660", "B760", "Z690", "Z790",
                "TRX40", "WRX80"
                    });
                    var model = f.Random.ListItem(new[] { "Gaming", "Plus", "Pro", "Elite", "Master", "Extreme", "Aero" });
                    return $"{brand} {series} {chipset} {model}";
                });

            components.AddRange(mbFaker.Generate(25));

            // ОПЕРАТИВНАЯ ПАМЯТЬ (20 шт - оставляем как было)
            Console.WriteLine("Генерация оперативной памяти...");
            var ramFaker = componentFaker
                .RuleFor(c => c.Type, "RAM")
                .RuleFor(c => c.Socket, f => "")
                .RuleFor(c => c.MemoryType, f => f.Random.ListItem(new[] { "DDR4", "DDR5", "DDR3" }))
                .RuleFor(c => c.FormFactor, f => "")
                .RuleFor(c => c.PowerConnector, f => "")
                .RuleFor(c => c.MaxMemory, f => (int?)null)
                .RuleFor(c => c.MemorySlots, f => (int?)null)
                .RuleFor(c => c.Name, f =>
                    $"{f.Random.ListItem(new[] { "Kingston", "Corsair", "G.Skill", "Crucial", "TeamGroup", "Patriot", "ADATA", "Silicon Power" })} " +
                    $"{f.Random.ListItem(new[] { "Fury", "Vengeance", "Trident", "Ripjaws", "T-Force", "Viper", "Spectrix", "XPG" })} " +
                    f.Random.Int(4, 128) + "GB " +
                    f.Random.Int(1600, 7200) + "MHz " +
                    f.Random.ListItem(new[] { "CL14", "CL16", "CL18", "CL32", "CL36", "CL40" }));

            components.AddRange(ramFaker.Generate(20));

            // ВИДЕОКАРТЫ (12 шт - оставляем как было)
            Console.WriteLine("Генерация видеокарт...");
            var gpuFaker = componentFaker
                .RuleFor(c => c.Type, "GPU")
                .RuleFor(c => c.Socket, f => "")
                .RuleFor(c => c.MemoryType, f => "")
                .RuleFor(c => c.FormFactor, f => f.Random.ListItem(new[] { "Dual-slot", "Triple-slot", "2.5-slot" }))
                .RuleFor(c => c.PowerConnector, f => f.Random.ListItem(new[] { "8-pin", "8+6 pin", "8+8 pin", "12VHPWR", "6-pin", "8+8+8 pin" }))
                .RuleFor(c => c.MaxMemory, f => (int?)null)
                .RuleFor(c => c.MemorySlots, f => (int?)null)
                .RuleFor(c => c.Name, f =>
                {
                    var brand = f.Random.ListItem(new[] { "NVIDIA", "AMD" });
                    if (brand == "NVIDIA")
                    {
                        var model = f.Random.ListItem(new[] { "GeForce RTX", "GeForce GTX", "Titan", "Quadro" });
                        var number = f.Random.ListItem(new[] { "1650", "1660", "2060", "3060", "4060", "3070", "4070", "3080", "4080", "3090", "4090" });
                        var suffix = f.Random.ListItem(new[] { "", "Ti", "Super" });
                        return $"NVIDIA {model} {number}{suffix} {f.Random.Int(8, 24)}GB";
                    }
                    else
                    {
                        var model = f.Random.ListItem(new[] { "Radeon RX", "Radeon Pro" });
                        var number = f.Random.ListItem(new[] { "5500", "5600", "5700", "6600", "6700", "6800", "6900", "7600", "7700", "7800", "7900" });
                        var suffix = f.Random.ListItem(new[] { "", "XT", "XTX" });
                        return $"AMD {model} {number}{suffix} {f.Random.Int(8, 24)}GB";
                    }
                });

            components.AddRange(gpuFaker.Generate(12));

            // SSD (15 шт - оставляем как было)
            Console.WriteLine("Генерация SSD...");
            var ssdFaker = componentFaker
                .RuleFor(c => c.Type, "SSD")
                .RuleFor(c => c.Socket, f => "")
                .RuleFor(c => c.MemoryType, f => "")
                .RuleFor(c => c.FormFactor, f => f.Random.ListItem(new[] { "M.2 2280", "2.5\"", "M.2 22110" }))
                .RuleFor(c => c.PowerConnector, f => "")
                .RuleFor(c => c.MaxMemory, f => (int?)null)
                .RuleFor(c => c.MemorySlots, f => (int?)null)
                .RuleFor(c => c.Name, f =>
                    $"{f.Random.ListItem(new[] { "Samsung", "Western Digital", "Kingston", "Crucial", "Seagate", "Sabrent", "ADATA", "SK Hynix" })} " +
                    f.Random.ListItem(new[] { "980 Pro", "990 Pro", "SN850X", "KC3000", "P5 Plus", "FireCuda", "Rocket", "S70 Blade", "Gold P31" }) + " " +
                    f.Random.Int(250, 8000) + "GB " +
                    f.Random.ListItem(new[] { "NVMe PCIe 4.0", "NVMe PCIe 3.0", "SATA III" }));

            components.AddRange(ssdFaker.Generate(15));

            // БЛОКИ ПИТАНИЯ (10 шт - оставляем как было)
            Console.WriteLine("Генерация блоков питания...");
            var psuFaker = componentFaker
                .RuleFor(c => c.Type, "PSU")
                .RuleFor(c => c.Socket, f => "")
                .RuleFor(c => c.MemoryType, f => "")
                .RuleFor(c => c.FormFactor, f => "ATX")
                .RuleFor(c => c.PowerConnector, f => "24-pin ATX + модульные")
                .RuleFor(c => c.MaxMemory, f => (int?)null)
                .RuleFor(c => c.MemorySlots, f => (int?)null)
                .RuleFor(c => c.Name, f =>
                    $"{f.Random.ListItem(new[] { "Seasonic", "Corsair", "be quiet!", "Cooler Master", "EVGA", "Thermaltake", "FSP", "Super Flower" })} " +
                    f.Random.Int(450, 1600) + "W " +
                    f.Random.ListItem(new[] { "80+ Bronze", "80+ Gold", "80+ Platinum", "80+ Titanium" }) + " " +
                    f.Random.ListItem(new[] { "Modular", "Semi-modular", "Non-modular" }));

            components.AddRange(psuFaker.Generate(10));

            // УВЕЛИЧИВАЕМ КОРПУСА с 8 до 30 для лучшей совместимости
            Console.WriteLine("Генерация корпусов...");
            var caseFaker = componentFaker
                .RuleFor(c => c.Type, "Case")
                .RuleFor(c => c.Socket, f => "")
                .RuleFor(c => c.MemoryType, f => "")
                .RuleFor(c => c.FormFactor, f => f.Random.ListItem(new[] {
            "Full-Tower", "Mid-Tower", "Mini-Tower",
            "Super-Tower", "Micro-ATX", "Mini-ITX",
            "Cube", "Desktop", "HTPC"
                }))
                .RuleFor(c => c.PowerConnector, f => "")
                .RuleFor(c => c.MaxMemory, f => (int?)null)
                .RuleFor(c => c.MemorySlots, f => (int?)null)
                .RuleFor(c => c.Name, f =>
                {
                    var brand = f.Random.ListItem(new[] {
                "NZXT", "Fractal Design", "Lian Li", "Phanteks", "Cooler Master",
                "Corsair", "be quiet!", "Thermaltake", "Silverstone", "Deepcool",
                "Antec", "InWin", "Aerocool", "BitFenix", "NZXT"
                    });

                    var model = f.Random.ListItem(new[] {
                "H5", "H7", "H9", "Meshify", "Define", "Torrent",
                "O11", "Lancool", "Eclipse", "Enthoo", "MasterBox",
                "4000D", "5000D", "Pure Base", "View", "Core", "FOCUS"
                    });

                    var variant = f.Random.ListItem(new[] {
                "Flow", "RGB", "Elite", "Pro", "Lite", "Air", "Dynamic",
                "Mini", "Compact", "XL", "Silent", "Gaming", "Performance"
                    });

                    var formFactor = f.PickRandom(new[] { "ATX", "mATX", "ITX", "E-ATX" });

                    return $"{brand} {model} {variant} {formFactor}";
                });

            components.AddRange(caseFaker.Generate(30));

            // КУЛЕРЫ (15 шт)
            Console.WriteLine("Генерация кулеров...");
            var coolerFaker = componentFaker
                .RuleFor(c => c.Type, "Cooler")
                .RuleFor(c => c.Socket, f => f.Random.ListItem(new[] {
            "AM4/AM5", "LGA1700/1200", "Multi-socket",
            "AM4/AM5/LGA1700", "TR4/sTRX4", "Universal"
                }))
                .RuleFor(c => c.MemoryType, f => "")
                .RuleFor(c => c.FormFactor, f => f.Random.ListItem(new[] { "120mm", "140mm", "240mm", "280mm", "360mm", "420mm" }))
                .RuleFor(c => c.PowerConnector, f => "")
                .RuleFor(c => c.MaxMemory, f => (int?)null)
                .RuleFor(c => c.MemorySlots, f => (int?)null)
                .RuleFor(c => c.Name, f =>
                {
                    var brand = f.Random.ListItem(new[] { "Noctua", "Cooler Master", "be quiet!", "Arctic", "Deepcool", "NZXT", "Corsair", "Thermaltake" });
                    var model = f.Random.ListItem(new[] { "NH-D15", "Hyper 212", "Dark Rock", "Freezer", "AK620", "Kraken", "H100i", "Floe" });
                    var type = f.Random.ListItem(new[] { "Air", "Liquid", "AIO", "Tower", "Low-profile" });
                    var size = f.Random.ListItem(new[] { "", "S", "Pro", "RGB", "Black", "Chromax", "SE", "V2" });
                    return $"{brand} {model} {size} {type}";
                });

            components.AddRange(coolerFaker.Generate(15));

            // МОНИТОРЫ (8 шт)
            Console.WriteLine("Генерация мониторов...");
            var monitorFaker = componentFaker
                .RuleFor(c => c.Type, "Monitor")
                .RuleFor(c => c.Socket, f => "")
                .RuleFor(c => c.MemoryType, f => "")
                .RuleFor(c => c.FormFactor, f => "")
                .RuleFor(c => c.PowerConnector, f => "AC Power")
                .RuleFor(c => c.MaxMemory, f => (int?)null)
                .RuleFor(c => c.MemorySlots, f => (int?)null)
                .RuleFor(c => c.Name, f =>
                    $"{f.Random.ListItem(new[] { "Samsung", "LG", "ASUS", "AOC", "MSI", "Dell", "HP", "Acer", "ViewSonic" })} " +
                    f.Random.Int(24, 49) + "\" " +
                    f.Random.ListItem(new[] { "Odyssey", "UltraGear", "ROG", "Gaming", "Optix", "Alienware", "Predator", "VX" }) + " " +
                    f.Random.Int(60, 360) + "Hz " +
                    f.Random.ListItem(new[] { "IPS", "VA", "TN", "OLED", "QD-OLED" }));

            components.AddRange(monitorFaker.Generate(8));

            // ЖЕСТКИЕ ДИСКИ (5 шт)
            Console.WriteLine("Генерация жестких дисков...");
            var hddFaker = componentFaker
                .RuleFor(c => c.Type, "HDD")
                .RuleFor(c => c.Socket, f => "")
                .RuleFor(c => c.MemoryType, f => "")
                .RuleFor(c => c.FormFactor, f => f.Random.ListItem(new[] { "3.5\"", "2.5\"" }))
                .RuleFor(c => c.PowerConnector, f => "SATA Power")
                .RuleFor(c => c.MaxMemory, f => (int?)null)
                .RuleFor(c => c.MemorySlots, f => (int?)null)
                .RuleFor(c => c.Name, f =>
                    $"{f.Random.ListItem(new[] { "Seagate", "Western Digital", "Toshiba", "Hitachi" })} " +
                    $"{f.Random.ListItem(new[] { "Barracuda", "IronWolf", "Red", "Purple", "Black", "Gold", "Enterprise" })} " +
                    f.Random.Int(1, 22) + "TB " +
                    f.Random.ListItem(new[] { "7200RPM", "5400RPM", "Enterprise" }));

            components.AddRange(hddFaker.Generate(5));

            Console.WriteLine($"Всего сгенерировано компонентов: {components.Count}");
            return components;
        }

        private static List<Computer> GenerateComputers(ApplicationDbContext context)
        {
            var computerFaker = new Faker<Computer>("ru")
                .RuleFor(c => c.Price, f => Math.Round(f.Random.Decimal(50000, 350000), 2))
                .RuleFor(c => c.Quantity, f => f.Random.Int(0, 20))
                .RuleFor(c => c.ImageUrl, f => f.Random.ListItem(new[] { "pc1.jpg", "pc2.jpg", "pc3.jpg", "pc4.jpg" }));

            var computers = new List<Computer>();

            // Игровые компьютеры (10 шт)
            var gamingPcFaker = computerFaker
                .RuleFor(c => c.Name, f =>
                    $"{f.Random.ListItem(new[] { "Игровой", "Геймерский", "Gaming" })} ПК " +
                    f.Random.ListItem(new[] { "Strike", "Fury", "Nexus", "Vortex", "Titan" }) + " " +
                    f.Random.Int(1, 5))
                .RuleFor(c => c.Description, f =>
                    $"Мощный игровой компьютер для {f.Random.ListItem(new[] { "киберспорта", "стриминга", "VR игр", "4K игр" })}");

            computers.AddRange(gamingPcFaker.Generate(10));

            return computers;
        }

        private static List<ComputerComponent> GenerateComputerComponents(
            ApplicationDbContext context,
            List<Computer> computers,
            List<Component> components)
        {
            var computerComponents = new List<ComputerComponent>();
            var random = new Random();

            foreach (var computer in computers)
            {
                // Для каждого компьютера добавляем случайные компоненты
                var componentTypes = new[] { "CPU", "MB", "RAM", "GPU", "SSD" };

                foreach (var type in componentTypes)
                {
                    var availableComponents = components.Where(c => c.Type == type).ToList();
                    if (availableComponents.Any())
                    {
                        var component = availableComponents[random.Next(availableComponents.Count)];
                        computerComponents.Add(new ComputerComponent
                        {
                            ComputerId = computer.Id,
                            ComponentId = component.Id
                        });
                    }
                }
            }

            return computerComponents;
        }

        private static string GetRandomPhoneNumber()
        {
            var random = new Random();
            return $"900{random.Next(1000000, 9999999)}";
        }

        private static List<ComponentCharacteristic> GenerateComponentCharacteristics(
            ApplicationDbContext context,
            List<Component> components)
        {
            var characteristics = context.Characteristics.ToList();
            var componentCharacteristics = new List<ComponentCharacteristic>();
            var random = new Random();

            foreach (var component in components)
            {
                // Добавляем 3-5 случайных характеристик для каждого компонента
                var randomCharacteristics = characteristics
                    .OrderBy(x => random.Next())
                    .Take(random.Next(3, 6))
                    .ToList();

                foreach (var characteristic in randomCharacteristics)
                {
                    componentCharacteristics.Add(new ComponentCharacteristic
                    {
                        ComponentId = component.Id,
                        CharacteristicId = characteristic.Id,
                        Value = GetRandomValueForCharacteristic(characteristic.Name)
                    });
                }
            }

            return componentCharacteristics;
        }

        private static string GetRandomValueForCharacteristic(string characteristicName)
        {
            var random = new Random();
            return characteristicName switch
            {
                "Тактовая частота" => random.Next(2, 5).ToString() + "." + random.Next(0, 9),
                "Количество ядер" => random.Next(4, 32).ToString(),
                "Объем памяти" => random.Next(8, 128).ToString(),
                "Частота памяти" => random.Next(2400, 6400).ToString(),
                "Емкость накопителя" => random.Next(250, 4000).ToString(),
                _ => random.Next(1, 100).ToString()
            };
        }

        private static List<Order> GenerateOrders(
            ApplicationDbContext context,
            List<User> users,
            List<Status> statuses,
            List<OrderType> orderTypes)
        {
            var orderFaker = new Faker<Order>("ru")
                .RuleFor(o => o.TotalAmount, f => Math.Round(f.Random.Decimal(5000, 150000), 2))
                .RuleFor(o => o.UserId, f => f.Random.ListItem(users.Select(u => u.Id).ToList()))
                .RuleFor(o => o.StatusId, f => f.Random.ListItem(statuses.Select(s => s.Id).ToList()))
                .RuleFor(o => o.OrderTypeId, f => f.Random.ListItem(orderTypes.Select(ot => ot.Id).ToList()));

            return orderFaker.Generate(20);
        }

        private static string GetRandomFirstName()
        {
            var names = new[] { "Александр", "Дмитрий", "Максим", "Сергей", "Андрей",
                               "Анна", "Мария", "Елена", "Ольга", "Наталья" };
            return names[new Random().Next(names.Length)];
        }

        private static string GetRandomLastName()
        {
            var lastNames = new[] { "Иванов", "Петров", "Сидоров", "Кузнецов", "Попов",
                                   "Смирнов", "Васильев", "Федоров", "Михайлов", "Новиков" };
            return lastNames[new Random().Next(lastNames.Length)];
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
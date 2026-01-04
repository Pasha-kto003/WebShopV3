using Microsoft.EntityFrameworkCore;
using WebShopV3.Services;

namespace WebShopV3.Models
{

    public static class DbInitializer
    {
        private readonly static IPasswordHasher _passwordHasher;
        public static void Initialize(ApplicationDbContext context)
        {
            // Проверяем, есть ли уже данные в базе
            if (context.Computers.Any() ||
                context.Components.Any() ||
                context.Characteristics.Any())
            {
                return; // База уже содержит данные
            }

            var executionStrategy = context.Database.CreateExecutionStrategy();

            executionStrategy.Execute(() =>
            {
                using var transaction = context.Database.BeginTransaction();

                try
                {
                    // 1. Заполняем типы пользователей
                    var userTypes = new List<UserType>
                    {
                        new UserType { Name = "Админ" },
                        new UserType { Name = "Менеджер" },
                        new UserType { Name = "Пользователь" }
                    };
                    context.UserTypes.AddRange(userTypes);

                    var users = new List<User>
                    {
                        new User
                        {
                            Username = "admin",
                            Email = "admin@example.com",
                            FirstName = "Алексей",
                            LastName = "Иванов",
                            Phone = "+79998887766",
                            PasswordHash = _passwordHasher.HashPassword("Admin123!"), // Пароль: Admin123!
                            UserTypeId = context.UserTypes.FirstOrDefault(ut => ut.Name == "Админ").Id,
                            CreatedAt = DateTime.Now
                        },
                        new User
                        {
                            Username = "manager",
                            Email = "manager@example.com",
                            FirstName = "Мария",
                            LastName = "Петрова",
                            Phone = "+79997776655",
                            PasswordHash = _passwordHasher.HashPassword("Manager456!"), // Пароль: Manager456!
                            UserTypeId = context.UserTypes.FirstOrDefault(ut => ut.Name == "Менеджер").Id,
                            CreatedAt = DateTime.Now
                        },
                        new User
                        {
                            Username = "user",
                            Email = "user@example.com",
                            FirstName = "Иван",
                            LastName = "Сидоров",
                            Phone = "+79996665544",
                            PasswordHash = _passwordHasher.HashPassword("User789!"), // Пароль: User789!
                            UserTypeId = context.UserTypes.FirstOrDefault(ut => ut.Name == "Пользователь").Id,
                            CreatedAt = DateTime.Now
                        }
                    };
                    context.Users.AddRange(users);

                    // 2. Заполняем статусы
                    var statuses = new List<Status>
                    {
                        new Status { Name = "Выполнен" },
                        new Status { Name = "В ожидании" },
                        new Status { Name = "Отмена" },
                        new Status { Name = "Проблема с наличием" }
                    };
                    context.Statuses.AddRange(statuses);

                    // 3. Заполняем типы заказов
                    var orderTypes = new List<OrderType>
                    {
                        new OrderType { Name = "Продажа" },
                        new OrderType { Name = "Приход" },
                        new OrderType { Name = "Отмена" }
                    };
                    context.OrderTypes.AddRange(orderTypes);

                    // 4. Заполняем характеристики
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
                        new Characteristic { Name = "Слоты расширения", Unit = "шт", Description = "Количество слотов расширения" },
                        new Characteristic { Name = "Отсеки для HDD", Unit = "шт", Description = "Количество отсеков для жестких дисков" },
                        new Characteristic { Name = "Отсеки для SSD", Unit = "шт", Description = "Количество отсеков для SSD" },
                        new Characteristic { Name = "Вентиляторы", Unit = "шт", Description = "Количество установленных вентиляторов" },
                        new Characteristic { Name = "Тип охлаждения", Unit = "", Description = "Тип охлаждения (Air, Liquid)" },
                        new Characteristic { Name = "Рассеиваемая мощность", Unit = "Вт", Description = "Максимальная рассеиваемая мощность" },
                        new Characteristic { Name = "Уровень шума", Unit = "дБ", Description = "Уровень шума при работе" },
                        new Characteristic { Name = "Совместимые сокеты", Unit = "", Description = "Поддерживаемые сокеты процессоров" },
                        new Characteristic { Name = "Гарантия", Unit = "мес", Description = "Срок гарантии производителя" },
                        new Characteristic { Name = "Габариты", Unit = "мм", Description = "Габаритные размеры компонента" },
                        new Characteristic { Name = "Вес", Unit = "г", Description = "Вес компонента" }
                    };
                    context.Characteristics.AddRange(characteristics);

                    // 5. Заполняем компоненты
                    var components = new List<Component>
                    {
                        // Жесткие диски
                        new Component
                        {
                            Name = "SeaGate",
                            Description = "Простое описание",
                            Price = 8000.00m,
                            Quantity = 15,
                            Type = "HDD",
                            Specifications = "Дополнительное описание",
                            Socket = "",
                            MemoryType = "",
                            FormFactor = "",
                            PowerConnector = "",
                            MaxMemory = null,
                            MemorySlots = null,
                            ImageUrl = "1.jpg"
                        },

                        // Материнские платы
                        new Component
                        {
                            Name = "ASUS ROG Strix B550-F Gaming",
                            Description = "Материнская плата AMD B550, Socket AM4, ATX",
                            Price = 18000.00m,
                            Quantity = 30,
                            Type = "MB",
                            Specifications = "AMD B550, 2x M.2, USB 3.2, 2.5G LAN",
                            Socket = "AM4",
                            MemoryType = "DDR4",
                            FormFactor = "ATX",
                            PowerConnector = "",
                            MaxMemory = 128,
                            MemorySlots = 4,
                            ImageUrl = "3.png"
                        },
                        new Component
                        {
                            Name = "Gigabyte B760 Gaming X",
                            Description = "Материнская плата Intel B760, Socket LGA1700, ATX",
                            Price = 16000.00m,
                            Quantity = 36,
                            Type = "MB",
                            Specifications = "Intel B760, PCIe 4.0, 2x M.2, 2.5G LAN",
                            Socket = "LGA1700",
                            MemoryType = "DDR5",
                            FormFactor = "ATX",
                            PowerConnector = "",
                            MaxMemory = 128,
                            MemorySlots = 4,
                            ImageUrl = "5985216.png"
                        },
                        new Component
                        {
                            Name = "AMD Ryzen 7 5800X",
                            Description = "Процессор AMD Ryzen 7, 8 ядер, 3.8 ГГц",
                            Price = 25000.00m,
                            Quantity = 29,
                            Type = "CPU",
                            Specifications = "8 cores/16 threads, 3.8-4.7 GHz, 36MB cache, 105W",
                            Socket = "AM4",
                            MemoryType = "",
                            FormFactor = "",
                            PowerConnector = "",
                            MaxMemory = null,
                            MemorySlots = null,
                            ImageUrl = "5985216.png"
                        },
                        new Component
                        {
                            Name = "Intel Core i7-13700K",
                            Description = "Процессор Intel Core i7, 16 ядер, 3.4 ГГц",
                            Price = 32000.00m,
                            Quantity = 31,
                            Type = "CPU",
                            Specifications = "16 cores/24 threads, 3.4-5.4 GHz, 30MB cache, 125W",
                            Socket = "LGA1700",
                            MemoryType = "",
                            FormFactor = "",
                            PowerConnector = "",
                            MaxMemory = null,
                            MemorySlots = null,
                            ImageUrl = "5985216.png"
                        },
                        // Оперативная память
                        new Component
                        {
                            Name = "Kingston Fury Beast 32GB DDR4 3200MHz",
                            Description = "Оперативная память 32GB DDR4, 2x16GB",
                            Price = 8000.00m,
                            Quantity = 41,
                            Type = "RAM",
                            Specifications = "32GB (2x16GB) DDR4 3200MHz, CL16, 1.35V",
                            Socket = "",
                            MemoryType = "DDR4",
                            FormFactor = "",
                            PowerConnector = "",
                            MaxMemory = null,
                            MemorySlots = null,
                            ImageUrl = "1039454.jpg"
                        },
                        // Видеокарты
                        new Component
                        {
                            Name = "NVIDIA GeForce RTX 4070",
                            Description = "Видеокарта NVIDIA GeForce RTX 4070, 12GB",
                            Price = 65000.00m,
                            Quantity = 39,
                            Type = "GPU",
                            Specifications = "RTX 4070, 12GB GDDR6X, DLSS 3, 3x DisplayPort, 1x HDMI",
                            Socket = "",
                            MemoryType = "",
                            FormFactor = "",
                            PowerConnector = "8-pin",
                            MaxMemory = null,
                            MemorySlots = null,
                            ImageUrl = "1024.png"
                        },
                        // SSD
                        new Component
                        {
                            Name = "Samsung 980 Pro 1TB",
                            Description = "SSD накопитель Samsung 980 Pro 1TB NVMe",
                            Price = 12000.00m,
                            Quantity = 45,
                            Type = "SSD",
                            Specifications = "1TB NVMe PCIe 4.0, 7000MB/s read, 5000MB/s write",
                            Socket = "",
                            MemoryType = "",
                            FormFactor = "",
                            PowerConnector = "",
                            MaxMemory = null,
                            MemorySlots = null,
                            ImageUrl = "5985216.png"
                        },
                        // Кулеры
                        new Component
                        {
                            Name = "Noctua NH-D15",
                            Description = "Башенный кулер Noctua NH-D15",
                            Price = 10000.00m,
                            Quantity = 38,
                            Type = "Cooler",
                            Specifications = "Dual tower, 2x 140mm fans, 165mm height, for Intel/AMD",
                            Socket = "",
                            MemoryType = "",
                            FormFactor = "",
                            PowerConnector = "",
                            MaxMemory = null,
                            MemorySlots = null,
                            ImageUrl = "5985216.png"
                        }
                    };
                    context.Components.AddRange(components);
                    context.SaveChanges();
                    var seaGate = context.Components.First(c => c.Name == "SeaGate");
                    var asusMotherboard = context.Components.First(c => c.Name == "ASUS ROG Strix B550-F Gaming");
                    var gigabyteMotherboard = context.Components.First(c => c.Name == "Gigabyte B760 Gaming X");
                    var amdCpu = context.Components.First(c => c.Name == "AMD Ryzen 7 5800X");
                    var intelCpu = context.Components.First(c => c.Name == "Intel Core i7-13700K");
                    var kingstonRam = context.Components.First(c => c.Name == "Kingston Fury Beast 32GB DDR4 3200MHz");
                    var nvidiaGpu = context.Components.First(c => c.Name == "NVIDIA GeForce RTX 4070");
                    var samsungSsd = context.Components.First(c => c.Name == "Samsung 980 Pro 1TB");
                    var noctuaCooler = context.Components.First(c => c.Name == "Noctua NH-D15");

                    // Получаем Id всех характеристик
                    var frequencyChar = context.Characteristics.First(c => c.Name == "Тактовая частота");
                    var coresChar = context.Characteristics.First(c => c.Name == "Количество ядер");
                    var threadsChar = context.Characteristics.First(c => c.Name == "Количество потоков");
                    var memoryChar = context.Characteristics.First(c => c.Name == "Объем памяти");
                    var memoryFreqChar = context.Characteristics.First(c => c.Name == "Частота памяти");
                    var memoryTypeChar = context.Characteristics.First(c => c.Name == "Тип памяти");
                    var timingsChar = context.Characteristics.First(c => c.Name == "Тайминги");
                    var storageCapacityChar = context.Characteristics.First(c => c.Name == "Емкость накопителя");
                    var readSpeedChar = context.Characteristics.First(c => c.Name == "Скорость чтения");
                    var writeSpeedChar = context.Characteristics.First(c => c.Name == "Скорость записи");
                    var storageTypeChar = context.Characteristics.First(c => c.Name == "Тип накопителя");
                    var vramChar = context.Characteristics.First(c => c.Name == "Объем видеопамяти");
                    var gpuFreqChar = context.Characteristics.First(c => c.Name == "Частота GPU");
                    var powerConnectorChar = context.Characteristics.First(c => c.Name == "Разъемы питания");
                    var psuRecommendChar = context.Characteristics.First(c => c.Name == "Рекомендуемый БП");
                    var chipsetChar = context.Characteristics.First(c => c.Name == "Чипсет");
                    var socketChar = context.Characteristics.First(c => c.Name == "Сокет");
                    var formFactorChar = context.Characteristics.First(c => c.Name == "Форм-фактор");
                    var memorySlotsChar = context.Characteristics.First(c => c.Name == "Количество слотов памяти");
                    var maxMemoryChar = context.Characteristics.First(c => c.Name == "Макс. объем памяти");
                    var expansionSlotsChar = context.Characteristics.First(c => c.Name == "Слоты расширения");
                    var sataPortsChar = context.Characteristics.First(c => c.Name == "Порты SATA");
                    var m2PortsChar = context.Characteristics.First(c => c.Name == "Порты M.2");
                    var powerChar = context.Characteristics.First(c => c.Name == "Мощность");
                    var certificationChar = context.Characteristics.First(c => c.Name == "Сертификат");
                    var pcieConnectorsChar = context.Characteristics.First(c => c.Name == "Разъемы PCI-E");
                    var sataConnectorsChar = context.Characteristics.First(c => c.Name == "Разъемы SATA");
                    var caseTypeChar = context.Characteristics.First(c => c.Name == "Тип корпуса");
                    var supportedBoardsChar = context.Characteristics.First(c => c.Name == "Поддерживаемые платы");
                    var expansionSlotsCaseChar = context.Characteristics.First(c => c.Name == "Слоты расширения");
                    var hddBaysChar = context.Characteristics.First(c => c.Name == "Отсеки для HDD");
                    var ssdBaysChar = context.Characteristics.First(c => c.Name == "Отсеки для SSD");
                    var fansChar = context.Characteristics.First(c => c.Name == "Вентиляторы");
                    var coolingTypeChar = context.Characteristics.First(c => c.Name == "Тип охлаждения");
                    var tdpChar = context.Characteristics.First(c => c.Name == "Рассеиваемая мощность");
                    var noiseChar = context.Characteristics.First(c => c.Name == "Уровень шума");
                    var compatibleSocketsChar = context.Characteristics.First(c => c.Name == "Совместимые сокеты");
                    var warrantyChar = context.Characteristics.First(c => c.Name == "Гарантия");
                    var dimensionsChar = context.Characteristics.First(c => c.Name == "Габариты");
                    var weightChar = context.Characteristics.First(c => c.Name == "Вес");

                    var componentCharacteristics = new List<ComponentCharacteristic>
                    {
                        // Характеристики для SeaGate HDD
                        new ComponentCharacteristic { ComponentId = seaGate.Id, CharacteristicId = storageCapacityChar.Id, Value = "1000" },
                        new ComponentCharacteristic { ComponentId = seaGate.Id, CharacteristicId = storageTypeChar.Id, Value = "HDD" },
                        new ComponentCharacteristic { ComponentId = seaGate.Id, CharacteristicId = warrantyChar.Id, Value = "24" },
            
                        // Характеристики для ASUS ROG Strix B550-F Gaming
                        new ComponentCharacteristic { ComponentId = asusMotherboard.Id, CharacteristicId = chipsetChar.Id, Value = "B550" },
                        new ComponentCharacteristic { ComponentId = asusMotherboard.Id, CharacteristicId = socketChar.Id, Value = "AM4" },
                        new ComponentCharacteristic { ComponentId = asusMotherboard.Id, CharacteristicId = formFactorChar.Id, Value = "ATX" },
                        new ComponentCharacteristic { ComponentId = asusMotherboard.Id, CharacteristicId = memorySlotsChar.Id, Value = "4" },
                        new ComponentCharacteristic { ComponentId = asusMotherboard.Id, CharacteristicId = maxMemoryChar.Id, Value = "128" },
                        new ComponentCharacteristic { ComponentId = asusMotherboard.Id, CharacteristicId = memoryTypeChar.Id, Value = "DDR4" },
                        new ComponentCharacteristic { ComponentId = asusMotherboard.Id, CharacteristicId = expansionSlotsChar.Id, Value = "3" },
                        new ComponentCharacteristic { ComponentId = asusMotherboard.Id, CharacteristicId = sataPortsChar.Id, Value = "6" },
                        new ComponentCharacteristic { ComponentId = asusMotherboard.Id, CharacteristicId = m2PortsChar.Id, Value = "2" },
                        new ComponentCharacteristic { ComponentId = asusMotherboard.Id, CharacteristicId = warrantyChar.Id, Value = "36" },
            
                        // Характеристики для Gigabyte B760 Gaming X
                        new ComponentCharacteristic { ComponentId = gigabyteMotherboard.Id, CharacteristicId = chipsetChar.Id, Value = "B760" },
                        new ComponentCharacteristic { ComponentId = gigabyteMotherboard.Id, CharacteristicId = socketChar.Id, Value = "LGA1700" },
                        new ComponentCharacteristic { ComponentId = gigabyteMotherboard.Id, CharacteristicId = formFactorChar.Id, Value = "ATX" },
                        new ComponentCharacteristic { ComponentId = gigabyteMotherboard.Id, CharacteristicId = memorySlotsChar.Id, Value = "4" },
                        new ComponentCharacteristic { ComponentId = gigabyteMotherboard.Id, CharacteristicId = maxMemoryChar.Id, Value = "128" },
                        new ComponentCharacteristic { ComponentId = gigabyteMotherboard.Id, CharacteristicId = memoryTypeChar.Id, Value = "DDR5" },
                        new ComponentCharacteristic { ComponentId = gigabyteMotherboard.Id, CharacteristicId = expansionSlotsChar.Id, Value = "3" },
                        new ComponentCharacteristic { ComponentId = gigabyteMotherboard.Id, CharacteristicId = sataPortsChar.Id, Value = "6" },
                        new ComponentCharacteristic { ComponentId = gigabyteMotherboard.Id, CharacteristicId = m2PortsChar.Id, Value = "2" },
                        new ComponentCharacteristic { ComponentId = gigabyteMotherboard.Id, CharacteristicId = warrantyChar.Id, Value = "36" },
            
                        // Характеристики для AMD Ryzen 7 5800X
                        new ComponentCharacteristic { ComponentId = amdCpu.Id, CharacteristicId = frequencyChar.Id, Value = "3.8" },
                        new ComponentCharacteristic { ComponentId = amdCpu.Id, CharacteristicId = coresChar.Id, Value = "8" },
                        new ComponentCharacteristic { ComponentId = amdCpu.Id, CharacteristicId = threadsChar.Id, Value = "16" },
                        new ComponentCharacteristic { ComponentId = amdCpu.Id, CharacteristicId = socketChar.Id, Value = "AM4" },
                        new ComponentCharacteristic { ComponentId = amdCpu.Id, CharacteristicId = tdpChar.Id, Value = "105" },
                        new ComponentCharacteristic { ComponentId = amdCpu.Id, CharacteristicId = warrantyChar.Id, Value = "36" },
            
                        // Характеристики для Intel Core i7-13700K
                        new ComponentCharacteristic { ComponentId = intelCpu.Id, CharacteristicId = frequencyChar.Id, Value = "3.4" },
                        new ComponentCharacteristic { ComponentId = intelCpu.Id, CharacteristicId = coresChar.Id, Value = "16" },
                        new ComponentCharacteristic { ComponentId = intelCpu.Id, CharacteristicId = threadsChar.Id, Value = "24" },
                        new ComponentCharacteristic { ComponentId = intelCpu.Id, CharacteristicId = socketChar.Id, Value = "LGA1700" },
                        new ComponentCharacteristic { ComponentId = intelCpu.Id, CharacteristicId = tdpChar.Id, Value = "125" },
                        new ComponentCharacteristic { ComponentId = intelCpu.Id, CharacteristicId = warrantyChar.Id, Value = "36" },
            
                        // Характеристики для Kingston Fury Beast 32GB DDR4 3200MHz
                        new ComponentCharacteristic { ComponentId = kingstonRam.Id, CharacteristicId = memoryChar.Id, Value = "32" },
                        new ComponentCharacteristic { ComponentId = kingstonRam.Id, CharacteristicId = memoryFreqChar.Id, Value = "3200" },
                        new ComponentCharacteristic { ComponentId = kingstonRam.Id, CharacteristicId = memoryTypeChar.Id, Value = "DDR4" },
                        new ComponentCharacteristic { ComponentId = kingstonRam.Id, CharacteristicId = timingsChar.Id, Value = "CL16" },
                        new ComponentCharacteristic { ComponentId = kingstonRam.Id, CharacteristicId = warrantyChar.Id, Value = "60" },
            
                        // Характеристики для NVIDIA GeForce RTX 4070
                        new ComponentCharacteristic { ComponentId = nvidiaGpu.Id, CharacteristicId = vramChar.Id, Value = "12" },
                        new ComponentCharacteristic { ComponentId = nvidiaGpu.Id, CharacteristicId = gpuFreqChar.Id, Value = "1920" },
                        new ComponentCharacteristic { ComponentId = nvidiaGpu.Id, CharacteristicId = powerConnectorChar.Id, Value = "8-pin" },
                        new ComponentCharacteristic { ComponentId = nvidiaGpu.Id, CharacteristicId = psuRecommendChar.Id, Value = "650" },
                        new ComponentCharacteristic { ComponentId = nvidiaGpu.Id, CharacteristicId = warrantyChar.Id, Value = "36" },
            
                        // Характеристики для Samsung 980 Pro 1TB
                        new ComponentCharacteristic { ComponentId = samsungSsd.Id, CharacteristicId = storageCapacityChar.Id, Value = "1000" },
                        new ComponentCharacteristic { ComponentId = samsungSsd.Id, CharacteristicId = storageTypeChar.Id, Value = "NVMe SSD" },
                        new ComponentCharacteristic { ComponentId = samsungSsd.Id, CharacteristicId = readSpeedChar.Id, Value = "7000" },
                        new ComponentCharacteristic { ComponentId = samsungSsd.Id, CharacteristicId = writeSpeedChar.Id, Value = "5000" },
                        new ComponentCharacteristic { ComponentId = samsungSsd.Id, CharacteristicId = warrantyChar.Id, Value = "60" },
            
                        // Характеристики для Noctua NH-D15
                        new ComponentCharacteristic { ComponentId = noctuaCooler.Id, CharacteristicId = coolingTypeChar.Id, Value = "Air" },
                        new ComponentCharacteristic { ComponentId = noctuaCooler.Id, CharacteristicId = tdpChar.Id, Value = "250" },
                        new ComponentCharacteristic { ComponentId = noctuaCooler.Id, CharacteristicId = noiseChar.Id, Value = "24.6" },
                        new ComponentCharacteristic { ComponentId = noctuaCooler.Id, CharacteristicId = compatibleSocketsChar.Id, Value = "AM4, LGA1700, LGA1200, AM5" },
                        new ComponentCharacteristic { ComponentId = noctuaCooler.Id, CharacteristicId = fansChar.Id, Value = "2" },
                        new ComponentCharacteristic { ComponentId = noctuaCooler.Id, CharacteristicId = dimensionsChar.Id, Value = "160x150x165" },
                        new ComponentCharacteristic { ComponentId = noctuaCooler.Id, CharacteristicId = weightChar.Id, Value = "1320" },
                        new ComponentCharacteristic { ComponentId = noctuaCooler.Id, CharacteristicId = warrantyChar.Id, Value = "72" }
                    };


                    context.ComponentCharacteristics.AddRange(componentCharacteristics);

                    // 7. Заполняем компьютеры
                    var computers = new List<Computer>
                    {
                        new Computer
                        {
                            Name = "ПК для (games)",
                            Description = "описание",
                            Price = 157300.00m,
                            Quantity = 0,
                            ImageUrl = "1.jpg"
                        },
                        new Computer
                        {
                            Name = "Compic",
                            Description = "вфывфыв",
                            Price = 157300.00m,
                            Quantity = 94,
                            ImageUrl = "2.jpg"
                        },
                        new Computer
                        {
                            Name = "навы",
                            Description = "Собранный ПК: ASUS ROG Strix B550-F Gaming, AMD Ryzen 7 5800X, Kingston Fury Beast 32GB DDR4 3200MHz, Seasonic Focus GX-750, NZXT H510 Flow, NVIDIA GeForce RTX 4070",
                            Price = 133000.00m,
                            Quantity = 1,
                            ImageUrl = "6.jpg"
                        }
                    };
                    context.Computers.AddRange(computers);

                    // Сохраняем компьютеры
                    context.SaveChanges();

                    var gamePc = context.Computers.First(c => c.Name == "ПК для (games)");
                    var compicPc = context.Computers.First(c => c.Name == "Compic");
                    var customPc = context.Computers.First(c => c.Name == "навы");

                    var computerComponents = new List<ComputerComponent>
                    {
                        new ComputerComponent { ComputerId = gamePc.Id, ComponentId = seaGate.Id },
                        new ComputerComponent { ComputerId = compicPc.Id, ComponentId = seaGate.Id },
                        new ComputerComponent { ComputerId = customPc.Id, ComponentId = asusMotherboard.Id },
                        new ComputerComponent { ComputerId = customPc.Id, ComponentId = amdCpu.Id },
                        new ComputerComponent { ComputerId = customPc.Id, ComponentId = kingstonRam.Id },
                        new ComputerComponent { ComputerId = customPc.Id, ComponentId = nvidiaGpu.Id },
                        new ComputerComponent { ComputerId = customPc.Id, ComponentId = samsungSsd.Id },
                        new ComputerComponent { ComputerId = customPc.Id, ComponentId = noctuaCooler.Id }
                    };
                    context.ComputerComponents.AddRange(computerComponents);

                    context.SaveChanges();
                    transaction.Commit();

                    Console.WriteLine("База данных успешно заполнена начальными данными.");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Console.WriteLine($"Ошибка при заполнении базы данных: {ex.Message}");
                    throw;
                }
            });

            try
            {
                // Начинаем транзакцию для атомарного заполнения данных
                using var transaction = context.Database.BeginTransaction();

                

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
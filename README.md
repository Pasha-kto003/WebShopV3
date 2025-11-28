# WebShopV3

# 💻 WebShopV3 — Интернет-магазин компьютеров и комплектующих

![.NET](https://img.shields.io/badge/.NET-6.0+-512BD4?logo=dotnet&logoColor=white)
![ASP.NET Core MVC](https://img.shields.io/badge/ASP.NET_Core_MVC-6.0+-5C2D91?logo=aspnet&logoColor=white)
![SQL Server](https://img.shields.io/badge/MS_SQL_Server-2019+-CC2927?logo=microsoft-sql-server&logoColor=white)
![C#](https://img.shields.io/badge/C%23-10+-239120?logo=csharp&logoColor=white)

Добро пожаловать в **WebShopV3** — современный интернет-магазин, разработанный на платформе **ASP.NET Core MVC**, специализирующийся на продаже компьютеров, ноутбуков и комплектующих. Проект создан с акцентом на удобство, масштабируемость и чистую архитектуру.

🔗 **Репозиторий на GitHub**: [https://github.com/Pasha-kto003/WebShopV3](https://github.com/Pasha-kto003/WebShopV3)

---

## 🧾 Описание проекта

**WebShopV3** — это полноценный веб-магазин, в котором пользователи могут:
- Просматривать каталог товаров (ПК, ноутбуки, процессоры, видеокарты и др.)
- Искать и фильтровать товары по категориям и характеристикам
- Добавлять товары в корзину и оформлять заказы
- Просматривать детальную информацию о каждом товаре

Проект использует **паттерн MVC**, реализован с соблюдением принципов чистого кода и разделения ответственности. База данных построена на **Microsoft SQL Server**, а бизнес-логика реализована на **C#**.

---

## 🛠️ Технологии и стек

- **Язык программирования**: C# 10+
- **Фреймворк**: ASP.NET Core 6.0+ (MVC)
- **База данных**: Microsoft SQL Server
- **Entity Framework Core**: для ORM и миграций
- **Frontend**: HTML5, CSS3, Tailwind, JavaScript (без фреймворков)
- **Аутентификация**: Identity + SQL
- **Инструменты**: Visual Studio 2022, SQL Server Management Studio

---

## 📦 Структура проекта (упрощённо)

### WebShopV3/
### ├── Controllers/ # Контроллеры MVC
### ├── Models/ # Модели данных (включая Entity Framework)
### ├── Views/ # Представления (Razor Pages)
### ├── Data/ # Контекст БД и миграции
### ├── wwwroot/ # Статические файлы (CSS, JS, изображения)


## 🚀 Как запустить локально

1. **Клонируйте репозиторий:**
   ```bash
   git clone https://github.com/Pasha-kto003/WebShopV3.git

2. **Откройте решение в Visual Studio 2022 (или новее).**
3. **Восстановите зависимости NuGet (обычно происходит автоматически).**
4. **Настройте строку подключения в appsettings.json:**
```JSON
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=WebShopV3Db;Trusted_Connection=true;"
}
```

6. **Примените миграции (если есть):**
   dotnet ef database update
7. **Запустите приложение:**
   - Через Visual Studio (F5)
   - Или через терминал:
   ```PM 
     dotnet run

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WebShopV3.Migrations
{
    /// <inheritdoc />
    public partial class CreateTablesStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Characteristics",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "ComponentCharacteristics",
                keyColumns: new[] { "CharacteristicId", "ComponentId" },
                keyValues: new object[] { 1, 1 });

            migrationBuilder.DeleteData(
                table: "ComponentCharacteristics",
                keyColumns: new[] { "CharacteristicId", "ComponentId" },
                keyValues: new object[] { 2, 1 });

            migrationBuilder.DeleteData(
                table: "ComponentCharacteristics",
                keyColumns: new[] { "CharacteristicId", "ComponentId" },
                keyValues: new object[] { 5, 2 });

            migrationBuilder.DeleteData(
                table: "ComponentCharacteristics",
                keyColumns: new[] { "CharacteristicId", "ComponentId" },
                keyValues: new object[] { 6, 2 });

            migrationBuilder.DeleteData(
                table: "ComponentCharacteristics",
                keyColumns: new[] { "CharacteristicId", "ComponentId" },
                keyValues: new object[] { 3, 3 });

            migrationBuilder.DeleteData(
                table: "ComponentCharacteristics",
                keyColumns: new[] { "CharacteristicId", "ComponentId" },
                keyValues: new object[] { 4, 3 });

            migrationBuilder.DeleteData(
                table: "ComponentCharacteristics",
                keyColumns: new[] { "CharacteristicId", "ComponentId" },
                keyValues: new object[] { 6, 3 });

            migrationBuilder.DeleteData(
                table: "ComponentCharacteristics",
                keyColumns: new[] { "CharacteristicId", "ComponentId" },
                keyValues: new object[] { 7, 4 });

            migrationBuilder.DeleteData(
                table: "ComponentCharacteristics",
                keyColumns: new[] { "CharacteristicId", "ComponentId" },
                keyValues: new object[] { 8, 4 });

            migrationBuilder.DeleteData(
                table: "ComponentCharacteristics",
                keyColumns: new[] { "CharacteristicId", "ComponentId" },
                keyValues: new object[] { 9, 5 });

            migrationBuilder.DeleteData(
                table: "OrderTypes",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "OrderTypes",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Statuses",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Statuses",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Statuses",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Characteristics",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Characteristics",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Characteristics",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Characteristics",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Characteristics",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Characteristics",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Characteristics",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Characteristics",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Characteristics",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Components",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Components",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Components",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Components",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Components",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "UserTypes",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "UserTypes",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "UserTypes",
                keyColumn: "Id",
                keyValue: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Characteristics",
                columns: new[] { "Id", "CreatedAt", "Description", "Name", "Unit" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 11, 10, 18, 38, 32, 881, DateTimeKind.Local).AddTicks(1074), "Базовая частота процессора", "Тактовая частота", "ГГц" },
                    { 2, new DateTime(2025, 11, 10, 18, 38, 32, 882, DateTimeKind.Local).AddTicks(7615), "Количество физических ядер", "Количество ядер", "шт" },
                    { 3, new DateTime(2025, 11, 10, 18, 38, 32, 882, DateTimeKind.Local).AddTicks(7629), "Объем оперативной памяти", "Объем памяти", "ГБ" },
                    { 4, new DateTime(2025, 11, 10, 18, 38, 32, 882, DateTimeKind.Local).AddTicks(7631), "Частота оперативной памяти", "Частота памяти", "МГц" },
                    { 5, new DateTime(2025, 11, 10, 18, 38, 32, 882, DateTimeKind.Local).AddTicks(7633), "Объем памяти видеокарты", "Объем видеопамяти", "ГБ" },
                    { 6, new DateTime(2025, 11, 10, 18, 38, 32, 882, DateTimeKind.Local).AddTicks(7634), "Тип памяти (DDR4, GDDR6, etc.)", "Тип памяти", "" },
                    { 7, new DateTime(2025, 11, 10, 18, 38, 32, 882, DateTimeKind.Local).AddTicks(7635), "Объем storage накопителя", "Емкость накопителя", "ГБ" },
                    { 8, new DateTime(2025, 11, 10, 18, 38, 32, 882, DateTimeKind.Local).AddTicks(7637), "Скорость чтения накопителя", "Скорость чтения", "МБ/с" },
                    { 9, new DateTime(2025, 11, 10, 18, 38, 32, 882, DateTimeKind.Local).AddTicks(7638), "Мощность блока питания", "Мощность", "Вт" },
                    { 10, new DateTime(2025, 11, 10, 18, 38, 32, 882, DateTimeKind.Local).AddTicks(7640), "Доступные разъемы и порты", "Разъемы", "" }
                });

            migrationBuilder.InsertData(
                table: "Components",
                columns: new[] { "Id", "Description", "Name", "Price", "Quantity", "Specifications", "Type" },
                values: new object[,]
                {
                    { 1, "Процессор Intel Core i7 12-го поколения", "Intel Core i7-12700K", 35000m, 10, "12 ядер, 20 потоков, 3.6 ГГц", "CPU" },
                    { 2, "Видеокарта NVIDIA GeForce RTX 4070", "NVIDIA RTX 4070", 65000m, 5, "12GB GDDR6X, 5888 ядер", "GPU" },
                    { 3, "Оперативная память Kingston Fury Beast", "Kingston Fury 32GB DDR5", 12000m, 15, "DDR5 5200MHz, 32GB (2x16GB)", "RAM" },
                    { 4, "SSD накопитель Samsung 980 Pro", "Samsung 980 Pro 1TB", 15000m, 8, "NVMe M.2, 7000MB/s read", "SSD" },
                    { 5, "Блок питания Seasonic 750W", "Seasonic Focus GX-750", 8000m, 12, "750W, 80+ Gold, fully modular", "PSU" }
                });

            migrationBuilder.InsertData(
                table: "OrderTypes",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Продажа" },
                    { 2, "Привоз" }
                });

            migrationBuilder.InsertData(
                table: "Statuses",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Выполнен" },
                    { 2, "В ожидании" },
                    { 3, "Отменен" }
                });

            migrationBuilder.InsertData(
                table: "UserTypes",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Админ" },
                    { 2, "Пользователь" },
                    { 3, "Менеджер" }
                });

            migrationBuilder.InsertData(
                table: "ComponentCharacteristics",
                columns: new[] { "CharacteristicId", "ComponentId", "Value" },
                values: new object[,]
                {
                    { 1, 1, "3.6" },
                    { 2, 1, "12" },
                    { 5, 2, "12" },
                    { 6, 2, "GDDR6X" },
                    { 3, 3, "32" },
                    { 4, 3, "5200" },
                    { 6, 3, "DDR5" },
                    { 7, 4, "1000" },
                    { 8, 4, "7000" },
                    { 9, 5, "750" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Email", "FirstName", "LastName", "PasswordHash", "Phone", "UserTypeId", "Username" },
                values: new object[,]
                {
                    { 1, "admin@example.com", "Admin", "User", "hashed_admin_123", "1234567890", 1, "admin" },
                    { 2, "manager@example.com", "Manager", "User", "hashed_manager_123", "1234567891", 3, "manager" },
                    { 3, "user@example.com", "Regular", "User", "hashed_user_123", "1234567892", 2, "user" }
                });
        }
    }
}

# Используем официальный образ .NET 8 SDK для сборки
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Копируем файлы решения и проекта
COPY ["WebShopV3.sln", "."]
COPY ["WebShopV3.Json/WebShopV3.Json.csproj", "WebShopV3.Json/"]

# Восстанавливаем зависимости
RUN dotnet restore "WebShopV3.Json/WebShopV3.Json.csproj"

# Копируем остальные файлы
COPY . .

# Собираем проект
WORKDIR "/src/WebShopV3.Json"
RUN dotnet publish "WebShopV3.Json.csproj" -c Release -o /app/publish

# Финальный образ — только runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Копируем опубликованные файлы
COPY --from=build /app/publish .

# Устанавливаем переменную окружения для использования Docker-конфигурации
ENV ASPNETCORE_ENVIRONMENT=Docker

# Указываем порт, который будет слушать приложение
EXPOSE 5002

# Запускаем приложение
ENTRYPOINT ["dotnet", "WebShopV3.Json.dll"]
# Используем официальный образ .NET SDK для сборки
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Копируем файлы проекта
COPY ["WebShopV3/WebShopV3.csproj", "WebShopV3/"]
RUN dotnet restore "WebShopV3/WebShopV3.csproj"

# Копируем все остальные файлы
COPY . .

# Собираем проект
WORKDIR "/src/WebShopV3"
RUN dotnet build "WebShopV3.csproj" -c Release -o /app/build

# Публикуем проект
FROM build AS publish
RUN dotnet publish "WebShopV3.csproj" -c Release -o /app/publish

# Используем образ только с runtime для запуска
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Устанавливаем необходимые инструменты
RUN apt-get update && \
    apt-get install -y curl netcat && \
    rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .

# Устанавливаем переменные окружения по умолчанию
ENV ASPNETCORE_ENVIRONMENT=Development
ENV ASPNETCORE_URLS=http://+:80

# Открываем порты
EXPOSE 80

# Создаем скрипт для ожидания SQL Server
RUN echo '#!/bin/bash\n\
set -e\n\
echo "Starting WebShopV3..."\n\
\n\
# Ожидание SQL Server\n\
echo "Waiting for SQL Server..."\n\
for i in {1..60}; do\n\
  echo "Attempt $i/60 to connect to SQL Server..."\n\
  if nc -z sqlserver 1433 2>/dev/null; then\n\
    echo "SQL Server is up!"\n\
    # Даем SQL Server время на полный запуск\n\
    sleep 10\n\
    break\n\
  fi\n\
  sleep 2\n\
done\n\
\n\
# Проверяем, что SQL Server действительно готов\n\
echo "Checking SQL Server connection..."\n\
for i in {1..30}; do\n\
  echo "Connection test attempt $i/30..."\n\
  if curl -s http://localhost:80/health > /dev/null 2>&1; then\n\
    echo "Application is already running"\n\
    exit 0\n\
  fi\n\
  sleep 2\n\
done\n\
\n\
echo "Starting application..."\n\
dotnet WebShopV3.dll\n\
' > /app/entrypoint.sh && chmod +x /app/entrypoint.sh

# Используем скрипт как entrypoint
ENTRYPOINT ["/bin/bash", "/app/entrypoint.sh"]
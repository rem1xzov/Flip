FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Копируем файлы проектов
COPY FlipChatAnon/FlipChatAnon.csproj ./FlipChatAnon/
COPY FlipChatStore/FlipChatStore.csproj ./FlipChatStore/

# Восстанавливаем зависимости основного проекта
RUN dotnet restore FlipChatAnon/FlipChatAnon.csproj

# Копируем весь код
COPY . .

# Собираем и публикуем
WORKDIR /src/FlipChatAnon
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Копируем опубликованное приложение
COPY --from=build /app/publish .

# Создаем папку для фронтенда и копируем HTML файл
RUN mkdir -p /app/wwwroot
COPY android.html /app/wwwroot/index.html

ENV ASPNETCORE_URLS="http://+:8080"
EXPOSE 8080
ENTRYPOINT ["dotnet", "FlipChatAnon.dll"]

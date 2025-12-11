FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Копируем ТОЛЬКО существующие проекты
COPY FlipChatAnon/FlipChatAnon.csproj ./FlipChatAnon/
COPY FlipChatStore/FlipChatStore.csproj ./FlipChatStore/

# Восстанавливаем зависимости основного проекта
RUN dotnet restore FlipChatAnon/FlipChatAnon.csproj

# Копируем весь код (оставшиеся файлы папок FlipChatAnon и FlipChatStore)
COPY . .

# Собираем и публикуем
WORKDIR /src/FlipChatAnon
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS="http://+:8080"
EXPOSE 8080
ENTRYPOINT ["dotnet", "FlipChatAnon.dll"]

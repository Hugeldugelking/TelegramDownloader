FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["TelegramDownloader/TelegramDownloader.csproj", "TelegramDownloader/"]
RUN dotnet restore "TelegramDownloader/TelegramDownloader.csproj"
COPY . .
WORKDIR "/src/TelegramDownloader"
RUN dotnet build "TelegramDownloader.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TelegramDownloader.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TelegramDownloader.dll"]

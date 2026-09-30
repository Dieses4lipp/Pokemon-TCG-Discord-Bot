FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY DiscordBot.csproj ./
RUN dotnet restore DiscordBot.csproj

COPY . .
RUN dotnet publish DiscordBot.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app

COPY --from=build /out ./
COPY Assets ./Assets

USER $APP_UID
ENTRYPOINT ["dotnet", "DiscordBot.dll"]

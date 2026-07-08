FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY DungeonCrawler.API/DungeonCrawler.API.csproj DungeonCrawler.API/
RUN dotnet restore DungeonCrawler.API/DungeonCrawler.API.csproj

COPY DungeonCrawler.API/. DungeonCrawler.API/
WORKDIR /src/DungeonCrawler.API
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "DungeonCrawler.API.dll"]
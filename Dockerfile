# Build stage — full SDK needed to compile and test
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY bazinga-game/bazinga-game.csproj bazinga-game/
COPY BazingaGame.Tests/BazingaGame.Tests.csproj BazingaGame.Tests/
RUN dotnet restore bazinga-game/bazinga-game.csproj
RUN dotnet restore BazingaGame.Tests/BazingaGame.Tests.csproj

COPY bazinga-game/ bazinga-game/
COPY BazingaGame.Tests/ BazingaGame.Tests/

# Run tests before publishing — a failing test stops the build
RUN dotnet test BazingaGame.Tests/BazingaGame.Tests.csproj --no-restore -c Release

RUN dotnet publish bazinga-game/bazinga-game.csproj -c Release -o /app/publish

# Runtime stage — minimal image, no SDK
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "bazinga-game.dll"]

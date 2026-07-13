FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json .
COPY src/StrikeShield.Domain/StrikeShield.Domain.csproj src/StrikeShield.Domain/
COPY src/StrikeShield.Application/StrikeShield.Application.csproj src/StrikeShield.Application/
COPY src/StrikeShield.Infrastructure/StrikeShield.Infrastructure.csproj src/StrikeShield.Infrastructure/
COPY src/StrikeShield.Api/StrikeShield.Api.csproj src/StrikeShield.Api/
RUN dotnet restore src/StrikeShield.Api/StrikeShield.Api.csproj

COPY src/ src/
RUN dotnet publish src/StrikeShield.Api/StrikeShield.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "StrikeShield.Api.dll"]

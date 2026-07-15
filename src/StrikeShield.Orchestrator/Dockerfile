FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json .
COPY src/StrikeShield.Domain/StrikeShield.Domain.csproj src/StrikeShield.Domain/
COPY src/StrikeShield.Application/StrikeShield.Application.csproj src/StrikeShield.Application/
COPY src/StrikeShield.Infrastructure/StrikeShield.Infrastructure.csproj src/StrikeShield.Infrastructure/
COPY src/StrikeShield.Orchestrator/StrikeShield.Orchestrator.csproj src/StrikeShield.Orchestrator/
RUN dotnet restore src/StrikeShield.Orchestrator/StrikeShield.Orchestrator.csproj

COPY src/ src/
RUN dotnet publish src/StrikeShield.Orchestrator/StrikeShield.Orchestrator.csproj -c Release -o /app --no-restore

# aspnet (not the leaner runtime-only image) so the ASP.NET Core shared
# framework used by StrikeShield.Infrastructure's health-check types is
# guaranteed present, regardless of FrameworkReference propagation nuances
# through the ProjectReference chain.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet", "StrikeShield.Orchestrator.dll"]

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

# Playwright's own .NET image, not the plain aspnet runtime image: it's
# built FROM the matching .NET runtime with Chromium (and its OS-level
# deps) pre-installed at the same Microsoft.Playwright NuGet version
# referenced in StrikeShield.Infrastructure.csproj — the Reporting Agent's
# Markdown -> HTML -> PDF pipeline (docs/PHASED_PLAN.md Phase 7) needs a
# real browser to print to PDF, and version drift between the driver
# (NuGet package) and the installed browser is a common source of
# Playwright failures, so these two versions must be bumped together.
FROM mcr.microsoft.com/playwright/dotnet:v1.47.0-jammy AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "StrikeShield.Api.dll"]

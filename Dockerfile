FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Directory.Build.props enables ImplicitUsings/Nullable for every project; without it the
# Domain/Application/Infrastructure projects do not compile.
COPY MechanicsSoftware.BillingService.sln Directory.Build.props ./
COPY src/MechanicsSoftware.BillingService.Domain/MechanicsSoftware.BillingService.Domain.csproj src/MechanicsSoftware.BillingService.Domain/
COPY src/MechanicsSoftware.BillingService.Application/MechanicsSoftware.BillingService.Application.csproj src/MechanicsSoftware.BillingService.Application/
COPY src/MechanicsSoftware.BillingService.Infrastructure/MechanicsSoftware.BillingService.Infrastructure.csproj src/MechanicsSoftware.BillingService.Infrastructure/
COPY src/MechanicsSoftware.BillingService.Api/MechanicsSoftware.BillingService.Api.csproj src/MechanicsSoftware.BillingService.Api/

RUN dotnet restore src/MechanicsSoftware.BillingService.Api/MechanicsSoftware.BillingService.Api.csproj

COPY src/ src/

RUN dotnet publish src/MechanicsSoftware.BillingService.Api/MechanicsSoftware.BillingService.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MechanicsSoftware.BillingService.Api.dll"]

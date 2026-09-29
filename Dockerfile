FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY MechanicsSoftware.BillingService.sln .
COPY src/MechanicsSoftware.BillingService.Api/MechanicsSoftware.BillingService.Api.csproj src/MechanicsSoftware.BillingService.Api/

RUN dotnet restore src/MechanicsSoftware.BillingService.Api/MechanicsSoftware.BillingService.Api.csproj

COPY src/ src/

RUN dotnet publish src/MechanicsSoftware.BillingService.Api/MechanicsSoftware.BillingService.Api.csproj \
    -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MechanicsSoftware.BillingService.Api.dll"]

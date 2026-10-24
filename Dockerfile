# build stage: full SDK, restores and publishes the API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# copy project files first so the restore layer is cached until dependencies change
COPY src/TariffCalculator.Api/TariffCalculator.Api.csproj src/TariffCalculator.Api/
COPY src/TariffCalculator.Core/TariffCalculator.Core.csproj src/TariffCalculator.Core/
RUN dotnet restore src/TariffCalculator.Api/TariffCalculator.Api.csproj

COPY src/ src/
RUN dotnet publish src/TariffCalculator.Api/TariffCalculator.Api.csproj -c Release -o /app --no-restore

# runtime stage: only the ASP.NET runtime, much smaller image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "TariffCalculator.Api.dll"]

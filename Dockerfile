FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/StockNet.Api/StockNet.Api.csproj src/StockNet.Api/
RUN dotnet restore src/StockNet.Api/StockNet.Api.csproj

COPY src/StockNet.Api/ src/StockNet.Api/
WORKDIR /src/src/StockNet.Api
RUN dotnet publish StockNet.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "StockNet.Api.dll"]

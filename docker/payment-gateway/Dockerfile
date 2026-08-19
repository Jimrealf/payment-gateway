FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src
COPY . .
RUN dotnet restore FicMart.PaymentGateway.slnx
RUN dotnet publish src/FicMart.PaymentGateway.Api/FicMart.PaymentGateway.Api.csproj \
    --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "FicMart.PaymentGateway.Api.dll"]

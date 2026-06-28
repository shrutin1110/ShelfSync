FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ShelfSync.Warehouse/ src/ShelfSync.Warehouse/
COPY src/ShelfSync.Shared/ src/ShelfSync.Shared/
RUN dotnet restore src/ShelfSync.Warehouse/ShelfSync.Warehouse.csproj
RUN dotnet publish src/ShelfSync.Warehouse/ShelfSync.Warehouse.csproj -c Release -o /app/publish

FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y \
    libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
EXPOSE 5003
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5003
ENTRYPOINT ["dotnet", "ShelfSync.Warehouse.dll"]

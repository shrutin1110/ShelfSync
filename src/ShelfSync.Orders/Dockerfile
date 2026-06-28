FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ShelfSync.Orders/ src/ShelfSync.Orders/
COPY src/ShelfSync.Shared/ src/ShelfSync.Shared/
COPY src/ShelfSync.Warehouse/ src/ShelfSync.Warehouse/
RUN dotnet restore src/ShelfSync.Orders/ShelfSync.Orders.csproj
RUN dotnet publish src/ShelfSync.Orders/ShelfSync.Orders.csproj \
    -c Release \
    -o /app/publish \
    /p:ErrorOnDuplicatePublishOutputFiles=false

FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5002
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5002
ENTRYPOINT ["dotnet", "ShelfSync.Orders.dll"]

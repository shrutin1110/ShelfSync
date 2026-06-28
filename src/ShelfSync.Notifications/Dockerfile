FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ShelfSync.Notifications/ src/ShelfSync.Notifications/
COPY src/ShelfSync.Shared/ src/ShelfSync.Shared/
RUN dotnet restore src/ShelfSync.Notifications/ShelfSync.Notifications.csproj
RUN dotnet publish src/ShelfSync.Notifications/ShelfSync.Notifications.csproj -c Release -o /app/publish
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5004
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5004
ENTRYPOINT ["dotnet", "ShelfSync.Notifications.dll"]

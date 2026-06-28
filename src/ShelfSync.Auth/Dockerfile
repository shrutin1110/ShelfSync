FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ShelfSync.Auth/ src/ShelfSync.Auth/
COPY src/ShelfSync.Shared/ src/ShelfSync.Shared/
RUN dotnet restore src/ShelfSync.Auth/ShelfSync.Auth.csproj
RUN dotnet publish src/ShelfSync.Auth/ShelfSync.Auth.csproj -c Release -o /app/publish
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5001
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5001
ENTRYPOINT ["dotnet", "ShelfSync.Auth.dll"]

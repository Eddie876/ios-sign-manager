FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore ios-sign-manager.slnx
RUN dotnet publish src/SignManager.Web/SignManager.Web.csproj -c Release -o /app/web
RUN dotnet publish src/SignManager.Worker/SignManager.Worker.csproj -c Release -o /app/worker

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Placeholder. Milestone 3 will copy pinned zsign binary into this image.
COPY --from=build /app/web ./web
COPY --from=build /app/worker ./worker

ENTRYPOINT ["dotnet", "web/SignManager.Web.dll"]

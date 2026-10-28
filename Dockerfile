# Production image: builds the React app and the API, then serves both from one ASP.NET Core container.
# Build:  docker build -t servicedesk .
# Run:    docker compose up   (see docker-compose.yml)

# ---- 1. Build the React front end ----
FROM node:22-alpine AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

# ---- 2. Build and publish the API ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS api
WORKDIR /src
# Restore first (only the project file) so Docker can cache NuGet packages between builds.
COPY src/ServiceDesk.Api/ServiceDesk.Api.csproj src/ServiceDesk.Api/
RUN dotnet restore src/ServiceDesk.Api/ServiceDesk.Api.csproj
COPY src/ src/
RUN dotnet publish src/ServiceDesk.Api/ServiceDesk.Api.csproj -c Release -o /app --no-restore

# ---- 3. Small runtime image ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /web/dist ./wwwroot
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# Run as the image's built-in non-root user.
USER $APP_UID
ENTRYPOINT ["dotnet", "ServiceDesk.Api.dll"]

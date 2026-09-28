# ========================================================
# Stage 1: Build & Publish
# ========================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0.401-alpine3.20 AS builder

WORKDIR /src

# Copy project file first for optimized Docker layer caching
COPY TicketBooking.Api/TicketBooking.Api.csproj TicketBooking.Api/
RUN dotnet restore TicketBooking.Api/TicketBooking.Api.csproj

# Copy remaining source code
COPY TicketBooking.Api/ TicketBooking.Api/

# Build and publish release binaries
WORKDIR /src/TicketBooking.Api
RUN dotnet publish TicketBooking.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ========================================================
# Stage 2: Runtime Image
# ========================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0.8-alpine3.20 AS runner

WORKDIR /app

# Configure ASP.NET Core URL to port 5000
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0

# Copy published files from build stage
COPY --from=builder /app/publish .

# Run under unprivileged non-root user (id 1654:app in .NET 8 alpine)
USER app

EXPOSE 5000

# Container healthcheck using wget (preinstalled in alpine)
HEALTHCHECK --interval=15s --timeout=5s --start-period=10s --retries=3 \
  CMD wget --quiet --tries=1 --spider http://127.0.0.1:5000/health/live || exit 1

ENTRYPOINT ["dotnet", "TicketBooking.Api.dll"]

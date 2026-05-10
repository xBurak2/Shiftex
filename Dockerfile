# ════════════════════════════════════════════════════════════
# Shiftex — Multi-stage Dockerfile
# Stage 1: Restore + Build  → Stage 2: Publish  → Stage 3: Runtime
# Boyut hedef: < 250 MB (aspnet:8.0-alpine base)
# ════════════════════════════════════════════════════════════

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Önce csproj kopyala — restore katmanını cache'le
COPY ShiftTrackingApp/ShiftTrackingApp.csproj ShiftTrackingApp/
RUN dotnet restore ShiftTrackingApp/ShiftTrackingApp.csproj

# Kalan kaynağı kopyala ve publish et
COPY ShiftTrackingApp/ ShiftTrackingApp/
WORKDIR /src/ShiftTrackingApp
RUN dotnet publish -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ─── Runtime image ───
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Non-root user — güvenlik için
RUN addgroup -S shiftex && adduser -S shiftex -G shiftex
USER shiftex

COPY --from=build --chown=shiftex:shiftex /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_NOLOGO=true

EXPOSE 8080

# Health check — container orchestration için
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "ShiftTrackingApp.dll"]

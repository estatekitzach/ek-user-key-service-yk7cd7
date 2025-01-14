# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Set build arguments and environment variables
ARG BUILD_CONFIGURATION=Release
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# Create non-root user and group
RUN groupadd -g 1000 app && \
    useradd -u 1000 -g app -s /bin/bash -m app

# Copy project files first to optimize layer caching
COPY ["src/EstateKit.Api/EstateKit.Api.csproj", "src/EstateKit.Api/"]

# Restore NuGet packages with retry logic
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "src/EstateKit.Api/EstateKit.Api.csproj" \
    --runtime linux-x64 \
    || (sleep 5 && dotnet restore "src/EstateKit.Api/EstateKit.Api.csproj" --runtime linux-x64) \
    || (sleep 10 && dotnet restore "src/EstateKit.Api/EstateKit.Api.csproj" --runtime linux-x64)

# Copy remaining source code
COPY . .

# Build application with optimization flags
RUN dotnet build "src/EstateKit.Api/EstateKit.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build \
    --no-restore \
    /p:UseAppHost=false \
    /p:DebugType=None \
    /p:DebugSymbols=false

# Publish application with trimming and self-contained deployment
RUN dotnet publish "src/EstateKit.Api/EstateKit.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    --runtime linux-x64 \
    --self-contained true \
    /p:PublishTrimmed=true \
    /p:PublishSingleFile=true \
    /p:DebugType=None \
    /p:DebugSymbols=false

# Development stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS development
WORKDIR /src

ENV ASPNETCORE_ENVIRONMENT=Development \
    DOTNET_USE_POLLING_FILE_WATCHER=1 \
    ASPNETCORE_URLS=http://+:80

# Copy solution files for development
COPY ["src/EstateKit.Api/EstateKit.Api.csproj", "src/EstateKit.Api/"]
RUN dotnet restore "src/EstateKit.Api/EstateKit.Api.csproj"

# Configure development volumes
VOLUME ["/src", "/root/.nuget/packages"]

# Enable development tools and debugging
EXPOSE 80
ENTRYPOINT ["dotnet", "watch", "run", "--project", "src/EstateKit.Api/EstateKit.Api.csproj"]

# Production stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Set production environment variables
ENV ASPNETCORE_URLS=http://+:80 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0 \
    COMPlus_EnableDiagnostics=0

# Create non-root user and set permissions
RUN groupadd -g 1000 app && \
    useradd -u 1000 -g app -s /bin/bash -m app && \
    apt-get update && \
    apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/* && \
    mkdir -p /app && \
    chown -R app:app /app

# Copy published artifacts from build stage
COPY --from=build --chown=app:app /app/publish .

# Set restrictive file permissions
RUN chmod -R 755 /app

# Configure security options
USER app:app
ENV COMPlus_EnableDiagnostics=0 \
    DOTNET_EnableDiagnostics=0

# Set container resource limits
ENV DOTNET_GCHeapHardLimit=800000000 \
    DOTNET_GCHighMemPercent=80 \
    DOTNET_ThreadPool_MinThreads=4 \
    DOTNET_ThreadPool_MaxThreads=25

# Configure health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:80/health || exit 1

# Expose API port
EXPOSE 80

# Set production entry point
ENTRYPOINT ["dotnet", "EstateKit.Api.dll"]
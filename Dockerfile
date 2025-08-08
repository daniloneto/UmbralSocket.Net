# Multi-stage build for UmbralSocket.Net samples
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Install necessary build tools and runtime dependencies FIRST (for better caching)
RUN apt-get update && apt-get install -y \
    clang \
    libc6-dev \
    && rm -rf /var/lib/apt/lists/* \
    && apt-get clean

# Copy only project files first (for better dependency caching)
COPY Directory.Build.props ./
COPY nuget.config ./
COPY *.sln ./

# Copy project files maintaining directory structure
COPY src/UmbralSocket.Net/*.csproj ./src/UmbralSocket.Net/
COPY samples/UmbralSocket.Net.Sample/*.csproj ./samples/UmbralSocket.Net.Sample/
COPY samples/PingPongZeroCopy/*.csproj ./samples/PingPongZeroCopy/
COPY samples/SecureServer/*.csproj ./samples/SecureServer/
COPY test/UmbralSocket.Net.Tests/*.csproj ./test/UmbralSocket.Net.Tests/

# Restore dependencies (this layer will be cached if project files don't change)
RUN dotnet restore -r linux-x64

# Now copy the actual source code
COPY . .

# Build all samples with Linux optimizations
RUN dotnet publish samples/UmbralSocket.Net.Sample/UmbralSocket.Net.Sample.csproj \
    -c Release -r linux-x64 \
    /p:PublishAot=true \
    --self-contained true \
    -o /app/publish/sample

RUN dotnet publish samples/PingPongZeroCopy/PingPongZeroCopy.csproj \
    -c Release -r linux-x64 \
    /p:PublishAot=true \
    --self-contained true \
    -o /app/publish/pingpong

RUN dotnet publish samples/SecureServer/SecureServer.csproj \
    -c Release -r linux-x64 \
    /p:PublishAot=true \
    --self-contained true \
    -o /app/publish/secure

# Runtime stage - use minimal runtime deps image
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0
WORKDIR /app

# Create dedicated directories for Unix Domain Sockets with proper permissions
RUN mkdir -p /data/sockets /data/secure \
    && chmod 755 /data/sockets \
    && chmod 755 /data/secure

# Copy all published samples
COPY --from=build /app/publish ./

# Make executables runnable
RUN chmod +x sample/UmbralSocket.Net.Sample \
    && chmod +x pingpong/PingPongZeroCopy \
    && chmod +x secure/SecureServer

# Copy startup script
COPY run.sh /app/run.sh
RUN chmod +x /app/run.sh

# Variables de ambiente serão definidas via docker-compose
# ENV removidas para evitar conflitos

ENTRYPOINT ["/app/run.sh"]
CMD ["sample"]

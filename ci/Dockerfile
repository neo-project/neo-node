FROM mcr.microsoft.com/dotnet/core/sdk:3.0

# Install dependencies:
RUN apt-get update && apt-get install -y \
    libleveldb-dev \
    sqlite3 \
    libsqlite3-dev \
    libunwind8-dev \
    wget \
    expect \
    screen \
    zip

# APT cleanup to reduce image size
RUN rm -rf /var/lib/apt/lists/*

WORKDIR /opt

# Get code to test
ADD neo-cli /opt/neo-cli-github
ADD ci /opt/ci
COPY NuGet.Config /opt

WORKDIR /opt/neo-cli-github

# Build the project
RUN dotnet restore
RUN dotnet publish -c Release
RUN mv bin/Release/netcoreapp3.0/publish /opt/neo-cli

WORKDIR /opt

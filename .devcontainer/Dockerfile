FROM mcr.microsoft.com/dotnet/sdk:10.0.101-noble

# Install required dependencies for LevelDB and SQLite
RUN apt-get update && \
    apt-get install -y \
    libleveldb-dev \
    sqlite3 \
    libsqlite3-dev \
    librocksdb-dev \
    libsnappy-dev \
    libunwind8-dev \
    && rm -rf /var/lib/apt/lists/*


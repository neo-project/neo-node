FROM mcr.microsoft.com/dotnet/core/sdk:2.2 AS Build

COPY neo-cli /neo-cli
COPY NuGet.Config /neo-cli

WORKDIR /neo-cli
RUN dotnet restore && dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/core/runtime:2.2 AS Final
RUN apt-get update && apt-get install -y \
  screen \
  libleveldb-dev \
  sqlite3
RUN rm -rf /var/lib/apt/lists/*

WORKDIR /neo-cli
COPY  --from=Build /app .

ENTRYPOINT ["screen","-DmS","node","dotnet","neo-cli.dll","-r"]
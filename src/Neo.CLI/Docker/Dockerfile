FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS Build
RUN apt-get update && apt-get install -y \
    net-tools \
    telnet \
    bash-completion \
    wget \
    curl \
    lrzsz \
    zip \
    unzip \
    sqlite3 \
    libsqlite3-dev \
    libunwind8-dev \
    screen \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /neo
COPY prepare-node.sh .
RUN chmod +x prepare-node.sh

ARG CLI_VERSION=v3.9.2
ARG PLUGIN_VERSION=
RUN if [ -z "$PLUGIN_VERSION" ]; then \
        ./prepare-node.sh $CLI_VERSION; \
    else \
        ./prepare-node.sh $CLI_VERSION $PLUGIN_VERSION; \
    fi

RUN sed -i 's/"BindAddress":[^,]*/"BindAddress": "0.0.0.0"/' neo-cli/Plugins/RpcServer/RpcServer.json
COPY start.sh .
RUN chmod -R +x ./neo-cli
ENTRYPOINT ["sh", "./start.sh"]
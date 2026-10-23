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
    libleveldb-dev \
    screen \
    dos2unix \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /neo
# CI may pre-stage release archives in this context; local builds download them in prepare-node.sh
COPY . .
RUN dos2unix prepare-node.sh start.sh && chmod +x prepare-node.sh start.sh

ARG CLI_VERSION=v3.10.1
ARG PLUGIN_VERSION=
ARG GITHUB_REPO=neo-project/neo-node
ENV NEO_CLI_DIR=neo-cli-${CLI_VERSION}
ENV GITHUB_REPO=${GITHUB_REPO}
RUN if [ -z "$PLUGIN_VERSION" ]; then \
        ./prepare-node.sh $CLI_VERSION; \
    else \
        ./prepare-node.sh $CLI_VERSION $PLUGIN_VERSION; \
    fi \
    && rm -f neo-cli.*.tar.gz ApplicationLogs.zip RpcServer.zip TokensTracker.zip \
    && chmod -R +x "${NEO_CLI_DIR}"

ENTRYPOINT ["sh", "./start.sh"]

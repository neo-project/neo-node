################################
### Runner image
################################
FROM ubuntu:18.04

ENV VERSION v2.10.2

RUN apt update && \
    apt install -y \
    libleveldb-dev \
    sqlite3 \
    libsqlite3-dev \
    libunwind8-dev \
    software-properties-common \
    unzip \
    awscli \
    wget && \
    wget -q https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    add-apt-repository universe && \
    apt update && \
    apt -y install aspnetcore-runtime-2.2 && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

RUN groupadd --gid 1000 neo && \
    useradd --uid 1000 --gid neo --shell /bin/bash --create-home neo

RUN wget -q https://github.com/neo-project/neo-cli/releases/download/$VERSION/neo-cli-linux-x64.zip && \
    unzip -o neo-cli-linux-x64.zip -d /home/neo && \
    rm -rf /neo-cli-linux-x64.zip && \
#####  The plugins can be added like this:
#    wget -q https://github.com/neo-project/neo-plugins/releases/download/$VERSION/ImportBlocks.zip && \
#    unzip -o ImportBlocks.zip -d /home/neo && \
#    rm -rf /ImportBlocks.zip && \
    chmod +x /home/neo/neo-cli/neo-cli && \
    sed -i "s/127.0.0.1/0.0.0.0/g" /home/neo/neo-cli/config.json && \
    chown -R neo:neo /home/neo

#####  Here you can use complicated bash script to start container's app with customized settings
#COPY start-script.sh /usr/local/bin/start-script.sh
#RUN chmod a+x /usr/local/bin/start-script.sh
#ENTRYPOINT ["/bin/bash", "/usr/local/bin/start-script.sh"]

VOLUME /home/neo/data/
WORKDIR /home/neo/data

#####  In kubernetes volumes are attached with predefined UID. In plain docker it's a bit harder to achieve, so this one is commented out
#USER neo

EXPOSE 10332
EXPOSE 10334

CMD ["/home/neo/neo-cli/neo-cli", "--rpc"]

#####  IMPORTANT NOTE   #####
# There is no problem in catching tty in kubernetes, but to run it in docker you need to:
# add -t to allow tty, ex: docker run -t neo
# add -d to have your tty back and run docker in background: docker run -dt neo
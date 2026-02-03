echo "Downloading neo node $1"
wget  https://github.com/neo-project/neo-node/releases/download/$1/neo-cli-linux-x64.zip
unzip neo-cli-linux-x64.zip


if [ -z "$2" ]; then
    echo "Downloading plugins $1"
    wget https://github.com/neo-project/neo-node/releases/download/$1/ApplicationLogs.zip
    wget https://github.com/neo-project/neo-node/releases/download/$1/RpcServer.zip
    wget https://github.com/neo-project/neo-node/releases/download/$1/TokensTracker.zip
else
    echo "Downloading plugins $2"
    wget https://github.com/neo-project/neo-node/releases/download/$2/ApplicationLogs.zip
    wget https://github.com/neo-project/neo-node/releases/download/$2/RpcServer.zip
    wget https://github.com/neo-project/neo-node/releases/download/$2/TokensTracker.zip
fi

unzip -n ApplicationLogs.zip -d ./neo-cli/
unzip -n RpcServer.zip -d ./neo-cli/
unzip -n TokensTracker.zip -d ./neo-cli/

echo "Node Ready!"
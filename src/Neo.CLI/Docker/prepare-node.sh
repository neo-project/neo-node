echo "Downloading neo node $1"
ARCHIVE="neo-cli.$1-linux-x64.tar.gz"
wget "https://github.com/neo-project/neo-node/releases/download/$1/$ARCHIVE"
tar -xzf "$ARCHIVE"

if [ -d neo-cli ]; then
    PLUGIN_DIR=./neo-cli
elif [ -f neo-cli ]; then
    PLUGIN_DIR=.
else
    echo "Error: unexpected neo-cli layout after unzip" >&2
    exit 1
fi

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

unzip -n ApplicationLogs.zip -d "$PLUGIN_DIR/"
unzip -n RpcServer.zip -d "$PLUGIN_DIR/"
unzip -n TokensTracker.zip -d "$PLUGIN_DIR/"

sed -i 's/"BindAddress":[^,]*/"BindAddress": "0.0.0.0"/' "$PLUGIN_DIR/Plugins/RpcServer/RpcServer.json"

echo "Node Ready!"
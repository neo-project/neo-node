echo "Preparing neo node $1"
NEO_DIR="neo-cli-$1"
ARCHIVE="neo-cli.$1-linux-x64.tar.gz"
GITHUB_REPO="${GITHUB_REPO:-neo-project/neo-node}"
PLUGIN_VERSION="${2:-$1}"

download_if_missing() {
    local file=$1
    local version=$2
    if [ -f "$file" ]; then
        echo "Using local $file"
        return
    fi
    echo "Downloading $file ($version)"
    wget "https://github.com/${GITHUB_REPO}/releases/download/${version}/${file}"
}

download_if_missing "$ARCHIVE" "$1"
tar -xzf "$ARCHIVE"

if [ ! -d "$NEO_DIR" ]; then
    echo "Error: expected directory $NEO_DIR after extracting $ARCHIVE" >&2
    exit 1
fi

PLUGIN_DIR="./$NEO_DIR"

echo "Preparing plugins $PLUGIN_VERSION"
download_if_missing "ApplicationLogs.zip" "$PLUGIN_VERSION"
download_if_missing "RpcServer.zip" "$PLUGIN_VERSION"
download_if_missing "TokensTracker.zip" "$PLUGIN_VERSION"

unzip -n ApplicationLogs.zip -d "$PLUGIN_DIR/"
unzip -n RpcServer.zip -d "$PLUGIN_DIR/"
unzip -n TokensTracker.zip -d "$PLUGIN_DIR/"

sed -i 's/"BindAddress":[^,]*/"BindAddress": "0.0.0.0"/' "$PLUGIN_DIR/Plugins/RpcServer/RpcServer.json"

echo "Node Ready!"

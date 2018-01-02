#!/bin/bash
#
# This script is run inside the Docker container and tests neo-cli
#
set -e

cd /opt/neo-cli

# Run tests with expect
expect /opt/ci/test-neo-cli.expect

# Start neo-cli in background for additional JSON-RPC tests
screen -dmS node1 bash -c "dotnet neo-cli.dll --rpc"

# Wait a little bit
sleep 3

# Test a RPX smart contract query
JSONRPC_RES=$( curl --silent \
  --request POST \
  --url localhost:10332/ \
  --header 'accept: application/json' \
  --header 'content-type: application/json' \
  --data '{
    "jsonrpc": "2.0",
    "method": "invokefunction",
    "params": [
        "ecc6b20d3ccac1ee9ef109af5a7cdb85706b1df9",
        "totalSupply"
    ],
    "id": 3
  }' )

echo "JSON-RPC response: $JSONRPC_RES"

# Make sure we get a valid response
echo ${JSONRPC_RES} | grep --quiet "00c10b746f74616c537570706c7967f91d6b7085db7c5aaf09f19eeec1ca3c0db2c6ec"

# Make sure the response doesn't include "error"
if echo ${JSONRPC_RES} | grep --quiet "\"error\""; then
    echo "Error: \"error\" found in json-rpc response"
    exit 1
fi

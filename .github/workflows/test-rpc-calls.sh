#!/bin/bash

# Test RPC server calls
RPC_URL="http://127.0.0.1:10332"

echo "Testing RPC server consistency..."

# Test 1: getversion
echo "Testing getversion..."
response=$(curl -s -X POST -H "Content-Type: application/json" -d '{"jsonrpc": "2.0", "id": 1, "method": "getversion"}' $RPC_URL)
if [[ $? -ne 0 ]]; then
    echo "Failed to call getversion"
    exit 1
fi
echo "getversion response: $response"

# Check if response contains expected fields
if ! echo "$response" | jq -e '.result.tcpport' > /dev/null; then
    echo "getversion response missing tcpport"
    exit 1
fi

# Test 2: getbestblockhash
echo "Testing getbestblockhash..."
response=$(curl -s -X POST -H "Content-Type: application/json" -d '{"jsonrpc": "2.0", "id": 2, "method": "getbestblockhash"}' $RPC_URL)
if [[ $? -ne 0 ]]; then
    echo "Failed to call getbestblockhash"
    exit 1
fi
echo "getbestblockhash response: $response"

# Should be a string hash
if ! echo "$response" | jq -e '.result | type == "string"' > /dev/null; then
    echo "getbestblockhash response not a string"
    exit 1
fi

# Test 3: getblockcount
echo "Testing getblockcount..."
response=$(curl -s -X POST -H "Content-Type: application/json" -d '{"jsonrpc": "2.0", "id": 3, "method": "getblockcount"}' $RPC_URL)
if [[ $? -ne 0 ]]; then
    echo "Failed to call getblockcount"
    exit 1
fi
echo "getblockcount response: $response"

# Should be a number
if ! echo "$response" | jq -e '.result | type == "number"' > /dev/null; then
    echo "getblockcount response not a number"
    exit 1
fi

# Test 4: listplugins
echo "Testing listplugins..."
response=$(curl -s -X POST -H "Content-Type: application/json" -d '{"jsonrpc": "2.0", "id": 4, "method": "listplugins"}' $RPC_URL)
if [[ $? -ne 0 ]]; then
    echo "Failed to call listplugins"
    exit 1
fi
echo "listplugins response: $response"

# Should be an array
if ! echo "$response" | jq -e '.result | type == "array"' > /dev/null; then
    echo "listplugins response not an array"
    exit 1
fi

# Check if RpcServer is in the list
if ! echo "$response" | jq -e '.result[] | select(.name == "RpcServer")' > /dev/null; then
    echo "RpcServer not found in plugins list"
    exit 1
fi

echo "All RPC tests passed!"
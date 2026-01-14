#!/bin/bash
#
# Script to test the Test-with-plugins workflow steps locally
# Run this from the workspace root directory
#

set -e  # Exit on error

echo "=== Testing Test-with-plugins workflow steps locally ==="
echo ""

# Step 1: Build solution (includes CLI and all plugins)
echo "Step 1: Building solution (includes CLI and all plugins)..."
dotnet build -c Release
dotnet publish -o ./out -c Release src/Neo.CLI
echo "✓ Build complete"
echo ""

# Step 2: Create Plugins folder and copy plugin DLLs
echo "Step 2: Creating Plugins folder and copying plugin DLLs..."
mkdir -p ./out/Plugins
for plugin_dir in plugins/*/; do
  plugin_name=$(basename "$plugin_dir")
  build_output="$plugin_dir/bin/Release/net10.0"
  if [ -d "$build_output" ]; then
    echo "  Copying $plugin_name DLLs..."
    mkdir -p "./out/Plugins/$plugin_name"
    cp -r "$build_output"/* "./out/Plugins/$plugin_name/"
  else
    echo "  Warning: Build output not found for $plugin_name"
  fi
done
# Remove duplicated RpcServer.dll from TokensTracker and StorageDumper (they can't load twice)
echo "  Removing duplicated RpcServer.dll from TokensTracker and StorageDumper..."
rm -f ./out/Plugins/TokensTracker/RpcServer.dll
rm -f ./out/Plugins/StateService/RpcServer.dll
echo "✓ Plugin DLLs copied"
echo ""

# Step 3: Debug - List Plugins folder contents
echo "Step 3: Debug - Listing Plugins folder contents..."
echo "Plugins folder contents:"
ls -la ./out/Plugins/ || echo "Plugins folder does not exist"
echo ""
echo "Plugin directories:"
find ./out/Plugins -mindepth 1 -maxdepth 1 -type d | sort || echo "No plugin directories found"
echo ""
echo "Sample plugin DLLs (ApplicationLogs):"
ls -la ./out/Plugins/ApplicationLogs/*.dll 2>/dev/null | head -5 || echo "No DLLs found"
echo ""

# Step 4: Install dependencies (if not already installed)
echo "Step 4: Checking dependencies..."
if ! command -v expect &> /dev/null; then
  echo "  Installing expect..."
  # Check if we're root - no sudo needed
  if [ "$EUID" -eq 0 ]; then
    apt-get update
    apt-get install -y expect
  else
    sudo apt-get update
    sudo apt-get install -y expect
  fi
else
  echo "  expect already installed"
fi
echo ""

# Step 5: Run expect test
echo "Step 5: Running expect test..."
expect ./.github/workflows/test-neo-cli-plugins.expect
echo "✓ Expect test complete"
echo ""

echo "=== All tests completed successfully ==="


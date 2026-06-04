# Neo CLI Docker Usage Examples

## Example 1: Install CLI v4.0.0 (with matching plugins)

```bash
# Build the image (v4.0.0 is the default)
docker build -t v4.0.0 .

# Run the container
docker run -d -p 10332:10332 --name neo-node v4.0.0

# Enter the container and test
docker exec -it neo-node bash
curl -X POST http://localhost:10332 -H "Content-Type: application/json" -d '{"jsonrpc":"2.0","method":"getblockcount","params":[],"id":1}'
```

## Example 2: Install CLI v4.0.0 with Plugin v4.0.0

```bash
# Build the image
docker build -t v4.0.0-plugins-4.0.0 --build-arg PLUGIN_VERSION=v4.0.0 .

# Run the container
docker run -d -p 10332:10332 --name neo-node v4.0.0-plugins-4.0.0

# Enter the container and test
docker exec -it neo-node bash
curl -X POST http://localhost:10332 -H "Content-Type: application/json" -d '{"jsonrpc":"2.0","method":"getblockcount","params":[],"id":1}'
```

## Example 3: Install CLI v4.0.0 (with matching plugins)

```bash
# Build the image
docker build -t v4.0.0 --build-arg CLI_VERSION=v4.0.0 .

# Run the container
docker run -d -p 10332:10332 --name neo-node v4.0.0

# Enter the container and test
docker exec -it neo-node bash
curl -X POST http://localhost:10332 -H "Content-Type: application/json" -d '{"jsonrpc":"2.0","method":"getblockcount","params":[],"id":1}'
```

## Quick Test Command

Once inside the container, you can also use:
```bash
curl http://localhost:10332 -X POST -H "Content-Type: application/json" -d '{"jsonrpc":"2.0","method":"getblockcount","params":[],"id":1}'
```

This will return the current block count, confirming the CLI is running and responding to RPC calls.

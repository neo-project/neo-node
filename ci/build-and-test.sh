#!/bin/bash
#
# This script builds neo-cli with dotnet 2.0, and runs the tests.
#
CONTAINER_NAME="neo-cli-ci"

# Get absolute path of code and ci folder. This allows to run this script from
# anywhere, whether from inside this directory or outside.
DIR_CI="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
DIR_BASE="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && cd .. && pwd )"
# echo "CI directory: $DIR_CI"
# echo "Base directory: $DIR_BASE"

# Build the Docker image (includes building the current neo-cli code)
# docker build --no-cache -f $DIR_CI/Dockerfile -t $CONTAINER_NAME $DIR_BASE
docker build -f $DIR_CI/Dockerfile -t $CONTAINER_NAME $DIR_BASE

# Stop already running containers
CONTAINER=$(docker ps -aqf name=$CONTAINER_NAME)
if [ -n "$CONTAINER" ]; then
	echo "Stopping container named $CONTAINER_NAME"
	docker stop $CONTAINER_NAME 1>/dev/null
	echo "Removing container named $CONTAINER_NAME"
	docker rm -f $CONTAINER_NAME 1>/dev/null
fi

# Start a new test container
docker run --name $CONTAINER_NAME $CONTAINER_NAME /opt/ci/run-tests-in-docker.sh

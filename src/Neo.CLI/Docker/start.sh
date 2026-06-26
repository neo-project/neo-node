#!/bin/bash

cd /neo

rm -f neo.log

NEO_HOME="/neo/${NEO_CLI_DIR}"
NEO_CLI="${NEO_HOME}/neo-cli"
if [ ! -x "$NEO_CLI" ]; then
    echo "Error: neo-cli executable not found at $NEO_CLI" >&2
    exit 1
fi
if [ ! -f "${NEO_HOME}/libleveldb.so" ]; then
    echo "Error: libleveldb.so not found in ${NEO_HOME}" >&2
    exit 1
fi

# screen does not reliably keep the caller cwd; use explicit cd + absolute executable path
screen -L -Logfile /neo/neo.log -dmS neo \
    /bin/sh -c "cd '${NEO_HOME}' && exec '${NEO_CLI}'"

while [ ! -f /neo/neo.log ]; do
  sleep 0.5
done

tail -f /neo/neo.log

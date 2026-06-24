#!/bin/bash

cd /neo

rm -f neo.log

NEO_CLI="./${NEO_CLI_DIR}/neo-cli"
if [ ! -x "$NEO_CLI" ]; then
    echo "Error: neo-cli executable not found at $NEO_CLI" >&2
    exit 1
fi

screen -L -Logfile neo.log -dmS neo "$NEO_CLI"

while [ ! -f neo.log ]; do
  sleep 0.5
done

tail -f neo.log

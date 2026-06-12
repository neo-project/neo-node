#!/bin/bash

cd /neo

rm -f neo.log

if [ -x ./neo-cli/neo-cli ]; then
    NEO_CLI=./neo-cli/neo-cli
else
    NEO_CLI=./neo-cli
fi

screen -L -Logfile neo.log -dmS neo "$NEO_CLI"

while [ ! -f neo.log ]; do
  sleep 0.5
done

tail -f neo.log

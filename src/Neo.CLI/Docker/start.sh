#!/bin/bash

cd /neo

rm -f neo.log

screen -L -Logfile neo.log -dmS neo ./neo-cli/neo-cli

while [ ! -f neo.log ]; do
  sleep 0.5
done

tail -f neo.log

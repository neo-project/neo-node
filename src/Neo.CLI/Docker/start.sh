#!/bin/bash

# Start CLI in background, log output to neo.log
screen -dmS neo bash -c "./neo-cli/neo-cli > neo.log 2>&1"

while [ ! -f neo.log ]; do
  sleep 0.5
done

# Timely check log
tail -f neo.log
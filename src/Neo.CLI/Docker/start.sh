#!/bin/bash

# 启动 dotnet CLI 后台运行，日志输出到 neo.log
screen -dmS neo bash -c "./neo-cli/neo-cli > neo.log 2>&1"

# 等待 neo.log 出现
while [ ! -f neo.log ]; do
  sleep 0.5
done

# 实时查看日志（保持容器不退出）
tail -f neo.log
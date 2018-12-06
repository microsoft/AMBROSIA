#!/bin/bash
set -xeuo pipefail

# Run both processes together in one script.  This is intended to be
# used within the Docker container built by Dockerfile.

# TODO: set up your credentials before calling this script:
# export AZURE_STORAGE_CONN_STRING="..."

echo "Launching CRA worker"
# strace dotnet "/ambrosia/ImmortalCoordinator/bin/Release/netcoreapp2.0/linux-x64/ImmortalCoordinator.dll" rrnjob 1500 2> /tmp/coord.log &
dotnet "/ambrosia/ImmortalCoordinator/bin/Release/netcoreapp2.0/linux-x64/ImmortalCoordinator.dll" rrnjob 1500 &
PID1=$!
sleep 1
# dotnet "/ambrosia/ImmortalCoordinator/bin/Release/netcoreapp2.0/linux-x64/ImmortalCoordinator.dll" rrnserver 2500 &
# PID2=$!

# Lame, racy:
sleep 7

echo "Proceeding on the assumption you saw \"Ready...\" above this line..."
./service_v4.exe

wait $PID1
# wait $PID2

#!/bin/bash
echo "Using AZURE_STORAGE_CONN_STRING =" $AZURE_STORAGE_CONN_STRING
set -euo pipefail
################################################################################
# Run TWO docker containers, each with an ImmortalCoordinator, one
# containing the PTI server and one containing the client.
################################################################################

# This script is meant to be used in automated testing.  The output is
# ugly (interleaved) because it creates concurrent child processes.
#
# It should exit cleanly after the test is complete.

cd `dirname $0`
source ./default_var_settings.sh

INSTANCE_PREFIX=""
if [ $# -ne 0 ];
then INSTANCE_PREFIX="$1"
fi
CLIENTNAME=${INSTANCE_PREFIX}dock2C
SERVERNAME=${INSTANCE_PREFIX}dock2S

echo "Running PerformanceTestInterruptible between two containers"
echo "  Instance: names $CLIENTNAME, $SERVERNAME"

function DOCKRUN() {
    echo "Running docker container with: $*"
    docker run --rm --env AZURE_STORAGE_CONN_STRING="$AZURE_STORAGE_CONN_STRING" $*
}

DOCKRUN ambrosia-perftest Ambrosia RegisterInstance -i $CLIENTNAME --rp $PORT1 --sp $PORT2 -l "/ambrosia_logs/"
DOCKRUN ambrosia-perftest Ambrosia RegisterInstance -i $SERVERNAME --rp $PORT3 --sp $PORT4 -l "/ambrosia_logs/"

# [2018.11.29] Docker for Windows appears to have a bug that will not properly
# pass through an absolute path for the program to run, but instead will prepend
# "C:/Users/../vendor/git-for-windows/", incorrectly reinterpreting the path on
# the host *host*.  For now, simply assume they're in PATH:
DOCKRUN --env AMBROSIA_INSTANCE_NAME=$SERVERNAME --cidfile ./server.id \
        ambrosia-perftest runAmbrosiaService.sh \
        Server --rp $PORT4 --sp $PORT3 -j $CLIENTNAME -s $SERVERNAME -n 1 -c &

sleep 10 # Clarifies output.

DOCKRUN --env AMBROSIA_INSTANCE_NAME=$CLIENTNAME ambrosia-perftest runAmbrosiaService.sh \
	Job --rp $PORT2 --sp $PORT1 -j $CLIENTNAME -s $SERVERNAME --mms 65536 -n 2 -c

echo "Job docker image exited cleanly, killing the server one."
docker kill $(cat ./server.id)

echo "Docker ps should show as empty now:"
docker ps

echo "TwoContainers test mode completed."

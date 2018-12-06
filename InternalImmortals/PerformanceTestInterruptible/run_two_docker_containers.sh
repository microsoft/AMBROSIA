#!/bin/bash
echo "Using AZURE_STORAGE_CONN_STRING =" $AZURE_STORAGE_CONN_STRING
set -xeuo pipefail
################################################################################
# Run TWO docker containers, each with an ImmortalCoordinator, one
# containing the PTI server and one containing the client.
################################################################################

# This script is meant to be used in automated testing.  The output is
# ugly (interleaved) because it creates concurrent child processes.
#
# It should exit cleanly after the test is complete.

cd `dirname $0`
source ./Scripts/default_var_settings.sh
function DOCKRUN() {
    docker run --rm --env AZURE_STORAGE_CONN_STRING="$AZURE_STORAGE_CONN_STRING" $*
}

DOCKRUN ambrosia-perftest Ambrosia RegisterInstance -i $CLIENTNAME --rp $PORT1 --sp $PORT2 -l "/ambrosia_logs/" --lts 1024
DOCKRUN ambrosia-perftest Ambrosia RegisterInstance -i $SERVERNAME --rp $PORT3 --sp $PORT4 -l "/ambrosia_logs/" --lts 1024

# [2018.11.29] Docker for Windows appears to have a bug that will not properly
# pass through an absolute path for the program to run, but instead will prepend
# "C:/Users/../vendor/git-for-windows/", incorrectly reinterpreting the path on
# the host *host*.  For now, simply assume they're in PATH:
DOCKRUN --env AMBROSIA_INSTANCE_NAME=$SERVERNAME ambrosia-perftest runAmbrosiaService.sh \
        Server --rp $PORT4 --sp $PORT3 -j $CLIENTNAME -s $SERVERNAME -n 1 -c &
sleep 5
DOCKRUN --env AMBROSIA_INSTANCE_NAME=$CLIENTNAME ambrosia-perftest runAmbrosiaService.sh \
	Job --rp $PORT2 --sp $PORT1 -j $CLIENTNAME -s $SERVERNAME --mms 65536 -n 2 -c

#!/bin/bash
echo "Using AZURE_STORAGE_CONN_STRING =" $AZURE_STORAGE_CONN_STRING
set -xeuo pipefail

# This script is meant to be used in automated testing.  The output is
# ugly (interleaved) because it creates concurrent child processes.
#
# It should exit cleanly after the test is complete.

source `dirname $0`/Scripts/default_var_settings.sh

Ambrosia RegisterInstance -i $CLIENTNAME --rp $PORT1 --sp $PORT2 -l "/ambrosia_logs/" --lts 1024
Ambrosia RegisterInstance -i $SERVERNAME --rp $PORT3 --sp $PORT4 -l "/ambrosia_logs/" --lts 1024

set +x
start_coordinator -i $CLIENTNAME -p $CRAPORT1
pid_coord1=$coord_pid

start_coordinator -i $SERVERNAME -p $CRAPORT2
pid_coord2=$coord_pid
set -x

# TODO: could prefix output with a tag to make the interleaving more readable:
Server --rp $PORT4 --sp $PORT3 -j $CLIENTNAME -s $SERVERNAME -n 1 -c & 
pid_server=$!

Job --rp $PORT2 --sp $PORT1 -j $CLIENTNAME -s $SERVERNAME --mms 65536 -n 2 -c 
# When the client exits shut down the rest.
echo "Client finished, shutting down other processes."

kill -9 $pid_server
kill -9 $pid_coord1
kill -9 $pid_coord2

echo "Everything shut down.  All done."

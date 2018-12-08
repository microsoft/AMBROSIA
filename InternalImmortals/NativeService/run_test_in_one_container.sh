#!/bin/bash
echo "Using AZURE_STORAGE_CONN_STRING =" $AZURE_STORAGE_CONN_STRING
set -xeuo pipefail

# This script is meant to be used in automated testing.  The output is
# ugly (interleaved) because it creates concurrent child processes.
#
# It should exit cleanly after the test is complete.

# source `dirname $0`/default_var_settings.sh

PORT1=50001
PORT2=50002
PORT3=50003
PORT4=50004

CLIENTNAME=nativecli
SERVERNAME=nativeserv

# Wipe the server-side information:
UnsafeDeregisterInstance $CLIENTNAME
UnsafeDeregisterInstance $SERVERNAME

Ambrosia RegisterInstance -i $CLIENTNAME --rp $PORT1 --sp $PORT2 -l "./ambrosia_logs/" 
Ambrosia RegisterInstance -i $SERVERNAME --rp $PORT3 --sp $PORT4 -l "./ambrosia_logs/" 

# AMBROSIA_IMMORTALCOORDINATOR_PORT=2500 runAmbrosiaService.sh ./service_v4.exe 0 $SERVERNAME $PORT1 $PORT2 24 1 20 

AMBROSIA_INSTANCE_NAME=$CLIENTNAME AMBROSIA_IMMORTALCOORDINATOR_PORT=1500 \
  runAmbrosiaService.sh ./service_v4.exe 0 $SERVERNAME $PORT1 $PORT2 24 1 20 &

sleep 5
AMBROSIA_INSTANCE_NAME=$CLIENTNAME AMBROSIA_IMMORTALCOORDINATOR_PORT=2500 \
  runAmbrosiaService.sh ./service_v4.exe 1 $CLIENTNAME $PORT1 $PORT2 24 1 20



echo "Everything shut down.  All done."

#!/bin/bash

[[ "$AZURE_STORAGE_CONN_STRING" =~ ';AccountName='[^\;]*';' ]] && \
  echo "Using AZURE_STORAGE_CONN_STRING with account "${BASH_REMATCH}
set -euo pipefail

# This script is meant to be used in automated testing.  The output is
# ugly (interleaved) because it creates concurrent child processes.
#
# It should exit cleanly after the test is complete.

# source `dirname $0`/default_var_settings.sh

PORT1=49001
PORT2=49002
PORT3=49003
PORT4=49004

INSTANCE_PREFIX=""
if [ $# -ne 0 ];
then INSTANCE_PREFIX="$1"
fi
CLIENTNAME=${INSTANCE_PREFIX}nativeSend
SERVERNAME=${INSTANCE_PREFIX}nativeRecv

echo
echo "--------------------------------------------------------------------------------"
echo "Running NativeService with 4 processes all in this machine/container"
echo "  Instance: names $CLIENTNAME, $SERVERNAME"
echo "--------------------------------------------------------------------------------"
echo

set -x
time Ambrosia RegisterInstance -i $CLIENTNAME --rp $PORT1 --sp $PORT2 -l "./ambrosia_logs/" 
time Ambrosia RegisterInstance -i $SERVERNAME --rp $PORT3 --sp $PORT4 -l "./ambrosia_logs/" 
set +x

# AMBROSIA_IMMORTALCOORDINATOR_PORT=2500 runAmbrosiaService.sh ./service_v4.exe 0 $SERVERNAME $PORT1 $PORT2 24 1 20 

echo
echo "NativeService: Launching Receiver"
set -x
AMBROSIA_INSTANCE_NAME=$SERVERNAME AMBROSIA_IMMORTALCOORDINATOR_PORT=1500 \
COORDTAG=CoordRecv AMBROSIA_IMMORTALCOORDINATOR_LOG=./recvr.log \
  runAmbrosiaService.sh ./service_v4.exe 1 $CLIENTNAME $PORT3 $PORT4 24 1 20 &
pid_server=$!
set +x

sleep 12
if ! kill -0 $pid_server 2>/dev/null ; then
    echo
    echo " !!!  Server already died!  Not launching Sender.  !!!"
    echo
    exit 1
fi
echo
echo "NativeService: Launching Sender now:"
set -x
AMBROSIA_INSTANCE_NAME=$CLIENTNAME AMBROSIA_IMMORTALCOORDINATOR_PORT=2500 \
COORDTAG=CoordSend AMBROSIA_IMMORTALCOORDINATOR_LOG=./sender.log \
  runAmbrosiaService.sh ./service_v4.exe 0 $SERVERNAME $PORT1 $PORT2 24 1 20
set +x

echo
echo "NativeService client finished, shutting down Receiver."
kill $pid_server
wait
echo "Everything shut down.  All done."

echo "Attempt a cleanup of our table metadata:"
time UnsafeDeregisterInstance $CLIENTNAME || true
time UnsafeDeregisterInstance $SERVERNAME || true
echo "All done."

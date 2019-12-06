#!/bin/bash

rm /tmp/ambrosia_logs -rf
fuser -k 50500/tcp

[[ "$AZURE_STORAGE_CONN_STRING" =~ ';AccountName='[^\;]*';' ]] && \
  echo "Using AZURE_STORAGE_CONN_STRING with account "${BASH_REMATCH}
set -euo pipefail

# ------------------------------------------------------------------------------
# This script is meant to be used in automated testing.  The output is
# ugly (interleaved) because it creates concurrent child processes.
#
# It should exit cleanly after the test is complete.
#
# This is often invoked within Docker:
#   docker run -it --rm --env AZURE_STORAGE_CONN_STRING="$AZURE_STORAGE_CONN_STRING" ambrosia/ambrosia-perftest ./run_small_PTI_and_shutdown.sh
#
# ------------------------------------------------------------------------------

cd `dirname $0`
source ./default_var_settings.sh

# PORTOFFSET: A number to add to all ports to avoid colliding or
# reusing recently used ports.
if ! [ ${PORTOFFSET:+defined} ]; then
    PORTOFFSET=0
else
    PORT1=$((PORT1 + PORTOFFSET))
    PORT2=$((PORT2 + PORTOFFSET))
    PORT3=$((PORT3 + PORTOFFSET))
    PORT4=$((PORT4 + PORTOFFSET))
    CRAPORT1=$((CRAPORT1 + PORTOFFSET))
    CRAPORT2=$((CRAPORT2 + PORTOFFSET))
fi

INSTANCE_PREFIX=""
if [ $# -ne 0 ];
then INSTANCE_PREFIX="$1"
fi
CLIENTNAME=${INSTANCE_PREFIX}dockC
SERVERNAME=${INSTANCE_PREFIX}dockS

#-- Use Client name and server as part of Azure object so need to lower case
CLIENTNAME="${CLIENTNAME,,}"
SERVERNAME="${SERVERNAME,,}"


if ! which Ambrosia; then
    pushd ../../bin
    PATH=$PATH:`pwd`
    popd
fi

echo
echo "--------------------------------------------------------------------------------"
echo "PerformanceTestInterruptible with 4 processes all in this machine/container"
echo "  Instance: names $CLIENTNAME, $SERVERNAME"
echo "--------------------------------------------------------------------------------"
echo

if ! [ ${SKIP_REGISTER:+defined} ]; then
    set -x
    Ambrosia RegisterInstance -i $CLIENTNAME --rp $PORT1 --sp $PORT2 -l "/tmp/ambrosia_logs/" 
    set +x
fi

which runAmbrosiaService.sh
slog=`mktemp server.XXXX.log`
jlog=`mktemp job.XXXX.log`
slog="/tmp/"$slog
jlog="/tmp/"$jlog

echo
echo "PTI: Launching Job now:"
set -x
AMBROSIA_INSTANCE_NAME=$CLIENTNAME AMBROSIA_IMMORTALCOORDINATOR_PORT=$CRAPORT2 \
COORDTAG=coordcli AMBROSIA_IMMORTALCOORDINATOR_LOG=$jlog \
  runAmbrosiaService.sh ./Client/publish/Job --rp $PORT2 --sp $PORT1 -j $CLIENTNAME -s $SERVERNAME -n 3 -c 
set +x

echo
echo "PTI Client finished, shutting down server."
kill $pid_server
wait
echo "Everything shut down.  All background processes done."

echo "Attempt a cleanup of our table metadata:"
set -x
UnsafeDeregisterInstance $CLIENTNAME || true
rm $slog $jlog
set +x
echo "All done."

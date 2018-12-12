#!/bin/bash
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
#   docker run -it --rm --env AZURE_STORAGE_CONN_STRING="$AZURE_STORAGE_CONN_STRING" ambrosia-hello ./run_helloworld_locally.sh
#
# ------------------------------------------------------------------------------
cd `dirname $0`

if ! [ ${OFFSET:+defined} ]; then
    OFFSET=0
fi
PORT1=1000
PORT2=1001
PORT3=2000
PORT4=2001
CRAPORT1=1500
CRAPORT2=2500

# ME=`whoami`
ME=rrnewton
CLIENTNAME=${ME}client${OFFSET}
SERVERNAME=${ME}server${OFFSET}

echo
echo "--------------------------------------------------------------------------------"
echo "HelloWorld with 2 instances all in this machine/container"
echo "  Instance: names $CLIENTNAME, $SERVERNAME"
echo "--------------------------------------------------------------------------------"
echo

# Clear logs for this demonstration.
rm -rf ./ambrosia_logs/

echo "Running with AMBROSIA binaries from: "$(dirname `which runAmbrosiaService.sh`)

./run_helloworld_server.sh &
set +x
pid_server=$!
echo "Server launched as PID ${pid_server}.  Waiting a bit."
sleep 12

if ! kill -0 $pid_server 2>/dev/null ; then
    echo
    echo " !!!  Server already died!  Not launching Client.  !!!"
    echo
    exit 1
fi

echo
echo "Launching Client1 now:"
./run_helloworld_client.sh

echo
echo "Client finished, shutting down server."
kill $pid_server
wait
echo "Everything shut down.  All background processes done."

echo "Finally, attempt a cleanup of our table metadata:"
set -x
UnsafeDeregisterInstance $CLIENTNAME || true
UnsafeDeregisterInstance $SERVERNAME || true
rm *-coord.*.log
set +x

echo "HelloWorld all done."

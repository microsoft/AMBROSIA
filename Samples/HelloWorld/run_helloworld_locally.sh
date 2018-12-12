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

# PORTOFFSET: A number to add to all ports to avoid colliding or
# reusing recently used ports.
# if ! [ ${OFFSET:+defined} ]; then
#     OFFSET=0
# fi
# PORT1=$((1000 + OFFSET))
# PORT2=$((1001 + OFFSET))
# PORT3=$((1002 + OFFSET))
# PORT4=$((1003 + OFFSET))
# CRAPORT1=$((1004 + OFFSET))
# CRAPORT2=$((1005 + OFFSET))

if ! [ ${OFFSET:+defined} ]; then
    OFFSET=0
fi
PORT1=1000
PORT2=1001
PORT3=2000
PORT4=2001
CRAPORT1=1500
CRAPORT2=2500

ME=`whoami`
CLIENTNAME=${ME}client${OFFSET}
SERVERNAME=${ME}server${OFFSET}

if ! which Ambrosia 2> /dev/null; then
    echo "'Ambrosia' not found."
    echo "You need Ambrosia on your PATH.  Please download an AMBROSIA binary distribution."
    exit 1
fi

if ! [ -e Client1/publish/Client1.dll ]; then
    echo "Build products don't exist in ./Client1/publish."
    echo "Did you run ./build_dotnetcore.sh yet?"
    exit 1
fi

echo
echo "--------------------------------------------------------------------------------"
echo "HelloWorld with 2 instances all in this machine/container"
echo "  Instance: names $CLIENTNAME, $SERVERNAME"
echo "--------------------------------------------------------------------------------"
echo

if ! [ ${SKIP_REGISTER:+defined} ]; then
    set -x
    time Ambrosia RegisterInstance -i $CLIENTNAME --rp $PORT1 --sp $PORT2 -l "./ambrosia_logs/" 
    time Ambrosia RegisterInstance -i $SERVERNAME --rp $PORT3 --sp $PORT4 -l "./ambrosia_logs/"
    set +x
fi

echo "Running with AMBROSIA binaries from: "$(dirname `which runAmbrosiaService.sh`)
slog=`mktemp server.XXXX.log`
jlog=`mktemp client.XXXX.log`

echo
echo "PTI: Launching Server:"
set -x
AMBROSIA_INSTANCE_NAME=$SERVERNAME AMBROSIA_IMMORTALCOORDINATOR_PORT=$CRAPORT1 \
COORDTAG=CoordServ AMBROSIA_IMMORTALCOORDINATOR_LOG=$slog \
  runAmbrosiaService.sh dotnet ./Server/publish/Server.dll $SERVERNAME

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
echo "PTI: Launching Client1 now:"
set -x
AMBROSIA_INSTANCE_NAME=$CLIENTNAME AMBROSIA_IMMORTALCOORDINATOR_PORT=$CRAPORT2 \
COORDTAG=CoordCli AMBROSIA_IMMORTALCOORDINATOR_LOG=$jlog \
  runAmbrosiaService.sh dotnet ./Client1/Publish/Client1.dll $CLIENTNAME $SERVERNAME
set +x

echo
echo "PTI Client finished, shutting down server."
kill $pid_server
wait
echo "Everything shut down.  All background processes done."

echo "Attempt a cleanup of our table metadata:"
set -x
UnsafeDeregisterInstance $CLIENTNAME || true
UnsafeDeregisterInstance $SERVERNAME || true
rm $slog $jlog
set +x


echo "HelloWorld all done."

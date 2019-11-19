#!/bin/bash
[[ "$AZURE_STORAGE_CONN_STRING" =~ ';AccountName='[^\;]*';' ]] && \
  echo "Using AZURE_STORAGE_CONN_STRING with account "${BASH_REMATCH}
set -euo pipefail
cd `dirname $0`

PORT1=2000
PORT2=2001
CRAPORT1=2500

ME=`whoami | sed 's/[^a-zA-Z0-9]//g'`
CLIENTNAME=${ME}client
SERVERNAME=${ME}server

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
echo "HelloWorld Client Starting, name $CLIENTNAME" 
echo

set -x
Ambrosia RegisterInstance -i $CLIENTNAME --rp $PORT1 --sp $PORT2 -l "./ambrosia_logs/" 
set +x

clog=`mktemp client-coord.XXXX.log`

set -x
AMBROSIA_INSTANCE_NAME=$CLIENTNAME AMBROSIA_IMMORTALCOORDINATOR_PORT=$CRAPORT1 \
COORDTAG=CoordCli AMBROSIA_IMMORTALCOORDINATOR_LOG=$clog \
  runAmbrosiaService.sh dotnet Client1/publish/Client1.dll $PORT2 $PORT1 $CLIENTNAME $SERVERNAME
set +x
echo "HelloWorld Client finished."

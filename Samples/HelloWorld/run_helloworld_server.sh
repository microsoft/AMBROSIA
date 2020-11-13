#!/bin/bash
[[ "$AZURE_STORAGE_CONN_STRING" =~ ';AccountName='[^\;]*';' ]] && \
  echo "Using AZURE_STORAGE_CONN_STRING with account "${BASH_REMATCH}
set -euo pipefail
cd `dirname $0`

PORT3=2000
PORT4=2001
CRAPORT2=2500

ME=`whoami | sed 's/[^a-zA-Z0-9]//g'`
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
echo "HelloWorld Server Starting, name $SERVERNAME" 
echo
set -x
Ambrosia RegisterInstance -i $SERVERNAME --rp $PORT3 --sp $PORT4 -l "./ambrosia_logs/"
set +x

slog=`mktemp server-coord.XXXX.log`

set -x
AMBROSIA_INSTANCE_NAME=$SERVERNAME AMBROSIA_IMMORTALCOORDINATOR_PORT=$CRAPORT2 \
COORDTAG=CoordServ AMBROSIA_IMMORTALCOORDINATOR_LOG=$slog \
  runAmbrosiaService.sh dotnet Server/publish/Server.dll $SERVERNAME true $PORT4 $PORT3  
set +x

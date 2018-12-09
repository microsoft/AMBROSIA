
# A convenience --to be sourced (source .set_env.sh) into your shell
# when developing AMBROSIA:

echo
echo "Setting PATH for AMBROSIA development..."

TOADD=`pwd`/bin
mkdir -p "$TOADD"
if [ "$PATH" == "" ]; then PATH=$TOADD;
elif [[ ":$PATH:" != *":$TOADD:"* ]]; then PATH="$PATH:$TOADD";
fi
export PATH

if [[ ${AZURE_STORAGE_CONN_STRING:-defined} ]]; then
    echo "NOTE: AZURE_STORAGE_CONN_STRING is set to:"
    echo
    echo "  $AZURE_STORAGE_CONN_STRING"
    echo
    echo "Confirm that this is the one you want to develop with."
else
    echo "Warning AZURE_STORAGE_CONN_STRING is not set."
    echo "You'll need that for registering instances and running AMBROSIA."
fi

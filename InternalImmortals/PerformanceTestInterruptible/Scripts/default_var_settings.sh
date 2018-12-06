
PORT1=50001
PORT2=50002
PORT3=50003
PORT4=50004

CLIENTNAME=dockertest1
SERVERNAME=dockertest2

CRAPORT1=1500
CRAPORT2=2500

cd `dirname $0`
myscriptdir=`pwd`

# Arguments: all passed through to the coordinator.
# Returns: when the Coordinator is READY (in the background).
# Returns: sets "coord_pid" to the return value.
#
# Side effect: uses a log file on disk in the same directory as this script.
# Side effect: runs a tail process in the background
function start_coordinator() {
    local COORDLOG="$myscriptdir/coord_out_client.txt"    
    echo "Launching coordingator with: ImmortalCoordinator" $*
    echo "  Redirecting output to: $COORDLOG"
    "ImmortalCoordinator" $* 2>1 >"$COORDLOG" &
    coord_pid=$!

    while [ ! -e "$COORDLOG" ]; do
	echo " -> Waiting for $COORDLOG to appear"
	sleep 1
    done
    tail -F $COORDLOG &

    while ! grep -q "Ready" "$COORDLOG" && kill -0 $coord_pid 2>- ;
    do sleep 2; done

    if ! kill -0 $coord_pid 2>- ;
    then echo
	 echo "ERROR: coordinator died while we were waiting.  Final log ended with:"
	 tail $COORDLOG
	 exit 1;
    fi
    echo "Coordinator ready."
}

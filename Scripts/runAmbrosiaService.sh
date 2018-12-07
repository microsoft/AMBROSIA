#!/bin/bash
set -euo pipefail

################################################################################
# A script to launch a service instance (coordinator + app), often
# inside a container.
################################################################################

# ----------------------------------------------------------
# Responds to these ENVIRONMENT VARIABLES:
#
#  * AMBROSIA_INSTANCE_NAME            (required)
#
#  * AMBROSIA_IMMORTALCOORDINATOR_PORT (optional)
#    - this port should be open on the container, and is used for
#      coordinator-coordinator communication
#
#  * AMBROSIA_SILENT_COORDINATOR        (optional)
#    - if set, this suppresses coordinator messages to stdout,
#      but they still go to /var/log/ImmortalCoordinator.log
#
# ----------------------------------------------------------

function print_usage() {
    echo "USAGE: $0 <service-binary> <service-args>*"
    echo    
    echo "This script takes a command (and arguments) that runs the application binary."
    echo "The script launches the ImmortalCoordinator in the background before launching"
    echo "the application."
    echo 
    echo "Required Environment Variables"
    echo "------------------------------"
    echo 
    echo " * AMBROSIA_INSTANCE_NAME:  must be bound to the service instance name."
    echo "   This is the same name that was registered with 'ambrosia RegisterInstance' "
    echo
    echo "Optional Environment Variables"
    echo "------------------------------"
    echo
    echo " * AMBROSIA_IMMORTALCOORDINATOR_PORT - default 1500"
    echo
    exit 1
}

if [ $# -eq 0 ]; then print_usage; fi

if [[ ! -v AMBROSIA_INSTANCE_NAME ]]; then
    echo "ERROR: unbound environment variable: AMBROSIA_INSTANCE_NAME"
    echo 
    print_usage
    exit 1
fi

if [[ -v AMBROSIA_IMMORTALCOORDINATOR_PORT ]];
then
    echo "Using environment var AMBROSIA_IMMORTALCOORDINATOR_PORT=$AMBROSIA_IMMORTALCOORDINATOR_PORT"
else
    AMBROSIA_IMMORTALCOORDINATOR_PORT=1500
    echo "Using default AMBROSIA_IMMORTALCOORDINATOR_PORT of $AMBROSIA_IMMORTALCOORDINATOR_PORT"
fi

COORDLOG=/var/log/ImmortalCoordinator.log

# Arguments: all passed through to the coordinator.
# Returns: when the Coordinator is READY (in the background).
# Returns: sets "coord_pid" to the return value.
#
# ASSUMES: ImmortalCoordinator in $PATH
#
# Side effect: uses a log file on disk in the same directory as this script.
# Side effect: runs a tail proycess in the background
function start_immortal_coordinator() {
    echo "Launching coordingator with: ImmortalCoordinator" $*
    echo "  Redirecting output to: $COORDLOG"

    if ! truncate -s 0 "$COORDLOG";
    then COORDLOG="./ImmortalCoordinator.log"
	 echo " ! WARNING [runAmbrosiaService.sh]: could not write coordinator log, using $COORDLOG instead."
	 truncate -s 0 "$COORDLOG"
    fi
    
    if which rotatelogs; then 
	# Bound the total amount of output used by the ImmortalCoordinator log:
	ImmortalCoordinator $* 2>1 | rotatelogs -f -t "$COORDLOG" 10M &
	coord_pid=$!
    else
	echo " ! WARNING [runAmbrosiaService.sh]: rotatelogs not available, NOT bounding $COORDLOG size."
	ImmortalCoordinator $* 2>1 > "$COORDLOG" &
	coord_pid=$!
    fi

    while [ ! -e "$COORDLOG" ]; do
	echo " -> Waiting for $COORDLOG to appear"
	sleep 1
    done
    if [[ ! -v AMBROSIA_SILENT_COORDINATOR ]]; then
	tail -F $COORDLOG | while read l; do echo " [ImmortalCoord] $l"; done &
    fi
    while ! grep -q "Ready" "$COORDLOG" && kill -0 $coord_pid 2>- ;
    do sleep 2; done

    if ! kill -0 $coord_pid 2>- ;
    then echo
	 echo "ERROR [runAmbrosiaService.sh]"
	 echo "Coordinator died while we were waiting.  Final log ended with:"
	 echo "--------------------------------------------------------------"
	 tail $COORDLOG
	 exit 1;
    fi
    echo "Coordinator ready."
}

# TODO: health monitoring.  Show an example of AMBROSIA best practice here.


# Step 1: 
start_immortal_coordinator -i $AMBROSIA_INSTANCE_NAME -p $AMBROSIA_IMMORTALCOORDINATOR_PORT

# Step 2:
echo "Launching app client process:"
set -x
$*
set +x

echo "Ambrosia: client exited, killing coordinator..."
kill $coord_pid || echo ok


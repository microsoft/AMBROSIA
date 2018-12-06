#!/bin/bash
set -euo pipefail

################################################################################
# Script to launch a service instance (coordinator + app), often
# inside a container.
################################################################################

# Responds to ENV VARS:
#  * AMBROSIA_INSTANCE_NAME            (required)
#
#  * AMBROSIA_IMMORTALCOORDINATOR_PORT (optional)
#    - this port should be open on the container, and is used for
#      coordinator-coordinator communication
#
#  * AMBROSIA_SILENT_COORDINATOR        (optional)
#    - if set, this suppresses coordinator messages to stdout,
#      but they still go to /var/log/ImmortalCoordinator.log


if [[ ! -v AMBROSIA_INSTANCE_NAME ]]; then
  echo "ERROR: unbound environment variable: AMBROSIA_INSTANCE_NAME"
  echo "runAmbrosiaService.sh expects it to be bound to the service instance name."
  echo "This is the same name that was registered with 'ambrosia RegisterInstance' "
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
    # Bound the total amount of output used by the ImmortalCoordinator log:
    ImmortalCoordinator $* 2>1 | rotatelogs -f -t "$COORDLOG" 10M &
    coord_pid=$!

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
	 echo "ERROR: coordinator died while we were waiting.  Final log ended with:"
	 tail $COORDLOG
	 exit 1;
    fi
    echo "Coordinator ready."
}

# Step 1: 
start_immortal_coordinator -i $AMBROSIA_INSTANCE_NAME -p $AMBROSIA_IMMORTALCOORDINATOR_PORT

# Step 2:
echo "Launching app client process:"
set -x
$*
set +x

echo "Ambrosia: client exited, killing coordinator..."
kill $coord_pid || echo ok


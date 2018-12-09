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

TAG="[runAmbrosiaService.sh]"

if ! [ "${COORDTAG:+defined}" ];
then COORDTAG="ImmortalCoord"
fi

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
    echo "ERROR $TAG: unbound environment variable: AMBROSIA_INSTANCE_NAME"
    echo 
    print_usage
    exit 1
fi

if [[ -v AMBROSIA_IMMORTALCOORDINATOR_PORT ]];
then
    echo " $TAG Using environment var AMBROSIA_IMMORTALCOORDINATOR_PORT=$AMBROSIA_IMMORTALCOORDINATOR_PORT"
else
    AMBROSIA_IMMORTALCOORDINATOR_PORT=1500
    echo " $TAG Using default AMBROSIA_IMMORTALCOORDINATOR_PORT of $AMBROSIA_IMMORTALCOORDINATOR_PORT"
fi

if [[ "${AMBROSIA_IMMORTALCOORDINATOR_LOG:+defined}" ]]; then
    COORDLOG="${AMBROSIA_IMMORTALCOORDINATOR_LOG}"
    echo " $TAG Responding to env var AMBROSIA_IMMORTALCOORDINATOR_LOG=$COORDLOG"
else
    # The canonical location, especially when running in a Docker container:
    COORDLOG=/var/log/ImmortalCoordinator.log
fi

# Arguments: all passed through to the coordinator.
# Returns: when the Coordinator is READY (in the background).
# Returns: sets "coord_pid" to the return value.
#
# ASSUMES: ImmortalCoordinator in $PATH
#
# Side effect: uses a log file on disk in the same directory as this script.
# Side effect: runs a tail proycess in the background
function start_immortal_coordinator() {
    echo " $TAG Launching coordingator with: ImmortalCoordinator" $*
    if ! which ImmortalCoordinator; then
        echo "  ERROR $TAG - ImmortalCoordinator not found on path!"
        exit 1
    fi    
    echo " $TAG   Creating zero length log file: $COORDLOG"
    if ! truncate -s 0 "$COORDLOG";
    then
        COORDLOG=`mktemp ImmortalCoordinator.XXXXXX.log`
        echo " ! WARNING $TAG: could not write coordinator log, using $COORDLOG instead."
        truncate -s 0 "$COORDLOG"
    fi
    echo " $TAG   Redirecting output to: $COORDLOG"
    
    if which rotatelogs; then 
        # Bound the total amount of output used by the ImmortalCoordinator log:
        ImmortalCoordinator $* 2>&1 | rotatelogs -f -t "$COORDLOG" 10M &
        coord_pid=$!
    else
        echo " ! WARNING $TAG: rotatelogs not available, NOT bounding size of $COORDLOG"
        ImmortalCoordinator $* >>"$COORDLOG" 2>&1 &
        coord_pid=$!
    fi

    while [ ! -e "$COORDLOG" ]; do
        echo " $TAG -> Waiting for $COORDLOG to appear"
        sleep 1
    done
    if [[ ! -v AMBROSIA_SILENT_COORDINATOR ]]; then
        # It seems like tail -F is spitting the same output multiple times on 
        # A complicated but full-proof bash method of line-by-line reading:
        tail -F "$COORDLOG" | \
            while IFS='' read -r line || [[ -n "$line" ]]; do
                echo " [$COORDTAG] $line";
            done &
    fi
    while ! grep -q "Ready" "$COORDLOG" && kill -0 $coord_pid 2>/dev/null ;
    do sleep 2; done
    
    if ! kill -0 $coord_pid 2>/dev/null ;
    then echo
         echo "--------------- ERROR $TAG ----------------"
         echo "Coordinator died while we were waiting.  Final log ended with:"
         echo "--------------------------------------------------------------"
         tail -n20 $COORDLOG
         echo "--------------------------------------------------------------"   
         exit 1;
    fi
    echo " $TAG Coordinator looks ready."
}

# Health monitoring. This is an example of AMBROSIA required practice:
# the coordinator and application are bound together, if one dies the
# other must (as though they were one process.)
# 
keep_monitoring=`mktemp healthMonitorContinue.XXXXXX`
touch $keep_monitoring

function monitor_health() {
    echo " $TAG Health monitor starting coord_pid=$coord_pid, app_pid=$app_pid"
    while [ -f $keep_monitoring ]; do
        sleep 2
        if ! kill -0 $coord_pid 2>- ; then
            echo "-------------------------------------------------------"
            echo "ERROR $TAG ImmortalCoordinator died!"
            echo "Killing application process.  Must initiate recovery."
            echo "-------------------------------------------------------"
            set -x 
            kill -9 $app_pid
            exit 1
        fi
    done
    echo " $TAG Cleanly exiting health monitor background function..."
}

# Step 1: 
start_immortal_coordinator -i $AMBROSIA_INSTANCE_NAME -p $AMBROSIA_IMMORTALCOORDINATOR_PORT

# Step 2:
echo " $TAG Launching app client process:"
set -x
$* &
set +x
app_pid=$!

monitor_health &

wait $app_pid
echo " $TAG Ambrosia: client exited, killing coordinator and health monitor function..."
kill -9 $coord_pid || echo ok
rm -f $keep_monitoring
wait

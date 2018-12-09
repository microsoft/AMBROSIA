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

# Don't let this fail, it's just informative:
APPNAME=`basename $1` || APPNAME="$1"

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

# Helper functions
# --------------------------------------------------

function tag_stdin() {
    local MSG=$1
    # A complicated but full-proof bash method of line-by-line reading:        
    while IFS='' read -r line || [[ -n "$line" ]]; do
        echo " [$MSG] $line";
    done
}

# Global variable set below:
tail_pid=""

function tail_tagged() {
    local MSG=$1
    local FILE=$2
    (tail -F "$FILE" | tag_stdin $MSG) &
    tail_pid=$!
}

# --------------------------------------------------

# Arguments: all passed through to the coordinator.
# Returns: when the Coordinator is READY (in the background).
# Returns: sets "coord_pid" to the return value.
#
# ASSUMES: ImmortalCoordinator in $PATH
#
# Side effect: uses a log file on disk in the same directory as this script.
# Side effect: runs a tail proycess in the background
function start_immortal_coordinator() {
    echo " $TAG Launching coordinator with: ImmortalCoordinator" $*
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

    if [[ ! -v AMBROSIA_SILENT_COORDINATOR ]]; then
        tail_tagged "$COORDTAG" "$COORDLOG"
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
        if ! kill -0 $coord_pid 2>/dev/null ; then
            # Sleep a bit here to make sure it's really a crash and not just normal shutdown:
            sleep 2
            if [ -f $keep_monitoring ]; then
                echo "-------------------------------------------------------"
                echo "ERROR $TAG ImmortalCoordinator died! ($COORDTAG)"
                echo "Likewise killing application process; they're a pair."
                echo "-------------------------------------------------------"
                set -x 
                kill -9 $app_pid
                exit 1
            fi
        fi
    done
    echo " $TAG Cleanly exiting health monitor background function..."
}

# Step 1: 
start_immortal_coordinator -i $AMBROSIA_INSTANCE_NAME -p $AMBROSIA_IMMORTALCOORDINATOR_PORT

# Step 2:
echo " $TAG Launching app process alongside coordinator:"
set -x
$* &
set +x
app_pid=$!

monitor_health &

wait $app_pid
echo " $TAG Ambrosia app cleanly exited ($APPNAME)"
if [ "$tail_pid" != "" ]; then
    kill $tail_pid
    echo " $TAG  |-> Stopped echoing coordinator output (transient errors in shutdown not important)."
fi
echo " $TAG  |-> Killing coordinator and health monitor function."
rm -f $keep_monitoring

kill -9 $coord_pid || echo ok
echo " $TAG  |-> Cleanup complete, exiting."
# wait 

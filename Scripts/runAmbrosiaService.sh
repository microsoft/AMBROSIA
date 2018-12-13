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
    echo "USAGE: $0 [--quiet] <service-binary> <service-args>*"
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

while [ $# -ge 1 ]; do
    case $1 in
        --quiet) shift; RUNAMBROSIA_QUIET=1 ;;
        *) break ;;
    esac
done
if [ $# -eq 0 ]; then print_usage; fi

# Don't let this fail, it's just informative:
APPNAME=`basename $1` || APPNAME="$1"

if ! [[ ${AMBROSIA_INSTANCE_NAME:+defined} ]]; then
    echo "ERROR $TAG: unbound environment variable: AMBROSIA_INSTANCE_NAME"
    echo 
    print_usage
    exit 1
fi

if [[ ${RUNAMBROSIA_QUIET:+defined} ]];
then AMBROSIA_SILENT_COORDINATOR=1
     function tag() { return; }
else function tag() { echo " $TAG $*"; }     
fi

if [[ ${AMBROSIA_IMMORTALCOORDINATOR_PORT:+defined} ]];
then
    tag "Using environment var AMBROSIA_IMMORTALCOORDINATOR_PORT=$AMBROSIA_IMMORTALCOORDINATOR_PORT"
else
    AMBROSIA_IMMORTALCOORDINATOR_PORT=1500
    tag "Using default AMBROSIA_IMMORTALCOORDINATOR_PORT of $AMBROSIA_IMMORTALCOORDINATOR_PORT"
fi

if [[ "${AMBROSIA_IMMORTALCOORDINATOR_LOG:+defined}" ]]; then
    COORDLOG="${AMBROSIA_IMMORTALCOORDINATOR_LOG}"
    tag "Responding to env var AMBROSIA_IMMORTALCOORDINATOR_LOG=$COORDLOG"
else
    # The canonical location, especially when running in a Docker container:
    COORDLOG=/var/log/ImmortalCoordinator.log
fi
    
# Helper functions
# --------------------------------------------------

# Global variable set below:
tail_pid=""
coord_pid=""
app_pid=""
keep_monitoring=""

_normal_cleanup() {
  kill -TERM "$coord_pid" 2>/dev/null  || true
  kill -TERM "$tail_pid"  2>/dev/null  || true
  kill -TERM "$app_pid"   2>/dev/null  || true
  rm -f "$keep_monitoring" 2>/dev/null || true
}

_unexpected_cleanup() {
    trap '' EXIT # some shells will call EXIT after the INT handler
    if [ $# -gt 0 ];
    then local WHCH="($1)"
    else local WHCH=""
    fi
    echo "$0: Exiting script abnormally! Cleaning up. $WHCH" 
    _normal_cleanup
    echo "$0: Done with cleanup."
}

trap _normal_cleanup     EXIT
trap _unexpected_cleanup TERM INT QUIT

function tag_stdin() {
    local MSG=$1
    # A complicated but full-proof bash method of line-by-line reading:        
    while IFS='' read -r line || [[ -n "$line" ]]; do
        echo " [$MSG] $line";
    done
}

function tail_tagged() {
    local MSG=$1
    local FILE=$2
    tail -F "$FILE" | tag_stdin $MSG &
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
    tag "Launching coordinator with: ImmortalCoordinator" $*
    if ! which ImmortalCoordinator 2> /dev/null; then
        echo "  ERROR $TAG - ImmortalCoordinator not found on path!"
        exit 1
    fi
    if [ -e "$COORDLOG" ]; then
        tag "  Removing existing log file $COORDLOG"
        rm "$COORDLOG"
    fi
    tag "  Creating zero-length log file at $COORDLOG"
    if ! touch "$COORDLOG";
    then
        COORDLOG=`mktemp ImmortalCoordinator.XXXXXX.log`
        echo " ! WARNING $TAG: could not write coordinator log, using $COORDLOG instead."
        touch "$COORDLOG"
    fi
    tag "  Redirecting output to: $COORDLOG"

    if [[ ${AMBROSIA_SILENT_COORDINATOR:+defined} ]]; then
	# OPTION (1): No output from Coordinator to stdout/stderr:
	ImmortalCoordinator $* 2>&1 > "$COORDLOG" &
	coord_pid=$!
	
    elif [ ${RUNAMBROSIA_USE_TAIL:+defined} ]; then
	# OPTION (2): Bound logs, but complicated (and tends to leave stray tail processes)
	# ---------------------------------------------------------------------------------
	if which rotatelogs 2> /dev/null ; then 
	    # Bound the total amount of output used by the ImmortalCoordinator log:
	    ImmortalCoordinator $* 2>&1 | rotatelogs -f -t "$COORDLOG" 10M &
	    coord_pid=$!
	else
	    echo " ! WARNING $TAG: rotatelogs not available, NOT bounding size of $COORDLOG"
	    ImmortalCoordinator $* >>"$COORDLOG" 2>&1 &
	    coord_pid=$!
	fi
	if ! [[ ${AMBROSIA_SILENT_COORDINATOR:+defined} ]]; then
	    tail_tagged "$COORDTAG" "$COORDLOG"
	fi
    else
	# OPTION (3) Just use tee. Don't bound coordinator log on disk.
	# -------------------------------------------------------------
	ImmortalCoordinator $* 2>&1 | tee "$COORDLOG" | tag_stdin "$COORDTAG" &
	coord_pid=$!
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
    tag "Coordinator looks ready."
}

# Script main body:
# --------------------------------------------------------------------------------

# Health monitoring. This is an example of AMBROSIA required practice:
# the coordinator and application are bound together, if one dies the
# other must (as though they were one process.)
# 
keep_monitoring=`mktemp healthMonitorContinue.XXXXXX`
touch $keep_monitoring

function monitor_health() {
    tag "Health monitor starting, coord_pid=$coord_pid, app_pid=$app_pid"
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
                kill $app_pid
                exit 1
            fi
        fi
    done
    tag "Cleanly exiting health monitor background function..."
}

# Step 1: 
start_immortal_coordinator -i $AMBROSIA_INSTANCE_NAME -p $AMBROSIA_IMMORTALCOORDINATOR_PORT

# Step 2:
tag "Launching app process alongside coordinator:"
# Test for interactive shell:
if tty -s; then
    set -x
    $* < /dev/stdin &
    set +x
    app_pid=$!
else
    set -x    
    $* &
    set +x
    app_pid=$!
fi

monitor_health &

wait $app_pid
tag "Ambrosia app cleanly exited ($APPNAME)"
if [ "$tail_pid" != "" ]; then
    kill $tail_pid
    tag " |-> Stopped echoing coordinator output (transient errors in shutdown not important)."
fi
tag " |-> Killing coordinator and health monitor function."
rm -f $keep_monitoring

kill $coord_pid || echo ok
tag " |-> Cleanup complete, exiting."
# wait 

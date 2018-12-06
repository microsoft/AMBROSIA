#!/bin/bash

set -euo pipefail

# Requires: Bash shell on Windows (cygwin/Git bash) or Linux.

# This script builds THREE docker containers:
#  (1) ambrosia-dev: the core library, sources, and binaries
#  (2) ambrosia: the binary release only
#  (3) ambrosia-perftest: an example application on top.
# 
# Run this inside a fresh working copy.
#
# Additionally, this responds to the following environment variable:
#
#  * AZURE_STORAGE_CONN_STRING : set appropriately
#  * PTI_MOUNT_LOGS = "ExternalLogs" | "InternalLogs"
#                     (default Internal)
#  * PTI_MODE = "OneContainer" | "TwoContainers"
#      (controls whether to do an intra- or inter-container test)

TAG1A=ambrosia-dev
TAG1B=ambrosia
TAG2=ambrosia-perftest

if [[ ! -v DOCKER ]]; then
   DOCKER=docker
fi

export AMBROSIA_ROOT=`pwd`

if [ $# -eq 0 ]; then
    mode="build";
else 
    mode="$1";
fi

# Default logs location:
if [ `uname` == "Linux" ]; then
    AMBROSIA_LOGDIR="/tmp/logs"
else
    AMBROSIA_LOGDIR="c:\\logs"
fi

# --------------------------------------------------------------------------------
if [ "$mode" != "runonly" ]; then

    echo "================================================================================"
    echo "Building images $TAG1A, $TAG1B, $TAG2.  Script's invoked in mode = $mode"
    if [ $mode == "build" ]; 
    then echo "Pass 'run' as the first argument to also run PerformanceTestInterruptable.";
    fi
    echo "================================================================================"
    echo
    
    $DOCKER build                       -t ${TAG1A} .
    
    if [[ ! -v BUILD_DEV_IMAGE_ONLY ]]; then
	$DOCKER build -f Dockerfile.release -t ${TAG1B} .
    fi
    
    pushd "$AMBROSIA_ROOT"/InternalImmortals/PerformanceTestInterruptible
    $DOCKER build -t ${TAG2} .
    popd

    # TODO: build other examples:
    # cd InternalImmortals/NativeService; $DOCKER build -t ambrosia-native .

    echo
    echo "Docker images built successfully."
    echo
    echo "Below is an example command bring up the generated image interactively:"
    echo "  $DOCKER run -it --rm --env AZURE_STORAGE_CONN_STRING=... ${TAG2}"
    echo
    echo "Extracting a release tarball..."
    set -x
    rm -rf ambrosia.tgz
    TMPCONT=temp-container-name_`date '+%s'`
    $DOCKER run --name $TMPCONT ambrosia-dev bash -c 'tar czvf /ambrosia/ambrosia.tgz /ambrosia/bin'
    $DOCKER cp $TMPCONT:/ambrosia/ambrosia.tgz ambrosia.tgz
    $DOCKER rm $TMPCONT
    set +x

fi
if [ "$mode" == "build" ]; then
    exit 0;
fi

# RUN mode:
# --------------------------------------------------------------------------------
echo "Running a single Docker container with PerformanceTestInterruptable."
echo "Using AZURE_STORAGE_CONN_STRING from your environment."
if [[ ! -v AZURE_STORAGE_CONN_STRING ]]; then
    echo "ERROR: you must have AZURE_STORAGE_CONN_STRING set to call this script.";
    exit 1;
else
    # When running as well as building  we pass this through into the container:
    OPTS=" --env AZURE_STORAGE_CONN_STRING=${AZURE_STORAGE_CONN_STRING}"
fi

# Set the default value if unset:
if [[ ! -v PTI_MOUNT_LOGS ]]; then
    PTI_MOUNT_LOGS="InternalLogs";
    echo " * defaulting to PTI_MOUNT_LOGS=$PTI_MOUNT_LOGS"
fi

if [[ ! -v PTI_MODE ]]; then
    PTI_MODE="OneContainer"
    echo " * defaulting to PTI_MOUNT_LOGS=$PTI_MOUNT_LOGS"
fi

# Optional: mount logs from *outside* the container:
case $PTI_MOUNT_LOGS in
    InternalLogs)
    ;;
    ExternalLogs)
	# [2018.11.27] RRN: Having problems with this on Windows^:
	OPTS+=" -v ${AMBROSIA_LOGDIR}:/ambrosia_logs "
    ;;
    *)
	echo "ERROR: invalid value of PTI_MOUNT_LOGS=$PTI_MOUNT_LOGS";
	echo "  (expected 'InternalLogs' or 'ExternalLogs')";
	exit 1;
    ;;
esac

case $PTI_MODE in
    OneContainer)
	echo "Running PTI server/client both inside ONE container:"
	set -x
	$DOCKER run --rm ${OPTS} ambrosia-perftest ./run_small_PTI_and_shutdown.sh
	set +x
	;;
    TwoContainers)
	echo "Running PTI server/client in separate, communicating containers:"
	"$AMBROSIA_ROOT"/InternalImmortals/PerformanceTestInterruptible/run_two_docker_containers.sh
	;;
    *)
	echo "ERROR: invalid value of PTI_MODE=$PTI_MODE";
	echo " (expected 'OneContainer' or 'TwoContainers')";
	exit 1;
esac

echo "$0: Finished successfully."

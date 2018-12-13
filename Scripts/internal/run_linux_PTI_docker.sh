#!/bin/bash
set -euo pipefail

# --------------------------------------------------------------------------------
# Run a full Docker-based AMBROSIA test, PerformanceTestInterruptable
# --------------------------------------------------------------------------------
#
# ASSUMES: that Docker images ambrosia-dev, ambrosia-perftest are built and available.
#
# Additionally, this responds to the following environment variable:
#
#  * AZURE_STORAGE_CONN_STRING : required (get from Azure Portal)
#
#  * PTI_MOUNT_LOGS = "ExternalLogs" | "InternalLogs"
#                     (default Internal)
#  * PTI_MODE = "OneContainer" | "TwoContainers"
#      (controls whether to do an intra- or inter-container test)
#
# --------------------------------------------------------------------------------

echo "Running a single Docker container with PerformanceTestInterruptable."
echo "Using AZURE_STORAGE_CONN_STRING from your environment."

if ! [[ ${AZURE_STORAGE_CONN_STRING:+defined} ]]; then
    echo "ERROR: you must have AZURE_STORAGE_CONN_STRING set to call this script.";
    exit 1;
else
    # When running as well as building  we pass this through into the container:
    OPTS=" --env AZURE_STORAGE_CONN_STRING=${AZURE_STORAGE_CONN_STRING}"
fi

# Set the default value if unset:
if ! [[ ${PTI_MOUNT_LOGS:+defined} ]]; then
    PTI_MOUNT_LOGS="InternalLogs";
    echo " * defaulting to PTI_MOUNT_LOGS=$PTI_MOUNT_LOGS"
fi

if ! [[ ${PTI_MODE:+defined} ]]; then
    PTI_MODE="OneContainer"
    echo " * defaulting to PTI_MOUNT_LOGS=$PTI_MOUNT_LOGS"
fi

if ! [[ ${DOCKER:+defined} ]]; then
   DOCKER=docker
fi

# Optional: mount logs from *outside* the container:
case $PTI_MOUNT_LOGS in
    InternalLogs)
        ;;
    ExternalLogs)
        # Use a directory on the local machine.
        AMBROSIA_LOGDIR=`pwd`/logs
        mkdir -p "$AMBROSIA_LOGDIR"
        OPTS+=" -v ${AMBROSIA_LOGDIR}:/ambrosia_logs "
        ;;
    *)
        echo "ERROR: invalid value of PTI_MOUNT_LOGS=$PTI_MOUNT_LOGS";
        echo "  (expected 'InternalLogs' or 'ExternalLogs')";
        exit 1;
        ;;
esac

if ! [ ${INSTPREF:+defined} ]; then
    INSTPREF=""
fi

case $PTI_MODE in
  tmux)
     echo "Testing the interactive, tmux-based demo."
    `dirname $0`/run_in_tmux_then_exit.sh $INSTPREF
    ;;
    
  OneContainer)
    echo "Running PTI server/client both inside ONE container:"
    set -x

    $DOCKER run --rm ${OPTS} \
       ambrosia/ambrosia-perftest ./run_small_PTI_and_shutdown.sh $INSTPREF
    set +x
    ;;
    
  TwoContainers)
    echo "Running PTI server/client in separate, communicating containers:"
    "$AMBROSIA_ROOT"/InternalImmortals/PerformanceTestInterruptible/run_two_docker_containers.sh $INSTPREF
    ;;
    
  *)
    echo "ERROR: invalid value of PTI_MODE=$PTI_MODE";
    echo " (expected 'OneContainer' or 'TwoContainers')";
    exit 1;
esac

echo "$0: Finished successfully."

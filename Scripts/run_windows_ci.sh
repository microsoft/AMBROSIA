#!/bin/bash
set -euo pipefail

# ------------------------------------------------------------
# A script to build and test under Windows (Azure DevOps) CI.
# ------------------------------------------------------------

# Hack to deal with Azure Devops Pipelines:
if ! [[ -e ./build_docker_images.sh ]]; then
    # For MOST CI environments, running this script in-place, this
    # will get us to the top of the repo:
    cd `dirname $0`/../
fi
# Set up common definitions.
source Scripts/ci_common_defs.sh


echo "Executing a native Windows, non-Docker build."
export AMBROSIA_ROOT=`pwd`
./build_dotnetcore_bindist.sh

# APPLICATION 1: PTI
# ----------------------------------------
cd "$AMBROSIA_ROOT"/InternalImmortals/PerformanceTestInterruptible
./build_dotnetcore.sh

if [[ ${AZURE_STORAGE_CONN_STRING:+defined} ]]; then
    echo
    echo "All builds completed.  Attempt to run a test."
    ./run_small_PTI_and_shutdown.sh $INSTPREF || \
        echo "EXPECTED FAILURE - allowing local non-docker test to fail for PTI."
else
    echo "AZURE_STORAGE_CONN_STRING not defined, so not attempting PTI test."
fi

# Application 2: ...
# ----------------------------------------


# ----------------------------------------
cd "$AMBROSIA_ROOT"

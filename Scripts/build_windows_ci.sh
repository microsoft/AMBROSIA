#!/bin/bash
set -euo pipefail

# ------------------------------------------------------------
# A script to build under Windows (Azure DevOps) CI.
# Only builds the files needed for the CI
# ------------------------------------------------------------

echo "*********  Running run_windows_ci.sh ********************"
echo "Args: "$@
echo "*******************************************************************"

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
export PATH="$PATH:$AMBROSIA_ROOT/bin"
./build_ci_core.sh  

# Build PTI
# ----------------------------------------
cd "$AMBROSIA_ROOT"/InternalImmortals/PerformanceTestInterruptible
./build_pti_ci.sh

# ----------------------------------------
echo
echo "All builds completed."

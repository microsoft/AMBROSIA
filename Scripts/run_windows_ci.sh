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
export PATH="$PATH:$AMBROSIA_ROOT/bin"

./build_dotnetcore_bindist.sh

# Build Application 1: PTI
# ----------------------------------------
cd "$AMBROSIA_ROOT"/InternalImmortals/PerformanceTestInterruptible
./build_dotnetcore.sh

# Build Application 2: Hello World Sample
# ----------------------------------------
cd "$AMBROSIA_ROOT"/Samples/HelloWorld
./build_dotnetcore.sh || echo "EXPECTED FAILURE - problems with Hello World net461 for now"

# ----------------------------------------
echo
echo "All builds completed.  Proceeding to running system tests."
if ! [[ ${AZURE_STORAGE_CONN_STRING:+defined} ]]; then
    echo "AZURE_STORAGE_CONN_STRING not defined, so not attempting runnning system tests."
    exit 0
fi

# Test Application: PTI
# ----------------------------------------
cd "$AMBROSIA_ROOT"/InternalImmortals/PerformanceTestInterruptible
./run_small_PTI_and_shutdown.sh $INSTPREF

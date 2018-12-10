#!/bin/bash
set -xeuo pipefail

# A simple script to build and test under Windows (Azure DevOps) CI.

# Hack to deal with Azure Devops Pipelines:
if ! [[ -e ./build_dotnetcore_bindist.sh ]]; then
    # For MOST CI environments, running this script in-place, this
    # will get us to the top of the repo:
    cd `dirname $0`/../
fi
    
# Gather a bit of info about where we are:
uname -a
pwd -P

export AMBROSIA_ROOT=`pwd`
./build_dotnetcore_bindist.sh

# [2018.12.08] Unfinished: with SEPARATED dotnet publish output, I
# don't know how to link the binaries on Windows:
pushd "$AMBROSIA_ROOT"/InternalImmortals/PerformanceTestInterruptible
./build_dotnetcore.sh
popd

if [[ ${AZURE_STORAGE_CONN_STRING:+defined} ]]; then
    echo
    echo "All builds completed.  Attempt to run a test."
    ./run_small_PTI_and_shutdown.sh $INSTPREF || \
        echo "EXPECTED FAILURE - allowing local non-docker test to fail for PTI."
else
    echo "AZURE_STORAGE_CONN_STRING not defined, so not attempting PTI test."
fi

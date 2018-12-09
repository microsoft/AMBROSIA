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

# [2018.12.08] Disabling this for now:
# with SEPARATED dotnet publish output, I don't know how to link the binaries on Windows:
#
# pushd "$AMBROSIA_ROOT"/InternalImmortals/PerformanceTestInterruptible
# ./build_dotnetcore.sh
# popd

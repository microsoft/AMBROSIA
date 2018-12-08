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

./build_dotnetcore_bindist.sh

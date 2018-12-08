#!/bin/bash
set -xeuo pipefail

# A simple script to build and test under Linux CI.

# Hack to deal with Azure Devops Pipelines:
if ! [[ -e ./build_docker_images.sh ]]; then
    # For MOST CI environments, running this script in-place, this
    # will get us to the top of the repo:
    cd `dirname $0`/../
fi
    
# Gather a bit of info about where we are:
uname -a
pwd -P
cat /etc/issue || echo ok

# Build and run a small PerformanceTestInterruptable:
# ./build_docker_images.sh 

if [ $# -eq 0 ] || [ "$1" == "docker" ]; then
    echo "Executing containerized, Docker build."
    # [2018.12.07] Because of issue #5, we're just doing a basic build on Linux for now:
    DONT_BUILD_PTI=1 ./build_docker_images.sh
elif [ "$1" == "nodocker" ]; then
    echo "Executing native-Linux, non-Docker build."
    ./build_dotnetcore_bindist.sh
else
    echo "$0: ERROR: unexpected first argument: $1"
    exit 1
fi    

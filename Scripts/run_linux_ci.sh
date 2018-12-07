#!/bin/bash
set -xeu

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
./build_docker_images.sh run

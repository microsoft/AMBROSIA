#!/bin/bash
set -xeu

# A simple script to build and test under Linux CI.

# Switch to the top of the working copy:
cd `dirname $0`/../

# Gather a bit of info about where we are:
uname -a
pwd -P
cat /etc/issue || echo ok

# Build and run a small PerformanceTestInterruptable:
./build_docker_images.sh run

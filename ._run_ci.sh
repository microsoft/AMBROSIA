#!/bin/bash
set -xeu

# A simple script to build and test under Linux CI.

uname -a
pwd -P
cat /etc/issue || echo ok

./build_docker_images.sh run

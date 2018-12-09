#!/bin/bash
set -xeuo pipefail

# ------------------------------------------------------------
# A script to build and test under Linux CI.
#
# This mostly DISPATCHES to other scripts.
# ------------------------------------------------------------

# Hack to deal with Azure Devops Pipelines:
if ! [[ -e ./build_docker_images.sh ]]; then
    # For MOST CI environments, running this script in-place, this
    # will get us to the top of the repo:
    cd `dirname $0`/../
fi

export AMBROSIA_ROOT=`pwd`

# Gather a bit of info about where we are:
uname -a
pwd -P
cat /etc/issue || echo ok

# Build and run a small PerformanceTestInterruptable:
# ./build_docker_images.sh 

if [ $# -eq 0 ]; then
    mode=nodocker
else
    mode=$1
fi


case $mode in
  docker)
    
      echo "Executing containerized, Docker build."      
      # When we are trying to run tests we don't really want the tarball:
      DONT_BUILD_TARBALL=1 ./build_docker_images.sh

      ./Scripts/internal/run_linux_PTI_docker.sh
      ;;
  
  nodocker)
        
      echo "Executing raw-Linux, non-Docker build."
      cd "$AMBROSIA_ROOT"
      ./build_dotnetcore_bindist.sh
      
      cd "$AMBROSIA_ROOT"/InternalImmortals/PerformanceTestInterruptible
      ./build_dotnetcore.sh
      cd "$AMBROSIA_ROOT"
      ;;

  *)
      echo "$0: ERROR: unknown first argument: $mode"
      exit 1
      ;;

esac


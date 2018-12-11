#!/bin/bash
set -euo pipefail

# ------------------------------------------------------------
# A script to build and test under Linux CI.
#  (Also currently used for Mac OS.)
#
# This mostly DISPATCHES to other scripts.
# ------------------------------------------------------------

# Hack to deal with Azure Devops Pipelines:
if ! [[ -e ./build_docker_images.sh ]]; then
    # For MOST CI environments, running this script in-place, this
    # will get us to the top of the repo:
    cd `dirname $0`/../
fi
# Set up common definitions.
source Scripts/ci_common_defs.sh

function check_az_storage_and_bail() {
    echo
    echo "All builds completed.  Proceeding to running system tests."
    if ! [[ ${AZURE_STORAGE_CONN_STRING:+defined} ]]; then
        echo "AZURE_STORAGE_CONN_STRING not defined, so not attempting runnning system tests."
        exit 0
    fi
}

case $mode in
  docker)
    
      echo "Executing containerized, Docker build."      
      # When we are trying to run tests we don't really want the tarball:
      DONT_BUILD_TARBALL=1 ./build_docker_images.sh

      # APPLICATION 1: PTI
      # ----------------------------------------
      check_az_storage_and_bail
      ./Scripts/internal/run_linux_PTI_docker.sh
      
      # Application 2: ...
      # ----------------------------------------          

      ;;
      
  nodocker)
        
      echo "Executing raw-Linux, non-Docker build."
      cd "$AMBROSIA_ROOT"
      ./build_dotnetcore_bindist.sh

      # APPLICATION 1: PTI
      # ----------------------------------------
      cd "$AMBROSIA_ROOT"/InternalImmortals/PerformanceTestInterruptible
      ./build_dotnetcore.sh
      check_az_storage_and_bail
      ./run_small_PTI_and_shutdown.sh $INSTPREF
      
      # Application 2: ...
      # ----------------------------------------


      ;;

  *)
      echo "$0: ERROR: unknown first argument: $mode"
      exit 1
      ;;

esac


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

      check_az_storage_and_bail
      
      # APPLICATION 1: PTI
      # ----------------------------------------
      ./Scripts/internal/run_linux_PTI_docker.sh
      
      # Application 2: NativeService
      # ----------------------------------------
      # docker --env AZURE_STORAGE_CONN_STRING="${AZURE_STORAGE_CONN_STRING}" --rm \
      #    ambrosia-nativeapp ./run_test_in_one_machine.sh
      
      ;;
      
  nodocker)
        
      echo "Executing raw-Linux, non-Docker build."
      cd "$AMBROSIA_ROOT"
      ./build_dotnetcore_bindist.sh

      # Build Application 1: PTI
      # ----------------------------------------
      cd "$AMBROSIA_ROOT"/InternalImmortals/PerformanceTestInterruptible
      ./build_dotnetcore.sh

      # Build Application 2: ...
      # ----------------------------------------

      check_az_storage_and_bail

      # Test Application 1: PTI
      # ----------------------------------------
      cd "$AMBROSIA_ROOT"/InternalImmortals/PerformanceTestInterruptible
      ./run_small_PTI_and_shutdown.sh $INSTPREF      
      
      # Test Application 2: ...
      # ----------------------------------------
      cd "$AMBROSIA_ROOT"/Clients/C
      ./run_hello_world.sh || echo "Allowed failure for now."

      ;;

  *)
      echo "$0: ERROR: unknown first argument: $mode"
      exit 1
      ;;

esac


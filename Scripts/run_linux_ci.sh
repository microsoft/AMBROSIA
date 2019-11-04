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
echo "*********  Setup Common Definitions ********************"
source Scripts/ci_common_defs.sh

function check_az_storage_and_bail() {
    echo
    echo "All builds completed.  Proceeding to running system tests."
    if ! [[ ${AZURE_STORAGE_CONN_STRING:+defined} ]]; then
        echo "AZURE_STORAGE_CONN_STRING not defined, so not attempting runnning system tests."
        exit 0
    fi
}

FMWK="${AMBROSIA_DOTNET_FRAMEWORK:-netcoreapp2.2}"
CONF="${AMBROSIA_DOTNET_CONF:-Release}"

case $mode in
  docker)
    
      echo "Executing containerized, Docker build."      
      # When we are trying to run tests we don't really want the tarball:
      DONT_BUILD_TARBALL=1 ./build_docker_images.sh

      check_az_storage_and_bail
      
      # Application 1: PTI
      # ----------------------------------------
	  echo "*********  PTI ********************"
      ./Scripts/internal/run_linux_PTI_docker.sh

      # Application 2: Hello World Sample
      # ----------------------------------------
	  echo "*********  Hello World Sample ********************"
      # docker --rm ambrosia/ambrosia-dev ./Samples/HelloWorld/build_dotnetcore.sh
      cd "$AMBROSIA_ROOT"/Samples/HelloWorld
      docker build -t ambrosia-hello .
      if [ ${AZURE_STORAGE_CONN_STRING:+defined} ]; then
	        # Expects stdin, so we pipe 'yes' to it:
	        docker run --rm --env "AZURE_STORAGE_CONN_STRING=$AZURE_STORAGE_CONN_STRING" \
		             ambrosia-hello bash -c 'yes|./run_helloworld_both.sh' \
		          || echo "Allowed failure for now."
      fi
      
      # Application 3: NativeService
      # ----------------------------------------
      # docker --env AZURE_STORAGE_CONN_STRING="${AZURE_STORAGE_CONN_STRING}" --rm \
      #    ambrosia-nativeapp ./run_test_in_one_machine.sh

      echo "Examine Docker image sizes:"
      docker images 
      
      ;;
      
  nodocker)
        
      echo "Executing raw-Linux, non-Docker build."
      export PATH="$PATH:$AMBROSIA_ROOT/bin"
	  
	  echo "********* Build DotNet Core ********************"
      cd "$AMBROSIA_ROOT"
      ./build_dotnetcore_bindist.sh

      # Build Application: PTI
      # ----------------------------------------
	   echo "********* PTI ********************"
       cd "$AMBROSIA_ROOT"/InternalImmortals/PerformanceTestInterruptible
       ./build_dotnetcore.sh

      # Build Application: Hello World Sample
      # ----------------------------------------
  	  echo "*********  Hello World Sample ********************"
	  cd "$AMBROSIA_ROOT"/Samples/HelloWorld
      #echo "HelloWorld: First make sure a straight-to-the-solution build works:"
      #dotnet publish -c $CONF -f $FMWK HelloWorld.sln
      echo "HelloWorld: Then make sure it builds from scratch:"
      rm -rf GeneratedSourceFiles
      git clean -nxd .
      ./build_dotnetcore.sh
      
      # ----------------------------------------
      check_az_storage_and_bail
      
      # Test Application: Native client hello
      # ----------------------------------------
   	  echo "*********  Test App: Hello World ********************"

      cd "$AMBROSIA_ROOT"/Clients/C
      ./run_hello_world.sh || echo "Allowed failure for now."

      # Test Application: Hello World Sample
      # ----------------------------------------
      # cd "$AMBROSIA_ROOT"/Samples/HelloWorld
      # ./build_dotnetcore.sh
      # # Expects stdin, so we pipe 'yes' to it:
      # yes | ./run_helloworld_both.sh || echo "Allowed failure for now."
      
  
      # Test Application: PTI (last because it's slow)
      # ----------------------------------------------
	  echo "********* Test App PTI ********************"
	  sleep 10
	  cd "$AMBROSIA_ROOT"/InternalImmortals/PerformanceTestInterruptible
      ./run_small_PTI_and_shutdown.sh $INSTPREF      
	  echo "*****************************"
	  sleep 10
    	  
      ;;
	 	  
	  

  *)
      echo "$0: ERROR: unknown first argument: $mode"
      exit 1
      ;;

esac


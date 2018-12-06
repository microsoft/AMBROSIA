#!/bin/bash
set -euo pipefail

#############################################################################
## This script builds a service container.  Note that a service container MAY
## have different entrypoints and underpin multiple service instances with
## different roles.
## 
## Args:
##     <service source path> -- Path to service src dir containing Dockerfile
## 
#############################################################################

if [ $# -ne 1 ]
  then
    echo "usage: Build-KS.sh <service source path>"
	exit 1
fi

SERVICE_SRC_PATH=$1
source `dirname $0`/Defs/Common-Defs.sh
echo "-----------Begin Build-and-Push-----------"

############################################
## Docker container registry login
############################################

## TODO: Docker container registry details could be stored in ENV
## variables and managed as secrets.
#
# For now, paste your Docker Credentials in this file:
if [ ! -e `dirname $0`/Defs/AmbrosiaAKSConf.sh ]; then
    echo "ERROR: the file "`dirname $0`"/Defs/AmbrosiaAKSConf.sh does not exist!"
    echo "Please follow the instructions in AmbrosiaAKSConf.sh.template to populate this file."
    exit 1;
fi
source `dirname $0`/Defs/AmbrosiaAKSConf.sh
source `dirname $0`/Defs/Set-Docker-Vars.sh

echo "Connecting to the Private Docker Registry...."
set -x
$DOCKER login $DockerPrivateRegistry_URL -u $DockerPrivateRegistry_Login -p $DockerPrivateRegistry_Pwd
set +x

############################################
## Build the service container
############################################
## This script expects the Dockerfile for the service to exist in its directory.

# But first, we depend on the ambrosia-dev base image:
# Go and build the base images only if they are not found:
if [ "$($DOCKER images -q ambrosia-dev)" == "" ]; then
    echo "Could not find 'ambrosia-dev' image, attempting to build it."
    # Top of Ambrosia source working dir:
    set -x
    pushd `dirname $0`/../
    ./build_docker_images.sh
    popd
    set +x
fi

echo "Building the service Docker container..."
pushd $SERVICE_SRC_PATH
set -x
$DOCKER build -t $DockerPrivateRegistry_URL/$AMBROSIA_CONTAINER_NAME .
$DOCKER tag $DockerPrivateRegistry_URL/$AMBROSIA_CONTAINER_NAME $AMBROSIA_CONTAINER_NAME
set +x
popd

############################################
## Push the container to the Docker Registry
############################################
echo "Pushing the service Docker container..."
set -x
time $DOCKER push $DockerPrivateRegistry_URL/$AMBROSIA_CONTAINER_NAME 
$AZ acr repository list --name $ACR_NAME
$AZ acr repository show-tags --name $ACR_NAME --repository $AMBROSIA_CONTAINER_NAME
set +x

echo "-----------Build-and-Push Finished-----------"
echo

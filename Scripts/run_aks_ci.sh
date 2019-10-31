#!/bin/bash
set -euo pipefail

# This script runs an end-to-end Azure/Kubernetes test.

echo "Running in directory: "`pwd`
cd ../Samples/AKS-scripts

echo "Filling settings into template (Defs/AmbrosiaAKSConf.sh)..."
cp Defs/AmbrosiaAKSConf.sh.template Defs/AmbrosiaAKSConf.sh
sed -i 's|xyz|aksambrosiaci|'       Defs/AmbrosiaAKSConf.sh
sed -i "s|PASTE_SUBSCRIPTION_ID_HERE|$AZURE_SUBSCRIPTION|" Defs/AmbrosiaAKSConf.sh


# TODO: Figure out how to overcome auth problems and push to private
# Azure container registry OR authenticate and push to public
# dockerhub.

# echo
# echo "Building Docker containers"
# DONT_BUILD_NATIVE_IMAGE=1 DONT_BUILD_TARBALL=1 \
#   ../../build_docker_images.sh
# docker tag ambrosia/ambrosia-perftest ambrosia/ambrosia-ci-test
# docker push ambrosia/ambrosia-ci-test
# export PUBLIC_CONTAINER_NAME=ambrosia/ambrosia-ci-test

# INCOMPLETE WORKAROUND:
#
# In the meantiime we just test the existing public images.  These
# will periodically update (automated build), but it is not
# *synchronized* with this CI run, so there's version skew.
export PUBLIC_CONTAINER_NAME=ambrosia/ambrosia-perftest

echo
echo "Launching end-to-end test..." 
NUM_ROUNDS=2 ./run-end-to-end-perftest-example.sh

#for ((i=0; i<10; i++)); do
#    sleep 2
#    kubectl get pods
#done

#kubectl describe pods

#POD=`kubectl get pods | grep Running | grep perftestclient | head -n1 | awk '{ print $1 }'`

#if [ "$POD" == ""]; then
#    echo "ERROR: could not find running client pod."
#    exit 1
#fi

#kubectl logs -f "$POD" \
  || echo "Ok if this exits with error for now."

echo "Cleaning up..."
# Option 1:
./Clean-AKS.sh all

# Leave the passive resources, but delete the active pods:
#kubectl delete pods,deployments --all



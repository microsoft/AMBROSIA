#!/bin/bash
set -euo pipefail

MINIKUBE=1

if [[ ! -v AZURE_STORAGE_CONNECTION_STRING ]]; then    
    echo "$0: AZURE_STORAGE_CONNECTION_STRING not set."
    exit 1
fi

if [ -v MINIKUBE ]; then
    KUBE=kubectl
    export DOCKER=docker
    set -x
    minikube status
    set +x
else
    KUBE=microk8s.kubectl
    export DOCKER=microk8s.docker
fi

# Go and build the base images only if they are not found:
if [ "$($DOCKER images -q ambrosia-dev)" == "" ]; then
    echo "Could not find 'ambrosia-dev' image, attempting to build it."
    # Top of Ambrosia source working dir:
    cd `dirname $0`/../../
    BUILD_DEV_IMAGE_ONLY=1 ./build_docker_images.sh
    cd `dirname $0`
fi

set -x
$KUBE get nodes
$KUBE get pods

cp local-kube-ambrosia-demo.yml generated.yml
sed -i "s|#AZURECONNSTRING#|${AZURE_STORAGE_CONNECTION_STRING}|" generated.yml

$KUBE apply -f generated.yml
$KUBE get pods
set +x

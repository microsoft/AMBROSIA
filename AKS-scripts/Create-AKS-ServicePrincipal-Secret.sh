#!/bin/bash
set -euo pipefail

################################################################################
# This script can be used to create a service principle which can then be used
# to create an image pull secret.
#
# Note: This is an alternative to giving direct access to ACS using the
# "Grant-AKS-acess-ACR.sh" script.
################################################################################

#Please see https://docs.microsoft.com/en-us/azure/container-registry/container-registry-auth-aks for more documentation

echo "-----------Begin Create-AKS-ServicePrincipal-Secret-----------"
source `dirname $0`/Defs/Common-Defs.sh

# Create a 'Reader' role assignment with a scope of the ACR resource.
# Idempotence: retrieve the password if it already exists, otherwise create:
if ! $AZ ad sp show --id http://$SERVICE_PRINCIPAL_NAME >- ;
then
    echo "Creating 'Reader' role and password."
    ACR_REGISTRY_ID=$($AZ acr show --name $ACR_NAME --query id --output tsv)
    set -x
    $AZ ad sp create-for-rbac --name http://$SERVICE_PRINCIPAL_NAME --role Reader --scopes $ACR_REGISTRY_ID
    set +x
else
    echo "Service principal exists, ASSUMING it's up-to-date (manually clean w Clean-AKS.sh)"
fi
source `dirname $0`/Defs/Set-Docker-Vars.sh

# Get the service principal client id.
set -x
CLIENT_ID=$($AZ ad sp show --id http://$SERVICE_PRINCIPAL_NAME --query appId --output tsv)
set +x
echo "Service principal ID: $CLIENT_ID"
echo "Service principal password: $DockerPrivateRegistry_Pwd"

# Create Kubernetes secret
if $KUBE get secret $ACR_SECRET_NAME >- ;
then
    echo "Secret $ACR_SECRET_NAME exists, ASSUMING up-to-date (manually clean w/ Clean-AKS.sh)."
else
    echo "Creating secret for Kube to access the private container registry:"
    set -x
    time $KUBE create secret docker-registry $ACR_SECRET_NAME --docker-server $DockerPrivateRegistry_URL --docker-username $CLIENT_ID --docker-password $DockerPrivateRegistry_Pwd --docker-email $DOCKER_EMAIL
    set +x
fi

echo "-----------Create-AKS-ServicePrincipal-Secret Finished-----------"
echo 

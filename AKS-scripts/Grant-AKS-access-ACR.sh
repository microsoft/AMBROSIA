#!/bin/bash
set -euo pipefail

################################################################################
## This script might need to be executed to give AKS cluster access to
## ACS. This access is needed to pull images from AKS.
##
## Please see
##  https://docs.microsoft.com/en-us/azure/container-registry/container-registry-auth-aks
## for more documentation
################################################################################

if [ $# -ne 0 ]
then
    echo "usage: Grant-AKS-acess-ACR.sh"
    echo "Expects no arguments.  Uses Common-Defs.sh and AmbrosiaAKSConf.sh"
    exit 1
fi

echo "-----------Begin Grant-AKS-access-ACR-----------"
source `dirname $0`/Defs/Common-Defs.sh
## Assumes that both AKS and ACR are in the same resource group

set -x
# Get the id of the service principal configured for AKS
CLIENT_ID=$($AZ aks show --resource-group $AZURE_RESOURCE_GROUP --name $AZURE_KUBERNETES_CLUSTER --query "servicePrincipalProfile.clientId" --output tsv)

# Get the ACR registry resource id
ACR_ID=$($AZ acr show --name $ACR_NAME --resource-group $AZURE_RESOURCE_GROUP --query "id" --output tsv)

# Set it to empty string if there's an error:
EXISTING_ROLES=""
EXISTING_ROLES=$($AZ role assignment list --role Reader --scope $ACR_ID --query '[0].id')
set +x
echo

# Create role assignment
if [ "$EXISTING_ROLES" == "" ]; then
    echo "Creating new role assignment."
    set -x
    $AZ role assignment create --assignee $CLIENT_ID --role Reader --scope $ACR_ID 
    set +x
else echo "Role assignments exists, ASSUMING it is correct."
fi
echo "-----------End Grant-AKS-access-ACR-----------"
echo

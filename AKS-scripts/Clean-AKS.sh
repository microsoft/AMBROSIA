#!/bin/bash
# set -euo pipefail

# Optional helper script: delete resources from Azure/AKS to clean up
# when finished or restart fresh.

ECHO_CORE_DEFS=1
source `dirname $0`/Defs/Common-Defs.sh

function usage() {
    echo
    echo "Usage: $0 <all|most|auth>"
    echo " * all: delete the entire resource group ($AZURE_RESOURCE_GROUP)"
    echo " * most: delete all resources created inside the group EXCEPT the kubernetes cluster,"
    echo "         furthermore, delete the pods inside it."
    echo " * auth: delete secrets and authentication info, forcing them to be recreated."
    echo
    exit 1
}

function clean_auth()
{
    # Private Azure Container Registry access:
    # ----------------------------------------
    if EXISTING=$($AZ ad sp show --id http://$SERVICE_PRINCIPAL_NAME --query appId --output tsv) ;
    then
	echo "Deleting existing service principal: $EXISTING"
	set -x
	# To avoid problems as described here:
	#   https://github.com/Azure/azure-powershell/issues/4919
	# namely this cryptic error:
	#   "Another object with the same value for property identifierUris already exists."
	# we must delete the app as well:
	APP_ID=$($AZ ad app list --identifier-uri http://$SERVICE_PRINCIPAL_NAME --query "[0].appId" --output tsv)
	$AZ ad app delete --id $APP_ID
	# $AZ ad sp delete --id http://$SERVICE_PRINCIPAL_NAME
	set +x
    else
	echo "Service principal, $SERVICE_PRINCIPAL_NAME, not found.  Not deleting."	
    fi
    if $KUBE get secret $ACR_SECRET_NAME >- ;
    then
	echo "Secret $ACR_SECRET_NAME exists, deleting:"
	set -x
	$KUBE delete secret $ACR_SECRET_NAME
	set +x
    fi

    # File share access secret (logs) 
    # ----------------------------------------
    if $KUBE get secret $FILESHARE_SECRET_NAME 2>-;
    then
	echo "Secret already exists, deleting:"
	set -x
	$KUBE delete secret $FILESHARE_SECRET_NAME
	set +x
    fi
}

function clean_most()
{
    echo "Deleting main Azure resources (except AKS cluster):"
    set -x
#    $AZ storage share delete --name $FILESHARE_NAME 
    $AZ storage account delete --name $AZURE_STORAGE_NAME -g $AZURE_RESOURCE_GROUP
    $AZ acr delete --name "$ACR_NAME" --resource-group "$AZURE_RESOURCE_GROUP"
    set +x
    
    echo "Do not destroy the (slow to create) Kube cluster, but clear its pods:"
    set -x
    $AZ aks get-credentials --resource-group=$AZURE_RESOURCE_GROUP --name=$AZURE_KUBERNETES_CLUSTER
    $KUBE config current-context
    $KUBE delete pods,deployments --all
    set +x
}

if [ $# -ne 1 ]; then usage; fi
MODE=$1
case $MODE in
    all)
	$AZ group delete --name "$AZURE_RESOURCE_GROUP" ;;
    most)
	clean_auth;  clean_most ;;
    auth)
	clean_auth ;;
    *)
	echo "ERROR: unrecognized mode argument."
	usage ;; 
esac	


# Blast away the secrets without deleting everything in the resource group.

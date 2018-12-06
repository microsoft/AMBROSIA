#!/bin/bash
set -euo pipefail

cd `dirname $0`
source Defs/Common-Defs.sh

echo "Logging into az CLI *if needed*..."
set -x
$AZ account list --output table || $AZ login

# Mutate the (stateful) az CLI to point to this subscription:
$AZ account set --subscription $AZURE_SUBSCRIPTION
# TODO: ^ can be eliminated by making sure all subsequent commands pass --subscription
set +x

echo
echo "--------Provision the resource group if it does not exist--------"
if [ `$AZ group exists --name $AZURE_RESOURCE_GROUP` == "false" ]; then
    set -x
    time $AZ group create --name $AZURE_RESOURCE_GROUP -l $AZURE_LOCATION ;
    set +x
else
    echo "Resource group already exists, not creating. (az group exists)"
fi

echo
echo "--------Provision the storage account if it does not exist--------"
if [ "" == "$($AZ storage account list --output table --subscription $AZURE_SUBSCRIPTION -g $AZURE_RESOURCE_GROUP)" ];
then
    echo 
    set -x
    time $AZ storage account create --name $AZURE_STORAGE_NAME -g $AZURE_RESOURCE_GROUP -l $AZURE_LOCATION
    set +x    
else
    echo "Storage account already exists, not creating. (az storage account list)"
fi

echo
echo "--------Now we're ready to retrieve the connection string--------"
source `dirname $0`/Defs/Set-Storage-Vars.sh

echo
echo "--------Provision the file share if it does not exist--------"
echo "This step is idempotent:"
set -x
# TODO: May want to delete it to make sure the logs start fresh:
# $AZ storage share delete --name $FILESHARE_NAME --account-name $ACR_NAME --account-key="$AZURE_STORAGE_KEY"
$AZ storage share create --name $FILESHARE_NAME --quota "80" --account-name $ACR_NAME --account-key="$AZURE_STORAGE_KEY"
set +x

echo
echo "---------Provision the Container registry if needed---------"
if [ "" == "$($AZ acr list --output table --subscription $AZURE_SUBSCRIPTION -g $AZURE_RESOURCE_GROUP)" ];
then
    set -x
    # TODO: remove need for admin access here:
    time $AZ acr create --name "$ACR_NAME" --resource-group "$AZURE_RESOURCE_GROUP" --sku Standard --admin-enabled true -l $AZURE_LOCATION
    set +x
else
    echo "Container registry already exists, not creating. (az acr list)"
fi
echo "Now log into the Azure Container Registry:"
set -x
$AZ acr login --name "$ACR_NAME"
set +x

echo
echo "--------Provision the Kubernetes Cluster if it's not already there--------"
if ! $AZ aks get-credentials --resource-group=$AZURE_RESOURCE_GROUP --name=$AZURE_KUBERNETES_CLUSTER 2>- ;
then
    echo "Kubernetes cluster not found, creating."
    set -x
    time $AZ aks create --resource-group $AZURE_RESOURCE_GROUP --name=$AZURE_KUBERNETES_CLUSTER --node-count 2 --generate-ssh-keys -l $AZURE_LOCATION
    $AZ aks get-credentials --resource-group=$AZURE_RESOURCE_GROUP --name=$AZURE_KUBERNETES_CLUSTER
    set +x
else
    echo "Kubernetes cluster already exists, not creating. (az aks get-credentials)"
fi
set -x
$KUBE config use-context $AZURE_KUBERNETES_CLUSTER
$KUBE config current-context
$KUBE get nodes
set +x

echo "-----------AKS Provisioning Finished-----------"
echo

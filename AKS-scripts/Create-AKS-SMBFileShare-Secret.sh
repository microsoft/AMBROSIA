#!/bin/bash
set -euo pipefail

# ASSUMES: that the storage account has already been provisioned.
# ASSUMES: that the caller sets AZURE_STORAGE_KEY

## Please see https://docs.microsoft.com/en-us/azure/aks/azure-files-volume for more documentation

# if [ $# -eq 0 ]
#   then
#     echo "usage: Create-AKS-Secrete-SMBFileShare.sh"
# 	exit 1
# fi

echo "-----------Begin Create-AKS-SMBFileShare-Secret-----------"
source `dirname $0`/Defs/Common-Defs.sh

if [[ ! -v AZURE_STORAGE_KEY ]]; then
    echo "$0: AZURE_STORAGE_KEY not set, retrieving:"
    source `dirname $0`/Defs/Set-Storage-Vars.sh
fi

if $KUBE get secret $FILESHARE_SECRET_NAME 2>-;
then
    echo "File share secret exists, ASSUMING it's up-to-date."
    echo " (Manually clean with 'Clean-AKS.sh auth'.)"
else
    echo "Creating secret for Kubernetes file share access:"
    set -x
    $KUBE create secret generic $FILESHARE_SECRET_NAME \
	  --from-literal=azurestorageaccountname=$AZURE_STORAGE_NAME \
	  --from-literal=azurestorageaccountkey=$AZURE_STORAGE_KEY
    set +x
fi

echo "-----------Create-AKS-SMBFileShare-Secret Finished-----------"
echo

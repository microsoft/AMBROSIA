#!/bin/bash
set -euo pipefail

cd `dirname $0`
HELLOTOP=`pwd`
cd ../../AKS-scripts

if [ ! -e Defs/AmbrosiaAKSConf.sh ]; then
    echo "You're not ready yet!  (Defs/AmbrosiaAKSConf.sh does not exist)"
    echo    
    echo "This script demonstrates the full process of provisioning and deploying AMBROSIA on K8s."
    echo "The only configuration needed is to fill out Defs/AmbrosiaAKSConf.sh.template"
    echo
    echo "Please follow the instructions in README.md and in that template file." 
    echo
    exit 1
fi

echo "$0: Provision and run an AMBROSIA app on Azure Kubernetes Service"
echo "Running with these user settings:"
( export ECHO_CORE_DEFS=1; source `dirname $0`/Defs/Common-Defs.sh)
echo

# This should perform IDEMPOTENT OPERATIONS
#------------------------------------------

# STEP 0: Create Azure resources.
./Provision-Resources.sh

# STEPs 1-3: Secrets and Authentication
./Grant-AKS-access-ACR.sh 
./Create-AKS-ServicePrincipal-Secret.sh # TODO: bypass if $servicePrincipalId/$servicePrincipalKey are set
./Create-AKS-SMBFileShare-Secret.sh 

# STEP 4: Building and pushing Docker.
./Build-AKS.sh  "."

# STEP 5: Deploy two pods.
echo "-----------Pre-deploy cleanup-----------"
source Defs/Common-Defs.sh
echo "These are the secrets Kubernetes will use to access files/containers:"
$KUBE get secrets
echo
echo "Deleting all pods in this test Kubernetes instance before redeploying"
$KUBE get pods
time $KUBE delete pods,deployments -l app=generated-helloclient
time $KUBE delete pods,deployments -l app=generated-helloserver
$KUBE get pods

export LOCALPORT1=2000
export LOCALPORT2=2001
./Deploy-AKS.sh helloserver 'runAmbrosiaService.sh dotnet Client1/Publish/Client1.dll helloclient helloserver'

export LOCALPORT1=1000
export LOCALPORT2=1001
./Deploy-AKS.sh helloclient 'runAmbrosiaService.sh dotnet Server/publish/Server.dll helloserver'
  
set +x
echo "-----------------------------------------------------------------------"
echo " ** End-to-end AKS / Kubernetes test script completed successfully. ** "
echo
source `dirname $0`/Defs/Common-Defs.sh
echo "P.S. If you want to delete the ENTIRE resource group, and thus everything touched by this script, run:"
echo "    az group delete --name $AZURE_RESOURCE_GROUP"
echo 

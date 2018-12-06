#!/bin/bash
set -euo pipefail

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
./Grant-AKS-access-ACR.sh  # TODO: Problems on Windows/Git-Bash [2018.12.03]
./Create-AKS-ServicePrincipal-Secret.sh
./Create-AKS-SMBFileShare-Secret.sh 

# STEP 4: Building and pushing Docker.
./Build-AKS.sh  "../InternalImmortals/PerformanceTestInterruptible/"

# STEP 5: Deploy two pods.
echo "-----------Pre-deploy cleanup-----------"
source Defs/Common-Defs.sh
echo "These are the secrets Kubernetes will use to access files/containers:"
$KUBE get secrets
echo
echo "Deleting all pods in this test Kubernetes instance before redeploying"
$KUBE get pods
time $KUBE delete pods,deployments -l app=generated-perftestclient
time $KUBE delete pods,deployments -l app=generated-perftestserver
$KUBE get pods

# Dummy version:
# ./Deploy-AKS.sh perftestserver 'while true; do sleep 1; echo server `date +%s`; done'
# ./Deploy-AKS.sh perftestclient 'while true; do sleep 1; echo client `date +%s`; done'

# [2018.12.03] If we run a DUMMY SERVICE here, the Coordinators do get to a "Ready" state.
./Deploy-AKS.sh perftestserver \
   'runAmbrosiaService.sh Server --sp '$LOCALPORT1' --rp '$LOCALPORT2' -j perftestclient -s perftestserver -n 1 -c'
#   'runAmbrosiaService.sh sleep 99999999'
#   'runAmbrosiaService.sh yes'

./Deploy-AKS.sh perftestclient \
   'runAmbrosiaService.sh Job --sp '$LOCALPORT1' --rp '$LOCALPORT2' -j perftestclient -s perftestserver --mms 65536 -n 13 -c'
#   'runAmbrosiaService.sh sleep 99999999'

set +x
echo "-----------------------------------------------------------------------"
echo " ** End-to-end AKS / Kubernetes test script completed successfully. ** "
echo
source `dirname $0`/Defs/Common-Defs.sh
echo "P.S. If you want to delete the ENTIRE resource group, and thus everything touched by this script, run:"
echo "    az group delete --name $AZURE_RESOURCE_GROUP"
echo 

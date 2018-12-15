#!/bin/bash
set -euo pipefail

# This example uses PerformanceTestInterruptable.
# See also the HelloWorld directory for a simpler example.

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

while [ $# -ge 1 ]; do
    case $1 in
        --deploy-only) DEPLOY_ONLY=1; shift ;;
        *)
            echo "Unrecognized command line argument: $1"
            exit 1 ;;
    esac
done

echo "$0: Provision and run an AMBROSIA app on Azure Kubernetes Service"
echo "Running with these user settings:"
( export ECHO_CORE_DEFS=1; source `dirname $0`/Defs/Common-Defs.sh)
echo

source Defs/Common-Defs.sh # For PUBLIC_CONTAINER_NAME
PUBLIC_CONTAINER_NAME=${PUBLIC_CONTAINER_NAME:-"ambrosia/ambrosia-perftest"}
export PUBLIC_CONTAINER_NAME

# Leave it running for a long time by default:
NUM_ROUNDS=${NUM_ROUNDS:-20}


# This should perform IDEMPOTENT OPERATIONS
#------------------------------------------

if ! [ ${DEPLOY_ONLY:+defined} ]; then

    # STEP 0: Create Azure resources.
    ./Provision-Resources.sh

    # STEPs 1-3: Secrets and Authentication
    if [ ${PUBLIC_CONTAINER_NAME:+defined} ]; then
        echo "---------PUBLIC_CONTAINER_NAME set, not creating AKS/ACR auth setup---------"
    else
        ./Grant-AKS-access-ACR.sh
        ./Create-AKS-ServicePrincipal-Secret.sh # TODO: bypass if $servicePrincipalId/$servicePrincipalKey are set
    fi
    ./Create-AKS-SMBFileShare-Secret.sh 

    # STEP 4: Building and pushing Docker.
    if [ ${PUBLIC_CONTAINER_NAME:+defined} ]; then
        echo "---------PUBLIC_CONTAINER_NAME set, NOT building Docker container locally---------"
    else
        ./Build-AKS.sh  "../../InternalImmortals/PerformanceTestInterruptible/"
    fi

fi
    
# STEP 5: Deploy two pods.
echo "-----------Pre-deploy cleanup-----------"
echo "These are the secrets Kubernetes will use to access files/containers:"
$KUBE get secrets
echo
echo "Deleting all pods in this test Kubernetes instance before redeploying"
$KUBE get pods
time $KUBE delete pods,deployments -l app=generated-perftestclient
time $KUBE delete pods,deployments -l app=generated-perftestserver
$KUBE get pods

./Deploy-AKS.sh perftestserver \
   'runAmbrosiaService.sh Server --sp '$LOCALPORT1' --rp '$LOCALPORT2' -j perftestclient -s perftestserver -n 1 -c'
./Deploy-AKS.sh perftestclient \
   'runAmbrosiaService.sh Job --sp '$LOCALPORT1' --rp '$LOCALPORT2' -j perftestclient -s perftestserver --mms 65536 -n $NUM_ROUNDS -c'

set +x
echo "-----------------------------------------------------------------------"
echo " ** End-to-end AKS / Kubernetes test script completed successfully. ** "
echo
source `dirname $0`/Defs/Common-Defs.sh
echo "P.S. If you want to delete the ENTIRE resource group, and thus everything touched by this script, run:"
echo "    az group delete --name $AZURE_RESOURCE_GROUP"
echo 

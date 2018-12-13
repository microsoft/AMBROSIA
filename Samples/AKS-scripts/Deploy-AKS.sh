#!/bin/bash
set -euo pipefail

####################################################################
## This script deploys a service instance to Kubernetes (AKS)
## 
## Env vars: AZURE_STORAGE_CONNECTION_STRING must be set.
## Args:
##     <instance name> -- Instance Name (serves as the ImmortalCooridinator instance name)
##     <full command string> -- shell command to start the contianer
## 
####################################################################

if [ $# -eq 0 ]
  then
    echo "usage: Deploy-AKS.sh <instance name> [shell command for container start]*"
	exit 1
fi
AMBROSIA_INSTANCE_NAME=$1
shift

echo "-----------Begin Deploy-AKS ($AMBROSIA_INSTANCE_NAME)-----------"
source `dirname $0`/Defs/Common-Defs.sh
if ! [ ${AZURE_STORAGE_CONNECTION_STRING:+defined} ]; then
    echo "$0: AZURE_STORAGE_CONNECTION_STRING not set, retrieving:"
    source `dirname $0`/Defs/Set-Storage-Vars.sh    
fi

##########################################################################################
## Identifiers for the service and the docker container for this service
##########################################################################################

# FIXME: remove discrepancies:
YMLEXTENSION=".yml"

# Could append some randomness here to make this more unique:
UNIQUE_ID="generated-${AMBROSIA_INSTANCE_NAME}"
SERVICE_YML_FILE="${UNIQUE_ID}${YMLEXTENSION}"  # Need yml file to have unique deployment using CRA name


##########################################################################################
## Registering the instance by calling LocalAmbrosiaRuntime
##########################################################################################

echo "Registering Instance..."
set -x
function DOCKRUN() {
    # TODO: this should work with ambrosia/ambrosia instead of ambrosia/ambrosia-dev
    $DOCKER run --rm \
	   --env AZURE_STORAGE_CONN_STRING="$AZURE_STORAGE_CONN_STRING" \
	   ambrosia/ambrosia-dev $*
# FIXME: VSTS bug 127 : --env AZURE_STORAGE_CONNECTION_STRING="$AZURE_STORAGE_CONNECTION_STRING" \
}
time DOCKRUN Ambrosia RegisterInstance -i $AMBROSIA_INSTANCE_NAME --rp $LOCALPORT1 --sp $LOCALPORT2 -l "$AMBROSIA_LOGDIR" --lts 1024
set +x

##########################################################################################
## Generate K8s YAML deployment file for this service from the template
##########################################################################################

source `dirname $0`/Defs/Set-Docker-Vars.sh

echo "Generating K8s Deployment YAML from Template...."

cp -f ScriptBits/lartemplate.yml $SERVICE_YML_FILE
sed -i "s/#AMBROSIAINSTANCE#/${AMBROSIA_INSTANCE_NAME}/g"   $SERVICE_YML_FILE
sed -i "s/#SERVICEEXEFILE#/${AMBROSIA_SERVICE_NAME}/g"      $SERVICE_YML_FILE
sed -i "s/#DEPLOYMENTNAME#/${UNIQUE_ID}/g"                  $SERVICE_YML_FILE
sed -i "s/#FILESHARESECRETNAME#/${FILESHARE_SECRET_NAME}/g" $SERVICE_YML_FILE
sed -i "s/#FILESHARENAME#/${FILESHARE_NAME}/g"              $SERVICE_YML_FILE

sed -i "s/#COORDPORT#/${AMBROSIA_IMMORTALCOORDINATOR_PORT}/g" $SERVICE_YML_FILE
sed -i "s/#LOCALPORT1#/${LOCALPORT1}/g"                       $SERVICE_YML_FILE
sed -i "s/#LOCALPORT2#/${LOCALPORT2}/g"                       $SERVICE_YML_FILE

if [ ${PUBLIC_CONTAINER_NAME:+defined} ]; then
    sed -i "s|- name: #ACRSECRETNAME#|## None Needed|"             $SERVICE_YML_FILE    
    sed -i "s|#FULLCONTAINERNAME#|${PUBLIC_CONTAINER_NAME}|"                            $SERVICE_YML_FILE
else    
    sed -i "s|#ACRSECRETNAME#|${ACR_SECRET_NAME}|"             $SERVICE_YML_FILE    
    sed -i "s|#FULLCONTAINERNAME#|${DockerPrivateRegistry_URL}/${CONTAINER_NAME_ROOT}|" $SERVICE_YML_FILE
fi

# Use an alternate delimiter because the string contains forward slash:
sed -i "s|#AZURECONNSTRING#|${AZURE_STORAGE_CONNECTION_STRING}|" $SERVICE_YML_FILE

sed -i "s|#FULLCOMMANDSTRING#|$*|"        $SERVICE_YML_FILE

sed -i "s|#LOGDIR#|${AMBROSIA_LOGDIR}|"   $SERVICE_YML_FILE


##########################################################################################
## Deploy the template file to ACS/AKS
##########################################################################################

echo "Deploying to K8s..."
set -x 

# # <IMPERATIVE METHOD>
# # This step is NOT idempotent, and thus we ask for the users intervention:
# if $KUBE get -f $SERVICE_YML_FILE ;
# then
#     set +x
#     echo
#     echo "Kubernetes cluster already deployed.  Bring it down with:"
#     echo "  $KUBE delete -f $SERVICE_YML_FILE"
#     echo "And then try again."
#     exit 1;
# fi
# $KUBE create -f $SERVICE_YML_FILE
# # </IMPERATIVE METHOD>

# <DECLARATIVE METHOD>
$KUBE apply -f $SERVICE_YML_FILE
# </DECLARATIVE METHOD>

$KUBE get pods
set +x

echo "-----------Finished Deploy-AKS ($AMBROSIA_INSTANCE_NAME)-----------"
echo

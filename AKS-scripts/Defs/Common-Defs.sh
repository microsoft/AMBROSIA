# Sourced into other scripts
# Assumes a working directory of ../ relative to this script.

UNAME=`uname`
if [ "$UNAME" == Linux ] || [ "$UNAME" == Darwin ]; then
    DOCKER=docker    
    AZ=az
    KUBE=kubectl
    # Also: KUBE=microk8s.kubectl
else
    DOCKER=docker
    AZ=az.cmd
    KUBE=kubectl.exe
fi
    
if [ ! -e Defs/AmbrosiaAKSConf.sh ]; then
    echo "ERROR: Defs/AmbrosiaAKSConf.sh does not exist."
    echo "  Use Defs/AmbrosiaAKSConf.sh.template and populate this file with your info."
    exit 1
fi

if [[ ${ECHO_CORE_DEFS:+isdefined} ]];  # No -v in Bash 3.2 (Mac OS)
then 
    set -x
    source Defs/AmbrosiaAKSConf.sh
    set +x
else
    source Defs/AmbrosiaAKSConf.sh
fi
# ^ must set AMBROSIA_SERVICE_NAME, used below.

# Let our subprocesses use these vars:
export AMBROSIA_SERVICE_NAME
export AZURE_SUBSCRIPTION
export AZURE_RESOURCE_GROUP
export AZURE_KUBERNETES_CLUSTER
export ACR_NAME
export AZURE_STORAGE_NAME
export FILESHARE_NAME
export DOCKER_EMAIL
export SERVICE_PRINCIPAL_NAME

export ACR_SECRET_NAME
export FILESHARE_SECRET_NAME
export AMBROSIA_CONTAINER_NAME

# These could be configurable, but we're setting boring defaults here instead:
PREFIX="ambrosia-"
POSTFIX="-container"
AMBROSIA_CONTAINER_NAME="${PREFIX}${AMBROSIA_SERVICE_NAME}${POSTFIX}"

export AMBROSIA_LOGDIR="/ambrosia_logs"

# Configure the ports used for localhost app<->coordinator communication,
# as well as coordinator<->coordinator networking.
# ------------------------------
# export LOCALPORT1=1000
# export LOCALPORT2=1001
# export AMBROSIA_IMMORTALCOORDINATOR_PORT=1500

export LOCALPORT1=50001
export LOCALPORT2=50002
export AMBROSIA_IMMORTALCOORDINATOR_PORT=50500


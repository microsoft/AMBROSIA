#!/bin/bash
set -euo pipefail

# This script runs an end-to-end Azure/Kubernetes test.

echo "Running in directory: "`pwd`
if [[ -d `dirname $0`/AKS-scripts ]]; then
    cd `dirname $0`/AKS-scripts
elif [[ -d ../AKS-scripts ]]; then
    cd ../AKS-scripts
else 
    cd ./AKS-scripts
fi
echo "Filling settings into template (Defs/AmbrosiaAKSConf.sh)..."
cp Defs/AmbrosiaAKSConf.sh.template Defs/AmbrosiaAKSConf.sh
sed -i 's|xyz|aksambrosiaci|'   Defs/AmbrosiaAKSConf.sh
sed -i "s|PASTE_SUBSCRIPTION_ID_HERE|$AZURE_SUBSCRIPTION|" Defs/AmbrosiaAKSConf.sh

echo "Launching end-to-end test..."
./run-end-to-end-test.sh

echo "Cleaning up..."
# ./Clean-AKS.sh all

# Leave the passive resources, but delete the active pods:
kubectl delete pods,deployments --all

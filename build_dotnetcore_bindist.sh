#!/bin/bash
set -euo pipefail

# A simple script that builds the core Ambrosia binary distribution.

# This script is REDUNDANT with the steps in `Dockerfile`.  The
# Dockerfile *could* just call this script, but then it would have one
# big build step instead of granular ones.

UNAME=`uname`
if [ "$UNAME" == Linux ];
then PLAT=linux-x64
elif [ "$UNAME" == Darwin ];
then PLAT=osx-x64
else PLAT=win10-x64
fi

OUTDIR=`pwd`/bin
BUILDIT="dotnet publish -o $OUTDIR -c Release -f netcoreapp2.0 -r $PLAT"

echo "Cleaning publish directory:"
rm -rf $OUTDIR
mkdir -p $OUTDIR

echo "Building with command: $BUILDIT"

echo 
echo "Building AMBROSIA libraries/binaries"
echo "----------------------------------------"
$BUILDIT  Ambrosia/Ambrosia/Ambrosia.csproj
$BUILDIT ImmortalCoordinator/ImmortalCoordinator.csproj
$BUILDIT DevTools/UnsafeDeregisterInstance/UnsafeDeregisterInstance.csproj

echo 
echo "Building C# client tools"
echo "----------------------------------------"
$BUILDIT Clients/CSharp/AmbrosiaCS/AmbrosiaCS.csproj

echo 
echo "Copying deployment script."
cp -a Scripts/runAmbrosiaService.sh bin/

echo 
echo "Building Native-code client library"
echo "----------------------------------------"
if [ "$UNAME" == Linux ]; then
    cd Clients/C && make publish || \
      echo "WARNING: Successfully built a dotnet core distribution, but without the native code wrapper library."
elif [ "$UNAME" == Darwin ]; then
    echo "WARNING: not building native client for Mac OS."
else
    echo "WARNING: this script doesn't build the native client for Windows yet (FINISHME)"
fi

#!/bin/bash
set -euo pipefail

# This is for non-dockerized, scripted builds.

UNAME=`uname`
if [ "$UNAME" == Linux ];
then PLAT=linux-x64
elif [ "$UNAME" == Darwin ];
then PLAT=osx-x64
else PLAT=win10-x64
fi

# Set defaults if these environment vars aren't present:
FMWK="${AMBROSIA_DOTNET_FRAMEWORK:-netcoreapp2.2}"
CONF="${AMBROSIA_DOTNET_CONF:-Release}"

# Use a non-absolute directory here to prevent collisions:
BUILDIT="dotnet publish -o publish -c $CONF -f $FMWK -r $PLAT"

echo
echo "Build the projects that contain the RPC APIs"
echo "---------------------------------------------"
set -x
$BUILDIT "API/ServerAPI.csproj" 
$BUILDIT "ClientAPI/ClientAPI.csproj"
set +x

echo
echo "Copy published build-products into the CodeGenDependencies dir"
echo "--------------------------------------------------------------"

GENDEST="PTAmbrosiaGeneratedAPI"

echo
echo "Generate the assemblies (assumes the AmbrosiaCS executable was built):"
echo "----------------------------------------------------------------------"
set -x
# Alternatively: "dotnet ../../bin/AmbrosiaCS.dll"
../../bin/AmbrosiaCS CodeGen -a "API/publish/ServerAPI.dll" -a "ClientAPI/publish/ClientAPI.dll" -p "API/ServerAPI.csproj" -p "ClientAPI/ClientAPI.csproj" -o $GENDEST -f "netcoreapp2.2" 
set +x

echo
echo "Build the generated code:"
echo "-------------------------"
set -x
$BUILDIT GeneratedSourceFiles/${GENDEST}/latest/${GENDEST}.csproj
set +x

echo 
echo "Finally, build the Job/Server executables:"
echo "------------------------------------------"
set -x
$BUILDIT Client/Job.csproj
$BUILDIT Server/Server.csproj
set +x

echo "$0: Finished building."

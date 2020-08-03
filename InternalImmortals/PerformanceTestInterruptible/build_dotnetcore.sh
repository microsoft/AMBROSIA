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
FMWK="${AMBROSIA_DOTNET_FRAMEWORK:-netcoreapp3.1}"
CONF="${AMBROSIA_DOTNET_CONF:-Release}"

# Use a non-absolute directory here to prevent collisions:
BUILDIT_WITH_CONF_FMWK="dotnet publish -o publish -c $CONF -f $FMWK -r $PLAT"
BUILDIT="dotnet publish -o publish -r $PLAT"


echo
echo "Build the projects that contain the RPC APIs"
echo "---------------------------------------------"
set -x
$BUILDIT "API/ServerAPI.csproj" 
$BUILDIT "IJob/IJob.csproj"
set +x

echo
echo "Copy published build-products into the CodeGenDependencies dir"
echo "--------------------------------------------------------------"

GENDEST="PTIAmbrosiaGeneratedAPI"

echo
echo "Generate the assemblies (assumes the AmbrosiaCS executable was built):"
echo "----------------------------------------------------------------------"
set -x
# Alternatively: "dotnet ../../bin/AmbrosiaCS.dll"
../../bin/AmbrosiaCS CodeGen -a "publish/ServerAPI.dll" -a "publish/IJob.dll" -p "API/ServerAPI.csproj" -p "IJob/IJob.csproj" -o $GENDEST -f "netstandard2.0" -f "netcoreapp3.1" -f "net461"
set +x

echo
echo "Build the generated code:"
echo "-------------------------"
set -x
$BUILDIT_WITH_CONF_FMWK GeneratedSourceFiles/${GENDEST}/latest/${GENDEST}.csproj
set +x

echo 
echo "Finally, build the Job/Server executables:"
echo "------------------------------------------"
set -x
$BUILDIT_WITH_CONF_FMWK Client/Job.csproj
$BUILDIT_WITH_CONF_FMWK Server/Server.csproj
set +x

echo "$0: Finished building."

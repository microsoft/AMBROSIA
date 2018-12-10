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
FMWK="${AMBROSIA_DOTNET_FRAMEWORK:-netcoreapp2.0}"
CONF="${AMBROSIA_DOTNET_CONF:-Release}"

# Use a non-absolute directory here to prevent collisions:
OUTDIR=publish
BUILDIT="dotnet publish -o $OUTDIR -c $CONF -f $FMWK -r $PLAT"

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

DEST=CodeGenDependencies/$FMWK
rm -rf $DEST
mkdir -p $DEST
cp -af API/publish/*  $DEST/
cp -af IJob/publish/* $DEST/

# Extra codegen dependence, put its code generator's own .csproj file in the resulting deps dir:
# TODO/FIXME: this should be replaced by a template which is modified by the user:
cp -f "../../Clients/CSharp/AmbrosiaCS/AmbrosiaCS.csproj" $DEST/

# echo "Populated dependencies folder with:"
# find CodeGenDependencies/$FMWK || git clean -nxd CodeGenDependencies/$FMWK

echo
echo "Generate the assemblies (assumes the AmbrosiaCS executable was built):"
echo "----------------------------------------------------------------------"
set -x
# Alternatively: "dotnet ../../bin/AmbrosiaCS.dll"
../../bin/AmbrosiaCS CodeGen -a "$DEST/ServerAPI.dll" -a "$DEST/IJob.dll" -o "PTIAmbrosiaGeneratedAPINetCore" -f "$FMWK" -b="$DEST" 
set +x

echo
echo "Build the generated code:"
echo "-------------------------"
set -x
$BUILDIT GeneratedSourceFiles/PTIAmbrosiaGeneratedAPINetCore/latest/PTIAmbrosiaGeneratedAPINetCore.csproj
set +x

if [ "$FMWK" == "net46" ]; then
    echo "================================================================================"
    echo "WARNING: EXPECTED FAILURES on net46.  Allowing failures below this line."
    echo "================================================================================"
    set +e
fi

echo 
echo "Finally, build the Job/Server executables:"
echo "------------------------------------------"
set -x
$BUILDIT Client/Job.csproj
$BUILDIT Server/Server.csproj
set +x

echo "$0: Finished building."

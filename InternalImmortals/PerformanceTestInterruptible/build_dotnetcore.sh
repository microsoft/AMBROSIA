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

# FMWK="net46"
FMWK="netcoreapp2.0"
CONF="Debug"

# OUTDIR=`pwd`/bin
OUTDIR=publish
BUILDIT="dotnet publish -o $OUTDIR -c $CONF -f $FMWK -r $PLAT"

# Build the API projects
set -x
$BUILDIT "API/ServerAPI.csproj" 
$BUILDIT "IJob/IJob.csproj"
set +x

DEST=CodeGenDependencies/$FMWK
rm -rf $DEST
mkdir -p $DEST
cp -af API/publish/*  $DEST/
cp -af IJob/publish/* $DEST/

# Extra codegen dependence, put its code generator's own .csproj file in the resulting deps dir:
cp -f "../../Clients/CSharp/AmbrosiaCS/AmbrosiaCS.csproj" $DEST/

# echo "Populated dependencies folder with:"
# find CodeGenDependencies/$FMWK || git clean -nxd CodeGenDependencies/$FMWK

# Generate the assemblies, assumes an executable which is created by previous build:
set -x
# dotnet ../../bin/AmbrosiaCS.dll
../../bin/AmbrosiaCS CodeGen -a "$DEST/ServerAPI.dll" -a "$DEST/IJob.dll" -o "PTIAmbrosiaGeneratedAPINetCore" -f "$FMWK" -b="$DEST" \
    || echo "WARNING: CODEGEN ERROR is an EXPECTED FAILURE for now..."
set +x


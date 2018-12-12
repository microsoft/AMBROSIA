#!/bin/bash
set -euo pipefail

# Set defaults if these environment vars aren't present:
FMWK="${AMBROSIA_DOTNET_FRAMEWORK:-netcoreapp2.0}"
CONF="${AMBROSIA_DOTNET_CONF:-Release}"

DEST=CodeGenDependencies/$FMWK
rm -rf $DEST
mkdir -p $DEST

# Extra codegen dependence, put its code generator's own .csproj file in the resulting deps dir:
# TODO/FIXME: this should be replaced by a template which is modified by the user:
cp -f "../../Clients/CSharp/AmbrosiaCS/AmbrosiaCS.csproj" $DEST/

echo
echo "(STEP 1) Build enough so that we have compiled versions of our RPC interfaces"
BUILDIT="dotnet publish -o publish -c $CONF -f $FMWK "
set -x
$BUILDIT IClient1/IClient1.csproj
$BUILDIT IClient2/IClient2.csproj
$BUILDIT ServerAPI/IServer.csproj
set +x

echo
echo "(STEP 2) Use those DLL's to generate proxy code for RPC calls"

CG="AmbrosiaCS CodeGen -f $FMWK -b=$DEST"
set -x
$CG -o ServerInterfaces  -a ServerAPI/publish/IServer.dll 
$CG -o Client1Interfaces -a ServerAPI/publish/IServer.dll -a IClient1/publish/IClient1.dll 
$CG -o Client2Interfaces -a ServerAPI/publish/IServer.dll -a IClient2/publish/IClient2.dll
set +x

echo
echo "(STEP 3) Now the entire solution can be built."
$BUILDIT HelloWorld.sln
echo
echo "Hello world built."

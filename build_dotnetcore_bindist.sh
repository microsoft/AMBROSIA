#!/bin/bash
set -euo pipefail
cd `dirname $0`
# --------------------------------------------------------------------
# A simple script that builds the core Ambrosia binary distribution.

# This script is REDUNDANT with the steps in `Dockerfile`.  The
# Dockerfile *could* just call this script, but then it would have one
# big build step instead of granular ones.
# --------------------------------------------------------------------

# Should be "net46" or "netcoreapp2.0".  Set a default if not set:
export AMBROSIA_DOTNET_FRAMEWORK="${AMBROSIA_DOTNET_FRAMEWORK:-netcoreapp2.0}"
# Release or Debug:
export AMBROSIA_DOTNET_CONF="${AMBROSIA_DOTNET_CONF:-Release}"

UNAME=`uname`
if [ $AMBROSIA_DOTNET_FRAMEWORK == "net46" ]; then
    PLAT=x64
else
    # netcore gives an error on Ambrosia.csproj with x64...
    if [ "$UNAME" == Linux ];
    then PLAT=linux-x64
    elif [ "$UNAME" == Darwin ];
    then PLAT=osx-x64
    else PLAT=win10-x64
    fi
fi

OUTDIR=`pwd`/bin
# Shorthands:
FMWK="${AMBROSIA_DOTNET_FRAMEWORK}"
CONF="${AMBROSIA_DOTNET_CONF}"
function buildit() {
    dir=$1
    shift
    dotnet publish --self-contained -o $dir -c $CONF -f $FMWK -r $PLAT $*
}


echo "Cleaning publish directory."
rm -rf $OUTDIR
mkdir -p $OUTDIR

echo "Output of 'dotnet --info':"
dotnet --info

# echo "Building with command: $BUILDIT"

if [ $FMWK == net46 ]; then
    echo 
    echo "Building adv-file-ops C++ prereq"
    echo "------------------------------------"
    pushd Ambrosia/adv-file-ops
    set -x
    dotnet restore adv-file-ops.vcxproj
    
    msbuild='/c/Program Files (x86)/Microsoft Visual Studio/2017/Enterprise/MSBuild/15.0/Bin/MSBuild.exe'
    if ! [ -e "$msbuild" ]; then
	set +x
	echo "ERROR: currently the adv-file-ops.vcxproj C++ library needs Visual studio to build."
	echo "Could not find MSBuild.exe in the expected location:"
	echo "  $msbuild"
	exit 1
    fi
    # Only "Release" mode for adv-file-ops.
    # Ambrosia.csproj ASSUMES that this is in the Release dir.
    "$msbuild" "-t:Build" "-p:Configuration=Release" "-p:Platform=$PLAT" "adv-file-ops.vcxproj"
    set +x
    popd
fi

echo 
echo "Building AMBROSIA libraries/binaries"
echo "------------------------------------"
set -x
buildit $OUTDIR/runtime Ambrosia/Ambrosia/Ambrosia.csproj
buildit $OUTDIR/coord ImmortalCoordinator/ImmortalCoordinator.csproj
buildit $OUTDIR/unsafedereg DevTools/UnsafeDeregisterInstance/UnsafeDeregisterInstance.csproj
pushd $OUTDIR
ln -s runtime/Ambrosia Ambrosia
ln -s coord/ImmortalCoordinator
ln -s unsafedereg/UnsafeDeregisterInstance
popd
set +x

echo 
echo "Building C# client tools"
echo "----------------------------------------"
set -x
buildit $OUTDIR/codegen Clients/CSharp/AmbrosiaCS/AmbrosiaCS.csproj
pushd $OUTDIR
ln -s codegen/AmbrosiaCS 
popd
set +x

echo 
echo "Copying deployment script."
cp -a Scripts/runAmbrosiaService.sh bin/
# (cd bin; ln -s Ambrosia ambrosia || echo ok)

echo 
echo "Building Native-code client library"
echo "----------------------------------------"
if [ "$UNAME" == Linux ]; then
    pushd Clients/C
    make publish || \
        echo "WARNING: Successfully built a dotnet core distribution, but without the native code wrapper library."
    popd
elif [ "$UNAME" == Darwin ]; then
    echo "WARNING: not building native client for Mac OS."
else
    echo "WARNING: this script doesn't build the native client for Windows yet (FINISHME)"
fi

# echo 
# echo "Removing unnecessary execute permissions"
# echo "----------------------------------------"
# chmod -x ./bin/*.dll ./bin/*.so ./bin/*.dylib ./bin/*.a 2>/dev/null || echo 

echo 
echo "Deduplicating output produced by separate dotnet publish calls"
echo "--------------------------------------------------------------"
if [ ${OS:+defined} ] && [ "$OS" == "Windows_NT" ];
then ./Scripts/dedup_bindist.sh squish
else ./Scripts/dedup_bindist.sh symlink
fi

echo "$0 Finished"

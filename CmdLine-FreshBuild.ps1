# Using MSBuild, this approximates building CRA and Ambrosia in visual
# studio, but in a scriptable manner.
#
# Recommended: use git clean to ensure a completely clean build:
#    git clean -fxd; (cd deps/CRA/; git clean -fxd)

param (
    [switch]$noclean = $false,
    [switch]$debug = $false    
)
Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

if ($debug) { Set-PSDebug -Trace 1 }

echo "Building Ambrosia Project"

$AMB_PLAT="x64"
# To properly change Framework, various CSProj files must be changes as well:
$AMB_FMWK="net46"
#$AMB_FMWK="netcoreapp2.0"

$AMB_CONF="Release"
#$AMB_CONF="Debug"

# A combined series of sub-paths used to find binaries:
$env:AMBVARIANT="$AMB_PLAT\$AMB_CONF\$AMB_FMWK"

# TODO: Set this in a more principled way (vswhere?)
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\MSBuild.exe'
$codegen = "$PSScriptRoot\Ambrosia\bin\$env:AMBVARIANT\Ambrosia.exe"

if (Test-Path $msbuild -PathType Leaf) {
    Write-Host "Using hard-coded msbuild, located at $msbuild"
} else {
    Write-Host "WARNING: Using MSBuild.exe from path: "
    $msbuild = "MSBuild.exe"
    where $msbuild
}

# Explicitly catch the error code (ErrorActionPreference appears insufficient)
function MS-Build ( $sol, $target ) 
{
    # /p:TargetFrameworkVersion=v3.5    
    Write-Host "BUILD: calling msbuild: $msbuild $sol /t:$target /p:Configuration=$AMB_CONF /p:Platform=$AMB_PLAT"
    & $msbuild $sol "/t:$target" "/p:Configuration=$AMB_CONF" "/p:Platform=$AMB_PLAT" 
    if ( $LASTEXITCODE -ne 0 )
    {
	Throw "MSBuild child process exited with return code: $LASTEXITCODE"
    }
}


Write-Host "[Build1] Build the adv-file-ops C++ library."
pushd LocalAmbrosiaRuntime\adv-file-ops
if (! $noclean) { MS-Build "adv-file-ops.vcxproj" "Clean" }
MS-Build "adv-file-ops.vcxproj" "Restore"
MS-Build "adv-file-ops.vcxproj" "Build"
popd

Write-Host "[Build2] Build the code-generation tool itself."
pushd CodeGen
if (! $noclean) { MS-Build "CodeGen.csproj" "Clean" }
MS-Build "CodeGen.csproj" "Restore"
MS-Build "CodeGen.csproj" "Build"
popd

Write-Host "[Build3] Build the Ambrosia client-side library."
pushd Ambrosia
if (! $noclean) { MS-Build "Ambrosia.sln" "Clean" }
MS-Build "Ambrosia.sln" "Restore"
MS-Build "Ambrosia.sln" "Build"
popd


Write-Host "[Build4] PTI: start building"
pushd Examples\PerformanceTestInterruptible
# ----------------------------------------
# Warning: delicate phase ordering here.  Currently (but hopefully not
#   for long) PerformanceTestInterruptible is the catch-all project
#  for building everything, but the codegen step requires smaller
#  pieces of it be built before the rest.
# if (! $noclean) { MS-Build "PerformanceTest.sln" "Clean" } # Destroys Ambrosia.exe
MS-Build "PerformanceTest.sln" "Restore"

Write-Host "[Build5] PTI: Individual pieces required by the codegen step:"

# # MS-Build "Server\Server.csproj" "Build"
# # MS-Build "JobAPI\JobAPI.csproj" "Build"
MS-Build "IJob\IJob.csproj"     "Build"
MS-Build "API\ServerAPI.csproj" "Build"

Write-Host "[Build6] PTI: perform actual codegen."
# Run Tal's temporary work-around script."
# # Create directory for the codegen-tool output:
.\Generate-Assemblies.ps1
if ( $LASTEXITCODE -ne 0 ) { Throw "ERROR: Trouble with work-around script" }

Write-Host "[Build7] PTI: Finally, build everything else in the solution."
MS-Build "PerformanceTest.sln" "Build"
# ----------------------------------------
popd

Write-Host "[BuildDone] All steps completed successfully."

if ($debug) { Set-PSDebug -Trace 0 }

###########################################
#
# Script to build Ambrosia projects locally that are related to Nuget changes
# Handles the code generation and builds that get checked in so all done in a script
#
# Call: 
#  .\BuildAmbrosiaAfterNugetChange.ps1 
#
#  Note: Run this script AFTER running UpdateAmbrosiaForNugetRelease.ps1
#        This will generate all the necessary files and rebuild everything locally with the new nuget references
#
#  Note: The msbuild.exe for VS 2017 needs to be in the path. Most likely it is here (C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin)
#  or run from Command Prompt for VS 2017 - then need to: powershell.exe -noexit -file BuildAmbrosiaAfterNugetChange.ps1 
#
#
###########################################


##########################################################################
#
#  Build projects which also includes generating files
#
##########################################################################

$CurrentDir = $(get-location);
$BuildPlatform = "X64";
$BuildConfiguration = "Release";
$BuildVisualStudioVersion = "15.0";

Write-output "------------- Clean Everything first -------------" 
msbuild.exe $CurrentDir'\Clients\CSharp\AmbrosiaCS\AmbrosiaCS.sln' /t:"Clean" /p:platform=$BuildPlatform /p:configuration=$BuildConfiguration /p:VisualStudioVersion=$BuildVisualStudioVersion 
msbuild.exe $CurrentDir'\InternalImmortals\PerformanceTest\PerformanceTest.sln' /t:"Clean" /p:platform=$BuildPlatform /p:configuration=$BuildConfiguration /p:VisualStudioVersion=$BuildVisualStudioVersion 
msbuild.exe $CurrentDir'\InternalImmortals\PerformanceTestInterruptible\PerformanceTest.sln' /t:"Clean" /p:platform=$BuildPlatform /p:configuration=$BuildConfiguration /p:VisualStudioVersion=$BuildVisualStudioVersion 
msbuild.exe $CurrentDir'\Samples\HelloWorld\HelloWorld.sln' /t:"Clean" /p:platform=$BuildPlatform /p:configuration=$BuildConfiguration /p:VisualStudioVersion=$BuildVisualStudioVersion 
msbuild.exe $CurrentDir'\Samples\StreamingDemo\StreamingDemo.sln' /t:"Clean" /p:platform=$BuildPlatform /p:configuration=$BuildConfiguration /p:VisualStudioVersion=$BuildVisualStudioVersion 
Write-output "------------- Finish Cleaning everything -------------" 

Write-output "------------- Build AmbrosiaCS -------------" 
msbuild.exe $CurrentDir'\Clients\CSharp\AmbrosiaCS\AmbrosiaCS.sln' /nologo /nr:false /p:platform=$BuildPlatform /p:configuration=$BuildConfiguration /p:VisualStudioVersion=$BuildVisualStudioVersion 

Write-output "------------- Build PerformanceTest -------------"
msbuild.exe $CurrentDir'\InternalImmortals\PerformanceTest\API\ServerAPI.csproj' /nologo /nr:false /p:platform=$BuildPlatform /p:configuration=$BuildConfiguration /p:VisualStudioVersion=$BuildVisualStudioVersion 
msbuild.exe $CurrentDir'\InternalImmortals\PerformanceTest\ClientAPI\ClientAPI.csproj' /nologo /nr:false /p:platform=$BuildPlatform /p:configuration=$BuildConfiguration /p:VisualStudioVersion=$BuildVisualStudioVersion 

# Generate assemblies from PerformanceTest Dir
cd InternalImmortals\PerformanceTest
.\Generate-Assemblies.ps1
cd ..
cd ..
# Build entire solution -- TO DO - NOT WORKING -- Works if run in VS though
msbuild.exe $CurrentDir'\InternalImmortals\PerformanceTest\PerformanceTest.sln' /nologo /nr:false /p:platform=$BuildPlatform /p:configuration=$BuildConfiguration /p:VisualStudioVersion=$BuildVisualStudioVersion 


Write-output "------------- Build PerformanceTestInterruptible -------------"
msbuild.exe $CurrentDir'\InternalImmortals\PerformanceTestInterruptible\API\ServerAPI.csproj' /nologo /nr:false /p:platform=$BuildPlatform /p:configuration=$BuildConfiguration /p:VisualStudioVersion=$BuildVisualStudioVersion 
msbuild.exe $CurrentDir'\InternalImmortals\PerformanceTestInterruptible\IJob\IJob.csproj' /nologo /nr:false /p:platform=$BuildPlatform /p:configuration=$BuildConfiguration /p:VisualStudioVersion=$BuildVisualStudioVersion 
# Generate assemblies from PerformanceTest Dir
cd InternalImmortals\PerformanceTestInterruptible
.\Generate-Assemblies.ps1
cd ..
cd ..
# Build entire solution -- TO DO - NOT WORKING -- Works if run in VS though
msbuild.exe $CurrentDir'\InternalImmortals\PerformanceTestInterruptible\PerformanceTest.sln' /nologo /nr:false /p:platform=$BuildPlatform /p:configuration=$BuildConfiguration /p:VisualStudioVersion=$BuildVisualStudioVersion 


Write-output "------------- Build HelloWorld -------------"
# Build interfaces - 3 client / 1 server
msbuild.exe $CurrentDir'\Samples\HelloWorld\GeneratedSourceFiles\Client1Interfaces\latest\Client1Interfaces.csproj' /nologo /nr:false /p:platform=$BuildPlatform /p:configuration=$BuildConfiguration /p:VisualStudioVersion=$BuildVisualStudioVersion 
msbuild.exe $CurrentDir'\Samples\HelloWorld\GeneratedSourceFiles\Client2Interfaces\latest\Client2Interfaces.csproj' /nologo /nr:false /p:platform=$BuildPlatform /p:configuration=$BuildConfiguration /p:VisualStudioVersion=$BuildVisualStudioVersion 
msbuild.exe $CurrentDir'\Samples\HelloWorld\GeneratedSourceFiles\Client3Interfaces\latest\Client3Interfaces.csproj' /nologo /nr:false /p:platform=$BuildPlatform /p:configuration=$BuildConfiguration /p:VisualStudioVersion=$BuildVisualStudioVersion 
msbuild.exe $CurrentDir'\Samples\HelloWorld\GeneratedSourceFiles\ServerInterfaces\latest\ServerInterfaces.csproj' /nologo /nr:false /p:platform=$BuildPlatform /p:configuration=$BuildConfiguration /p:VisualStudioVersion=$BuildVisualStudioVersion 
# Build I* projects  - 3 client / 1 server
msbuild.exe $CurrentDir'\Samples\HelloWorld\IClient1\IClient1.csproj' /nologo /nr:false /p:platform=$BuildPlatform /p:configuration=$BuildConfiguration /p:VisualStudioVersion=$BuildVisualStudioVersion 
msbuild.exe $CurrentDir'\Samples\HelloWorld\IClient2\IClient2.csproj' /nologo /nr:false /p:platform=$BuildPlatform /p:configuration=$BuildConfiguration /p:VisualStudioVersion=$BuildVisualStudioVersion 
msbuild.exe $CurrentDir'\Samples\HelloWorld\IClient3\IClient3.csproj' /nologo /nr:false /p:platform=$BuildPlatform /p:configuration=$BuildConfiguration /p:VisualStudioVersion=$BuildVisualStudioVersion 
msbuild.exe $CurrentDir'\Samples\HelloWorld\ServerAPI\IServer.csproj' /nologo /nr:false /p:platform=$BuildPlatform /p:configuration=$BuildConfiguration /p:VisualStudioVersion=$BuildVisualStudioVersion 
# Generate assemblies
cd Samples\HelloWorld
.\Generate-Assemblies.ps1
cd ..
cd ..
# Build entire solution -- TO DO - NOT WORKING -- Works if run in VS though
msbuild.exe $CurrentDir'\Samples\HelloWorld\HelloWorld.sln' /nologo /nr:false /p:platform=$BuildPlatform /p:configuration=$BuildConfiguration /p:VisualStudioVersion=$BuildVisualStudioVersion 


Write-output "------------- Build StreamingDemo -------------"
msbuild.exe $CurrentDir'\Samples\StreamingDemo\AnalyticsAPI\AnalyticsAPI.csproj' /nologo /nr:false /p:platform=$BuildPlatform /p:configuration=$BuildConfiguration /p:VisualStudioVersion=$BuildVisualStudioVersion
msbuild.exe $CurrentDir'\Samples\StreamingDemo\DashboardAPI\DashboardAPI.csproj' /nologo /nr:false /p:platform=$BuildPlatform /p:configuration=$BuildConfiguration /p:VisualStudioVersion=$BuildVisualStudioVersion
# Generate assemblies
cd Samples\StreamingDemo
.\Generate-Assemblies.ps1
cd ..
cd ..
msbuild.exe $CurrentDir'\Samples\StreamingDemo\StreamingDemo.sln' /nologo /nr:false /p:platform=$BuildPlatform /p:configuration=$BuildConfiguration /p:VisualStudioVersion=$BuildVisualStudioVersion

Write-output "--------------------------------------------"
Write-output "-------------      DONE!!!     -------------"
Write-output "--------------------------------------------"


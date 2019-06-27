###########################################
#
# Script to update Ambrosia to the new Nuget Release
#
# Call: 
#  .\UpdateAmbrosiaForNugetRelease.ps1 1.0.7 1.0.8
#
# Parameters:
#	CurrentVersion - Version of the Nuget 
#   NewVersion - Version upgrading to.  If NewVersion is same as CurrentVersion, it will just rebuild everything 
#
#  Note: Run this script AFTER the .nuspec files have been updated (and checked in) 
#        and the Ambrosia nuget packages (AmbrosiaLibCS and AmbrosiaLibCSDebug) have been released
#		 FYI - To release those Nuget packages, run the Ambrosia-Nuget-Release and Ambrosia-Nuget-Debug pipelines in Azure Dev Ops for Ambrosia
#
###########################################

$CurrentVersion=$args[0]
$NewVersion=$args[1]


# Verify parameters are passed
if ([string]::IsNullOrEmpty($CurrentVersion)) {            
#####    Write-output "ERROR! Missing the first parameter (CurrentVersion). "
#####	 exit
 }

if ([string]::IsNullOrEmpty($NewVersion)) {            
#####    Write-output "ERROR! Missing the second parameter (NewVersion). "
#####    exit
 }

##########################################################################
#   Wrapper around swapping out Nuget Versions in CSProj files.
#
#   Sample strings that will need to be replaced
#    <PackageReference Include="AmbrosiaLibCS" Version="1.0.11" Condition="'$(Configuration)' == 'Release' " />
#    <PackageReference Include="AmbrosiaLibCSDebug" Version="1.0.11" Condition="'$(Configuration)' == 'Debug'" />
##########################################################################

function SwapNugetStringInFile { 
	Param ($OldVer,$NewVer,$FileName)

	Write-Output "Looking in file:"$FileName
	Write-Output "Finding:"$OldVer
	Write-Output "Replacing with:"$NewVer

	# Replace for AmbrosiaLibCS
	$FullOldVerString = '"AmbrosiaLibCS" Version="'+$OldVer+'"'
	$FullNewVerString = '"AmbrosiaLibCS" Version="'+$NewVer+'"'
	(Get-Content $FileName).replace($FullOldVerString, $FullNewVerString) | Set-Content $FileName

	# Replace for AmbrosiaLibCSDebug
	$FullOldVerDebugString = '"AmbrosiaLibCSDebug" Version="'+$OldVer+'"'
	$FullNewVerDebugString = '"AmbrosiaLibCSDebug" Version="'+$NewVer+'"'
	(Get-Content $FileName).replace($FullOldVerDebugString, $FullNewVerDebugString) | Set-Content $FileName
}


Write-output "------------- Update .csproj files -------------"
Write-output ""
SwapNugetStringInFile -OldVer "1.0.12" -NewVer "1.0.13" -FileName "c:\junk\testthis.txt"






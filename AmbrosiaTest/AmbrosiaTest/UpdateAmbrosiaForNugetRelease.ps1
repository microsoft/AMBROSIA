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

function SwapStringInFile { 
	Param ($OldString,$NewString,$FileName)

	# Under construction

Write-Output $FileName
}


Write-output "------------- Update .csproj files -------------"
Write-output ""
SwapStringInFile("A","B","C")
#(Get-Content c:\junk\testthis.txt).replace('WasHere', 'Yes!!') | Set-Content c:\junk\testthis.txt



Write-output "------------- Verify Azure tables -------------"
Write-output ""



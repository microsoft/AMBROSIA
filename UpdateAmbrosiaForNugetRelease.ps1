###########################################
#
# Script to update Ambrosia to the new Nuget Release
#
# Call: 
#  .\UpdateAmbrosiaForNugetRelease.ps1 1.0.18 1.0.19
#
# Parameters:
#	CurrentVersion - Version of the Nuget 
#   NewVersion - Version upgrading to.  If NewVersion is same as CurrentVersion, it will just rebuild everything 
#
#  Note: Run this script AFTER the .nuspec files have been updated (and checked in) 
#        and the Ambrosia nuget packages (Microsoft.Ambrosia.LibCS and Microsoft.Ambrosia.LibCSDebug) have been released to Nuget.org
#		 FYI - To release those Nuget packages, run the Ambrosia-Nuget-Release and Ambrosia-Nuget-Debug pipelines in Azure Dev Ops for Ambrosia
#
###########################################

$CurrentVersion=$args[0]
$NewVersion=$args[1]


# Verify parameters are passed
if ([string]::IsNullOrEmpty($CurrentVersion)) {            
	Write-output "ERROR! Missing the first parameter (CurrentVersion). "
	exit
 }

if ([string]::IsNullOrEmpty($NewVersion)) {            
	Write-output "ERROR! Missing the second parameter (NewVersion). "
	exit
 }

##########################################################################
#   Wrapper around swapping out Nuget Versions in CSProj files.
#
#	Need to set proper encoding as files use different ones. Want to save in same encoding that created in
# 		Generated code = UTF8NoBOM (aka "(Western European (windows)") - default for Set-Content.
#		Source code = UTF8
#
#   Sample strings that will need to be replaced
#    <PackageReference Include="AmbrosiaLibCS" Version="1.0.11" Condition="'$(Configuration)' == 'Release' " />
#    <PackageReference Include="AmbrosiaLibCSDebug" Version="1.0.11" Condition="'$(Configuration)' == 'Debug'" />
##########################################################################
function SwapNugetStringInFile { 
	Param ($OldVer,$NewVer,$FileName, $NoBOMEncoding)

	$message = "File: $FileName    Nuget Ver: $OldVer -> $NewVer";
	Write-Output $message;

	# Replace for AmbrosiaLibCS and AmbrosiaLibCSDebug
	$FullOldVerString = '"Microsoft.Ambrosia.LibCS" Version="'+$OldVer+'"';
	$FullNewVerString = '"Microsoft.Ambrosia.LibCS" Version="'+$NewVer+'"';
	$FullOldVerDebugString = '"Microsoft.Ambrosia.LibCSDebug" Version="'+$OldVer+'"';
	$FullNewVerDebugString = '"Microsoft.Ambrosia.LibCSDebug" Version="'+$NewVer+'"';

	# Make the call based on what encoding to use
	If ($NoBOMEncoding -eq 'T') #UTF8NoBom used for Generated Code
	{
		(Get-Content $FileName).replace($FullOldVerString, $FullNewVerString) | Set-Content $FileName;
		(Get-Content $FileName).replace($FullOldVerDebugString, $FullNewVerDebugString) | Set-Content $FileName;
	}
	else  # Standard UTF8 used in Source code
	{
		(Get-Content $FileName).replace($FullOldVerString, $FullNewVerString) | Set-Content -Encoding UTF8 $FileName;
		(Get-Content $FileName).replace($FullOldVerDebugString, $FullNewVerDebugString) | Set-Content -Encoding UTF8 $FileName;
	}
}


##########################################################################
#
#  Make the calls here to update csproj files
#
##########################################################################

Write-output "------------- Update .csproj files -------------"
$CurrentDir = $(get-location);

# AmbrosiaCS
SwapNugetStringInFile -OldVer $CurrentVersion -NewVer $NewVersion -FileName $CurrentDir'\Clients\CSharp\AmbrosiaCS\AmbrosiaCS.csproj' -NoBOMEncoding 'F';

# PerformanceTest - discontinued
#SwapNugetStringInFile -OldVer $CurrentVersion -NewVer $NewVersion -FileName $CurrentDir'\InternalImmortals\PerformanceTest\GeneratedSourceFiles\PTAmbrosiaGeneratedAPI\latest\PTAmbrosiaGeneratedAPI.csproj' -NoBOMEncoding 'T';

# PerformanceTestInterruptible
SwapNugetStringInFile -OldVer $CurrentVersion -NewVer $NewVersion -FileName $CurrentDir'\InternalImmortals\PerformanceTestInterruptible\API\ServerAPI.csproj' -NoBOMEncoding 'F';
SwapNugetStringInFile -OldVer $CurrentVersion -NewVer $NewVersion -FileName $CurrentDir'\InternalImmortals\PerformanceTestInterruptible\GeneratedSourceFiles\PTIAmbrosiaGeneratedAPI\latest\PTIAmbrosiaGeneratedAPI.csproj' -NoBOMEncoding 'T';

# HelloWorld
SwapNugetStringInFile -OldVer $CurrentVersion -NewVer $NewVersion -FileName $CurrentDir'\Samples\HelloWorld\GeneratedSourceFiles\Client1Interfaces\latest\Client1Interfaces.csproj' -NoBOMEncoding 'T';
SwapNugetStringInFile -OldVer $CurrentVersion -NewVer $NewVersion -FileName $CurrentDir'\Samples\HelloWorld\GeneratedSourceFiles\Client2Interfaces\latest\Client2Interfaces.csproj' -NoBOMEncoding 'T';
SwapNugetStringInFile -OldVer $CurrentVersion -NewVer $NewVersion -FileName $CurrentDir'\Samples\HelloWorld\GeneratedSourceFiles\Client3Interfaces\latest\Client3Interfaces.csproj' -NoBOMEncoding 'T';
SwapNugetStringInFile -OldVer $CurrentVersion -NewVer $NewVersion -FileName $CurrentDir'\Samples\HelloWorld\GeneratedSourceFiles\ServerInterfaces\latest\ServerInterfaces.csproj' -NoBOMEncoding 'T';
#SwapNugetStringInFile -OldVer $CurrentVersion -NewVer $NewVersion -FileName $CurrentDir'\Samples\HelloWorld\IClient1\IClient1.csproj' -NoBOMEncoding 'F';  
SwapNugetStringInFile -OldVer $CurrentVersion -NewVer $NewVersion -FileName $CurrentDir'\Samples\HelloWorld\IClient2\IClient2.csproj' -NoBOMEncoding 'F';
#SwapNugetStringInFile -OldVer $CurrentVersion -NewVer $NewVersion -FileName $CurrentDir'\Samples\HelloWorld\IClient3\IClient3.csproj' -NoBOMEncoding 'F';
#SwapNugetStringInFile -OldVer $CurrentVersion -NewVer $NewVersion -FileName $CurrentDir'\Samples\HelloWorld\ServerAPI\IServer.csproj' -NoBOMEncoding 'F';

# StreamingDemo
SwapNugetStringInFile -OldVer $CurrentVersion -NewVer $NewVersion -FileName $CurrentDir'\Samples\StreamingDemo\AnalyticsAPI\AnalyticsAPI.csproj' -NoBOMEncoding 'F';
SwapNugetStringInFile -OldVer $CurrentVersion -NewVer $NewVersion -FileName $CurrentDir'\Samples\StreamingDemo\GeneratedSourceFiles\AnalyticsAPIGenerated\latest\AnalyticsAPIGenerated.csproj' -NoBOMEncoding 'T';
#SwapNugetStringInFile -OldVer $CurrentVersion -NewVer $NewVersion -FileName $CurrentDir'\Samples\StreamingDemo\DashboardAPI\DashboardAPI.csproj' -NoBOMEncoding 'F';
SwapNugetStringInFile -OldVer $CurrentVersion -NewVer $NewVersion -FileName $CurrentDir'\Samples\StreamingDemo\Dashboard\Dashboard.csproj' -NoBOMEncoding 'F';
SwapNugetStringInFile -OldVer $CurrentVersion -NewVer $NewVersion -FileName $CurrentDir'\Samples\StreamingDemo\GeneratedSourceFiles\DashboardAPIGenerated\latest\DashboardAPIGenerated.csproj' -NoBOMEncoding 'T';

#XamarinCommandShell
SwapNugetStringInFile -OldVer $CurrentVersion -NewVer $NewVersion -FileName $CurrentDir'\InternalImmortals\XamarinCommandShell\ICommandShellImmortal\ICommandShellImmortal.csproj' -NoBOMEncoding 'F';
SwapNugetStringInFile -OldVer $CurrentVersion -NewVer $NewVersion -FileName $CurrentDir'\InternalImmortals\XamarinCommandShell\GeneratedSourceFiles\ICommandShellImmortalGenerated\latest\ICommandShellImmortalGenerated.csproj' -NoBOMEncoding 'F';
SwapNugetStringInFile -OldVer $CurrentVersion -NewVer $NewVersion -FileName $CurrentDir'\InternalImmortals\XamarinCommandShell\XamarinCommandShell.GTK\XamarinCommandShell.GTK.csproj' -NoBOMEncoding 'F';

Write-output "--------------------------------------------"
Write-output "-------------      DONE!!!     -------------"
Write-output "--------------------------------------------"




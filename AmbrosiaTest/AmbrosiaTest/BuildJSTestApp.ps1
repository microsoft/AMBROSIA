###########################################
#
# Script to build the Javascript Test Apps
#
# TO DO: Currently, only one JS Test App, but if get more could make this generic enough
#  Parameter:
#      PathToAppToBuild - path on where the TestApp is located
#
#  Example: BuildJSTestApp.ps1 D:\\Ambrosia\\AmbrosiaJS\\TestApp
#
###########################################



$PathToAppToBuild=$args[0]

# Verify parameter is passed
if ([string]::IsNullOrEmpty($PathToAppToBuild)) {            
    Write-Host "ERROR! Missing parameter value. "
	Write-Host "       Please specify the path to TestApp"            
    Write-Host   
	exit
}

Write-host "------------- Building TestApp at: $PathToAppToBuild -------------"
Write-host 
Set-Location $PathToAppToBuild
npx tsc -p tsconfig.json
Write-host "------------- DONE! Building!  -------------"

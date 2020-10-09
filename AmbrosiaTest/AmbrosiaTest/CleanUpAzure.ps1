###########################################
#
# Script to clean up the Azure tables.
#
#  NOTE: This script requires PowerShell 7.  Make sure that is the version that is in the path.
#  NOTE: powershell.exe is < ver 6.  pwsh.exe is ver 6+
#
# Parameters:
#	ObjectName - name of the objects in Azure you want to delete - can use "*" as wild card ... so "process" will NOT delete "process1" but "process*" will.
#
#	NOTE - might need Microsoft Azure Powershell add in - http://go.microsoft.com/fwlink/p/?linkid=320376&clcid=0x409
#		 - also need to do this at powershell prompt: 
#               - Install-Module Az -AllowClobber
#               - Install-Module AzTable -AllowClobber
#               - Enable-AzureRmAlias -Scope CurrentUser
#				- Get-Module -ListAvailable AzureRM   -->> This should show 5.6 (just needs to be above 4.4)
#                - NOTE - might need to run  Set-ExecutionPolicy Unrestricted
#		- This script requires environment variable
#				- AZURE_STORAGE_CONN_STRING - Connection string used to connect to the Azure subscription
#
#   Info - https://docs.microsoft.com/en-us/azure/cosmos-db/table-storage-how-to-use-powershell
#
#  WARNING - you are deleting items in Azure ... be careful on using this as you don't want to delete other people's data
#
###########################################

$ObjectName=$args[0]

# Verify parameter is passed
if ([string]::IsNullOrEmpty($ObjectName)) {            
    Write-Host "ERROR! Missing parameter value. "
	Write-Host "       Please specify the name of the objects that you want deleted from the Ambrosia Azure tables."            
    Write-Host   
    Write-Host "       Note: Wild cards (ie *ImmCoord1*) are supported."            
    Write-Host   
	exit
}

# Verify the connection info is there
if ([string]::IsNullOrEmpty($env:AZURE_STORAGE_CONN_STRING)) {            
    Write-Host "ERROR! Missing environment variable AZURE_STORAGE_CONN_STRING"            
    Write-Host "       That env variable containes the needed connection info"            
    Write-Host   
	exit
}

Write-host "------------- Clean Up Azure tables and file share -------------"
Write-host 
Write-host "--- Connection Info ---"

# Get connection info from Env Var
$ConnectionString = $env:AZURE_STORAGE_CONN_STRING
$ConnectionString_Array = $ConnectionString.Split(";")
$ConnectionString_Array2 = $ConnectionString_Array.Split("=")
$storageAccountName = $ConnectionString_Array2[3]
$storageKey = $ConnectionString_Array2[5]+"=="  #Split removes the == off the end so put them back

Write-host " Storage Account:" $storageAccountName
Write-host " Storage Key:" $storageKey
Write-host "----------------"
Write-host

# Get a storage context
$ctx = New-AzStorageContext -StorageAccountName $storageAccountName -StorageAccountKey $storageKey
$container = "ambrosialogs"

# Clean up the data in the CRA (Immortal Coordinator) tables
Write-host "------------- Delete items in Azure table: craendpointtable filtered on $ObjectName -------------"
$tableName = "craendpointtable"
$storageTable = Get-AzStorageTable -Name $tableName -Context $ctx 
Get-AzTableRow -table $storageTable.CloudTable | Where-Object -Property “PartitionKey” -CLike $ObjectName | Remove-AzTableRow -table $storageTable.CloudTable
Write-host 


Write-host "------------- Delete items in Azure table: craconnectiontable filtered on $ObjectName -------------"
$tableName = "craconnectiontable"
$storageTable = Get-AzStorageTable -Name $tableName -Context $ctx 
Get-AzTableRow -table $storageTable.CloudTable | Where-Object -Property “PartitionKey” -CLike $ObjectName | Remove-AzTableRow -table $storageTable.CloudTable
Write-host 


Write-host "------------- Delete items in Azure table: cravertextable filtered on $ObjectName -------------"
$tableName = "cravertextable"
$storageTable = Get-AzStorageTable  -Name $tableName -Context $ctx 
Get-AzTableRow -table $storageTable.CloudTable | Where-Object -Property “PartitionKey” -CLike $ObjectName | Remove-AzTableRow -table $storageTable.CloudTable
Write-host 

# Delete the tables created by the Ambrosia
Write-host "------------- Delete Ambrosia created tables filtered on $ObjectName -------------"
Get-AzStorageTable $ObjectName* -Context $ctx | Remove-AzStorageTable -Context $ctx -Force

Write-host "------------- Delete Azure Blobs in Azure table: ambrosialogs filtered on $ObjectName -------------"
$blobs = Get-AzStorageBlob -Container $container -Context $ctx | Where-Object Name -Like $ObjectName*

#Remove lease on each Blob
$blobs | ForEach-Object{$_.ICloudBlob.BreakLease()}

#Delete blobs in a specified container.
$blobs| Remove-AzStorageBlob

#Write-host "------------- Clean Up Azure File Share -------------"
#Write-host 
## TO DO: Not sure what we do here for File Share ... need the proper name and if we even use it any more.
#Remove-AzureStorageShare -Context $ctx.Context -Name Ambrosia_logs


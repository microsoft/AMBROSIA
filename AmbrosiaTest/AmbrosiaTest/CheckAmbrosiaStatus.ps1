###########################################
#
# Script to check the status of Ambrosia Azure tables as well as running processes
#
# Call: 
#  .\CheckAmbrosiaStatus.ps1 laractiveactiveadd* > AmbrosiaStatus.log 2>&1
#
# Parameters:
#	ObjectName - name of the objects in Azure you want to check - can use "*" as wild card ... 
#
#	Note - might need Microsoft Azure Powershell add in - http://go.microsoft.com/fwlink/p/?linkid=320376&clcid=0x409
#		 - also need to do this at powershell prompt: 
#				- Install-Module -Name AzureRM -AllowClobber
#				- Install-Module AzureRmStorageTable
#				- Get-Module -ListAvailable AzureRM   -->> This should show 5.6 (just needs to be above 4.4)
#		- This script requires environment variable
#				- AZURE_STORAGE_CONN_STRING - Connection string used to connect to the Azure subscription
#
#   Info - https://docs.microsoft.com/en-us/azure/cosmos-db/table-storage-how-to-use-powershell
#
###########################################

$ObjectName=$args[0]


# Verify parameter is passed
if ([string]::IsNullOrEmpty($ObjectName)) {            
    Write-output "ERROR! Missing parameter value. "
	Write-output "       Please specify the name of the objects that you want checked in the Ambrosia Azure tables."            
    Write-output ""
    Write-output "       Note: Wild cards (ie *ImmCoord1*) are supported."            
    Write-output ""  
	exit
 }

# Verify the connection info is there
if ([string]::IsNullOrEmpty($env:AZURE_STORAGE_CONN_STRING)) {            
    Write-output "ERROR! Missing environment variable AZURE_STORAGE_CONN_STRING"            
    Write-output "       That env variable containes the needed connection info"            
    Write-output ""  
	exit
}

Write-output "------------- Verify Running Ambrosia process -------------"
Write-output ""
Write-output "-- ImmCoord Worker -- "
Get-Process -Name ImmortalCoordinator
Write-output "-- Job.exe -- "
Get-Process -Name Job
Write-output "-- Server.exe -- "
Get-Process -Name Server
Write-output "-- Ambrosia.exe -- "
Get-Process -Name Ambrosia

Write-output "------------- Verify Azure tables -------------"
Write-output ""
Write-output "--- Connection Info ---"

# Get connection info from Env Var
$ConnectionString = $env:AZURE_STORAGE_CONN_STRING
$ConnectionString_Array = $ConnectionString.Split(";")
$ConnectionString_Array2 = $ConnectionString_Array.Split("=")
$storageAccountName = $ConnectionString_Array2[3]
$storageKey = $ConnectionString_Array2[5]+"=="  #Split removes the == off the end so put them back

Write-output " Storage Account:" $storageAccountName
Write-output " Storage Key:" $storageKey
Write-output "----------------"
Write-output ""

# had issues when used $ctx for each table call so made separate ctx var

Write-output "------------- Get items from Azure table: craendpointtable filtered on $ObjectName -------------"
$tableName2 = "craendpointtable"
$ctx = New-AzureStorageContext -StorageAccountName $storageAccountName -StorageAccountKey $storageKey
$storageTable2 = Get-AzureStorageTable -Name $tableName2 -Context $ctx 
Get-AzureStorageTableRowAll -table $storageTable2 | where PartitionKey -Like $ObjectName
Write-output ""

Write-output "------------- Get items from Azure table: craconnectiontable filtered on $ObjectName -------------"
$tableName1 = "craconnectiontable"
$ctx1 = New-AzureStorageContext -StorageAccountName $storageAccountName -StorageAccountKey $storageKey
$storageTable1 = Get-AzureStorageTable -Name $tableName1 -Context $ctx1
Get-AzureStorageTableRowAll -table $storageTable1 | where PartitionKey -Like $ObjectName 
Write-output "" 

Write-output "------------- Get items from Azure table: cravertextable filtered on $ObjectName -------------"
$tableName3 = "cravertextable"
$ctx2 = New-AzureStorageContext -StorageAccountName $storageAccountName -StorageAccountKey $storageKey
$storageTable3 = Get-AzureStorageTable -Name $tableName3 -Context $ctx2 
Write-output "-- PartitionKey --"
Get-AzureStorageTableRowAll -table $storageTable3 | where PartitionKey -Like $ObjectName 
Write-output "-- RowKey -- "
Get-AzureStorageTableRowAll -table $storageTable3 | where RowKey -Like $ObjectName
Write-output "" 



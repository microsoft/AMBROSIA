# Sourced into parent scripts or shell:
# Sets auth-related variables to access Azure Storage.

# if [[ -v AZ ]]; # Needs bash 4
if [[ ${AZ:+isdefined} ]];  
then
  AZURE_STORAGE_CONNECTION_STRING=$($AZ storage account show-connection-string --name $AZURE_STORAGE_NAME --resource-group $AZURE_RESOURCE_GROUP --query connectionString --output tsv)
  AZURE_STORAGE_KEY=$($AZ storage account keys list --account-name $AZURE_STORAGE_NAME --resource-group $AZURE_RESOURCE_GROUP --query "[0].value" --output tsv)
  export AZURE_STORAGE_CONNECTION_STRING
  export AZURE_STORAGE_KEY

  # <FIXME>: Ambrosia should be corrected to not expect this!!!
    AZURE_STORAGE_CONN_STRING="$AZURE_STORAGE_CONNECTION_STRING"
    export AZURE_STORAGE_CONN_STRING
  # </FIXME>: See VSTS bug 127

  echo "AZURE_STORAGE_KEY=$AZURE_STORAGE_KEY"
  echo "AZURE_STORAGE_CONNECTION_STRING=$AZURE_STORAGE_CONNECTION_STRING"
else
    echo "Error, Set-Storage-Vars.sh: source Defs/Common-Defs.sh before this file."
fi

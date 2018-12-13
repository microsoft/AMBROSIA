# Sourced into parent scripts or shell:
# Sets auth-related variables to access a docker registry.

if [[ ${AZ:+isdefined} ]];     
then
    DockerPrivateRegistry_Login=$ACR_NAME
    DockerPrivateRegistry_URL=$($AZ acr show --name $ACR_NAME --query loginServer --output tsv)
    # TSV output here avoids double quotes getting into the variable:
    DockerPrivateRegistry_Pwd=$($AZ acr credential show --name $ACR_NAME --query "passwords[0].value" --output tsv)

    export DockerPrivateRegistry_Login
    export DockerPrivateRegistry_URL
    export DockerPrivateRegistry_Pwd
else
    echo "Error, Set-Storage-Vars.sh: source Defs/Common-Defs.sh before this file."
fi

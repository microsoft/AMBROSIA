# Set PTI Client app parameter defaults
$serverInstanceName = "PTIServer"

# Other parameters [that we specify no default values for]
$otherParams = @() # An empty array
$ambrosiaConfigFile = ""

# Reports an error if the supplied parameter value is not empty
function checkValue
{
    param([string]$value, [string]$paramName)
    if ($value.trim().length -eq 0)
    {
        write-host "`nError: Missing value for parameter '${paramName}'`n" -ForegroundColor Red
        exit 1
    }
}

# Override our defaults with any corresponding values from the command-line
for ($i = 0; $i -lt $args.count; $i++)
{
    $name = $args[$i]
    $value = ""

    if ($args[$i].indexOf("=") -ne -1)
    {
        $name = $args[$i].split("=")[0].trim()
        $value = $args[$i].split("=")[1].trim()
    }

    switch -exact ($name)
    {
        { @("-sin", "--serverInstanceName") -contains $_ } { checkValue $value $_; $serverInstanceName = $value; }
        { @(("-ir", "--instanceRole") -contains $_) } { if ($value -ne "Client") { write-host "`nError: Parameter '${_}' must not be supplied; it is always 'Client' when using runClient.ps1`n" -ForegroundColor Red; exit 1; } }
        "--ambrosiaConfigFile" { checkValue $value $_; $ambrosiaConfigFile = "ambrosiaConfigFile=${value}"; } # A JS-LB (not PTI) parameter [included here only to allow specifying it with a leading "--" for consistency with other PTI app parameters]
        default { $otherParams += $args[$i]; } # We let the PTI app validate these
    }
} 

write-host `nStarting PTI with these parameters: --instanceRole=Client --serverInstanceName=$serverInstanceName $otherParams $ambrosiaConfigFile
#exit 0

# Launch the client
node ..\App\out\main.js --instanceRole=Client --serverInstanceName=$serverInstanceName $otherParams $ambrosiaConfigFile
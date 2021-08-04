# Set PTI Server app parameter defaults
$expectedFinalBytes = 1073741824 # 1 GB
$clientInstanceName = "PTIClient"

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
        { @("-efb", "--expectedFinalBytes") -contains $_ } { checkValue $value $_; $expectedFinalBytes = $value; }
        { @("-cin", "--clientInstanceName") -contains $_ } { checkValue $value $_; $clientInstanceName = $value; }
        { @(("-ir", "--instanceRole") -contains $_) } { if ($value -ne "Server") { write-host "`nError: Parameter '${_}' must not be supplied; it is always 'Server' when using runServer.ps1`n" -ForegroundColor Red; exit 1; } }
        "--ambrosiaConfigFile" { checkValue $value $_; $ambrosiaConfigFile = "ambrosiaConfigFile=${value}"; } # A JS-LB (not PTI) parameter [included here only to allow specifying it with a leading "--" for consistency with other PTI app parameters]
        default { $otherParams += $args[$i]; } # We let the PTI app validate these
    }
} 

write-host `nStarting PTI with these parameters: --instanceRole=Server --expectedFinalBytes=$expectedFinalBytes $otherParams $ambrosiaConfigFile
#exit 0

# Launch the server
node ..\App\out\main.js --instanceRole=Server --expectedFinalBytes=$expectedFinalBytes --clientInstanceName=$clientInstanceName $otherParams $ambrosiaConfigFile
#Perform the code-generation step for this example application.

# PerformanceTest requires to be in Debug
#if ( $env:AMBVARIANT ) {
#    $AMBVARIANT = $env:AMBVARIANT
#} else {
    $AMBVARIANT="x64\Debug\net461"
#}

if ( $env:AMBROSIATOOLS ) {
    $AMBROSIATOOLS=$env:AMBROSIATOOLS
} else {
    $AMBROSIATOOLS = "..\..\Clients\CSharp\AmbrosiaCS\bin"
}

Write-Host "Using variant of AmbrosiaCS: $AMBVARIANT"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
Write-Host "Executing codegen command: $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a=API\bin\Debug\netstandard2.0\ServerAPI.dll -a=ClientAPI\bin\Debug\netstandard2.0\ClientAPI.dll -p=API\ServerAPI.csproj -p=ClientAPI\ClientAPI.csproj -o=PTAmbrosiaGeneratedAPI -f=netstandard2.0"
& $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a="API\bin\Debug\netstandard2.0\ServerAPI.dll" -a="ClientAPI\bin\Debug\netstandard2.0\ClientAPI.dll" -p="API\ServerAPI.csproj" -p="ClientAPI\ClientAPI.csproj" -o="PTAmbrosiaGeneratedAPI" -f="netstandard2.0"
# Perform the code-generation step for this example application.
if ( $env:AMBVARIANT ) {
    $AMBVARIANT = $env:AMBVARIANT
} else {
    $AMBVARIANT="x64\Debug\net461"
}

if ( $env:AMBROSIATOOLS ) {
    $AMBROSIATOOLS=$env:AMBROSIATOOLS
} else {
    $AMBROSIATOOLS = "..\..\Clients\CSharp\AmbrosiaCS\bin"
}

Write-Host "Using variant of AmbrosiaCS: $AMBVARIANT"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
Write-Host "Executing codegen command: $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a=API\bin\$AMBVARIANT\ServerAPI.dll -a=IJob\bin\$AMBVARIANT\IJob.dll -p=API\ServerAPI.csproj -p=IJob\IJob.csproj -o=PTIAmbrosiaGeneratedAPI -f=netstandard2.0"
& $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a="API\bin\Debug\netstandard2.0\ServerAPI.dll" -a="IJob\bin\Debug\netstandard2.0\IJob.dll" -p="API\ServerAPI.csproj" -p="IJob\IJob.csproj" -o="PTIAmbrosiaGeneratedAPI" -f="netstandard2.0"
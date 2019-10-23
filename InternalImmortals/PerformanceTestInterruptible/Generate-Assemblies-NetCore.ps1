# Perform the code-generation step for this example application.
if ( $env:AMBVARIANTCORE ) {
    $AMBVARIANTCORE=$env:AMBVARIANTCORE
} else {
    $AMBVARIANTCORE = "x64\Debug\netcoreapp2.2"
}

if ( $env:AMBROSIATOOLS ) {
    $AMBROSIATOOLS=$env:AMBROSIATOOLS
} else {
    $AMBROSIATOOLS = "..\..\Clients\CSharp\AmbrosiaCS\bin"
}

Write-Host "Using variant of AmbrosiaCS: $AMBVARIANTCORE"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
Write-Host "Executing codegen command: dotnet $AMBROSIATOOLS\$AMBVARIANTCORE\AmbrosiaCS.dll CodeGen -a=API\bin\$AMBVARIANTCORE\ServerAPI.dll -a=IJob\bin\$AMBVARIANTCORE\IJob.dll -p=API\ServerAPI.csproj -p=IJob\IJob.csproj -o=PTIAmbrosiaGeneratedAPI -f=net46 -f=netcoreapp2.2"
& dotnet $AMBROSIATOOLS\$AMBVARIANTCORE\AmbrosiaCS.dll CodeGen -a="API\bin\$AMBVARIANTCORE\ServerAPI.dll" -a="IJob\bin\$AMBVARIANTCORE\IJob.dll" -p="API\ServerAPI.csproj" -p="IJob\IJob.csproj" -o="PTIAmbrosiaGeneratedAPI" -f="net46" -f="netcoreapp2.2"
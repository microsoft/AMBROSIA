# Perform the code-generation step for this example application.

if ( $env:AMBVARIANTCORE ) {
    $AMBVARIANTCORE=$env:AMBVARIANTCORE
} else {
    $AMBVARIANTCORE = "x64\Debug\netcoreapp2.0"
}

# Create the dependencies folder
New-Item -ItemType Directory -Force -Path "CodeGenDependencies\netcoreapp2.0"
Get-ChildItem "CodeGenDependencies\netcoreapp2.0" | Remove-Item
Copy-Item "API\bin\Debug\netcoreapp2.0\publish\*" -Force -Destination "CodeGenDependencies\netcoreapp2.0\"
# DANGER, WARNING, FIXME: it is UNSAFE to MERGE the outputs of two publish directories:
Copy-Item "IJob\bin\Debug\netcoreapp2.0\publish\*" -Force -Destination "CodeGenDependencies\netcoreapp2.0\"

Write-Host "Using variant of AmbrosiaCS.exe: $AMBVARIANTCORE"

Write-Host "Executing codegen command: dotnet ..\..\Clients\CSharp\AmbrosiaCS\bin\$AMBVARIANT\AmbrosiaCS.dll CodeGen -a=API\bin\$AMBVARIANT\ServerAPI.dll -a=IJob\bin\$AMBVARIANT\IJob.dll -o=PTAmbrosiaGeneratedAPI -f=net46 -f=netcoreapp2.0 -fb=net46;CodeGenDependencies\net46 -fb=netcoreapp2.0;CodeGenDependencies\netcoreapp2.0"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
& dotnet "..\..\Clients\CSharp\AmbrosiaCS\bin\$AMBVARIANT\AmbrosiaCS.dll" CodeGen -a="API\bin\$AMBVARIANT\ServerAPI.dll" -a="IJob\bin\$AMBVARIANT\IJob.dll" -o="PTIAmbrosiaGeneratedAPI" -f="net46" -f="netcoreapp2.0" -fb="net46;CodeGenDependencies\net46" -fb="netcoreapp2.0;CodeGenDependencies\netcoreapp2.0"
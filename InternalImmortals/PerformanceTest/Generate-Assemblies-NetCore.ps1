# Perform the code-generation step for this example application.

# Create the output directory if necessary
New-Item -ItemType Directory -Force -Path GeneratedAssemblies | Out-Null

# Remove the old copies of the generated assemblies
Get-ChildItem GeneratedAssemblies | Remove-Item

if (-not ( $env:AMBVARIANT )) {
    $env:AMBVARIANT="x64\Debug\netcoreapp2.0"
}

Write-Host "Using variant of AmbrosiaCS.exe: $env:AMBVARIANT"

Write-Host "Executing codegen command: dotnet ..\..\Clients\CSharp\AmbrosiaCS\bin\$env:AMBVARIANT\AmbrosiaCS.dll CodeGen -a=API\bin\$env:AMBVARIANT\ServerAPI.dll -a=ClientAPI\bin\$env:AMBVARIANT\ClientAPI.dll -o=PTIAmbrosiaGeneratedAPI"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
& dotnet "..\..\Clients\CSharp\AmbrosiaCS\bin\$env:AMBVARIANT\AmbrosiaCS.dll" CodeGen -a="API\bin\$env:AMBVARIANT\ServerAPI.dll" -a="ClientAPI\bin\$env:AMBVARIANT\ClientAPI.dll" -o="PTIAmbrosiaGeneratedAPI"
# Perform the code-generation step for this example application.

if (-not ( $env:AMBVARIANT )) {
    $env:AMBVARIANT="x64\Debug\net46"
}

Write-Host "Using variant of AmbrosiaCS.exe: $env:AMBVARIANT"

Write-Host "Executing codegen command: ..\..\Clients\CSharp\AmbrosiaCS\bin\$env:AMBVARIANT\AmbrosiaCS.exe CodeGen -a=IJob\bin\$env:AMBVARIANT\IJob.dll -a=API\bin\$env:AMBVARIANT\ServerAPI.dll -o=PTIAmbrosiaGeneratedAPI"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
& "..\..\Clients\CSharp\AmbrosiaCS\bin\$env:AMBVARIANT\AmbrosiaCS.exe" CodeGen -a="IJob\bin\$env:AMBVARIANT\IJob.dll" -a="API\bin\$env:AMBVARIANT\ServerAPI.dll" -o="PTIAmbrosiaGeneratedAPI"
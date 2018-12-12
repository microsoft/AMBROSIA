# Perform the code-generation step for this example application.

if (-not ( $env:AMBVARIANT )) {
    $env:AMBVARIANT="x64\Debug\net46"
}

# Create the dependencies folder
New-Item -ItemType Directory -Force -Path "CodeGenDependencies\net46\"
Get-ChildItem "CodeGenDependencies\net46\" | Remove-Item
Copy-Item "API\bin\$env:AMBVARIANT\*" -Force -Destination "CodeGenDependencies\net46\"
Copy-Item "ClientAPI\bin\$env:AMBVARIANT\*" -Force -Destination "CodeGenDependencies\net46\"

Write-Host "Using variant of AmbrosiaCS.exe: $env:AMBVARIANT"

Write-Host "Executing codegen command: ..\..\Clients\CSharp\AmbrosiaCS\bin\$env:AMBVARIANT\AmbrosiaCS.exe CodeGen -a=API\bin\$env:AMBVARIANT\ServerAPI.dll -a=ClientAPI\bin\$env:AMBVARIANT\ClientAPI.dll -o=PTIAmbrosiaGeneratedAPI -f=net46 -b=CodeGenDependencies\net46"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
& "..\..\Clients\CSharp\AmbrosiaCS\bin\$env:AMBVARIANT\AmbrosiaCS.exe" CodeGen -a="API\bin\$env:AMBVARIANT\ServerAPI.dll" -a="ClientAPI\bin\$env:AMBVARIANT\ClientAPI.dll" -o="PTAmbrosiaGeneratedAPINet46" -f="net46" -b="CodeGenDependencies\net46"

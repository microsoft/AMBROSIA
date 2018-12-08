# Perform the code-generation step for this example application.

if (-not ( $env:AMBVARIANT )) {
    $env:AMBVARIANT="x64\Debug\net46"
}

# Create the dependencies folder
New-Item -ItemType Directory -Force -Path "CodeGenDependencies\net46\"
Get-ChildItem "CodeGenDependencies\net46\" | Remove-Item
Copy-Item "API\bin\$env:AMBVARIANT\*" -Force -Destination "CodeGenDependencies\net46\"
Copy-Item "IJob\bin\$env:AMBVARIANT\*" -Force -Destination "CodeGenDependencies\net46\"

Copy-Item "..\..\Clients\CSharp\AmbrosiaCS\AmbrosiaCS.csproj" -Force -Destination "CodeGenDependencies\net46\"

Write-Host "Using variant of AmbrosiaCS.exe: $env:AMBVARIANT"

Write-Host "Executing codegen command: ..\..\Clients\CSharp\AmbrosiaCS\bin\$env:AMBVARIANT\AmbrosiaCS.exe CodeGen -a=API\bin\$env:AMBVARIANT\ServerAPI.dll -a=IJob\bin\$env:AMBVARIANT\IJob.dll -o=PTIAmbrosiaGeneratedAPINet46 -f=net46 -b=CodeGenDependencies\net46"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
& "..\..\Clients\CSharp\AmbrosiaCS\bin\$env:AMBVARIANT\AmbrosiaCS.exe" CodeGen -a="API\bin\$env:AMBVARIANT\ServerAPI.dll" -a="IJob\bin\$env:AMBVARIANT\IJob.dll" -o="PTIAmbrosiaGeneratedAPINet46" -f="net46" -b="CodeGenDependencies\net46"
# Perform the code-generation step for this example application.

if ( $env:AMBVARIANT ) {
    $AMBVARIANT = $env:AMBVARIANT
} else {
    $AMBVARIANT="x64\Debug\net46"
}

# Create the dependencies folder
New-Item -ItemType Directory -Force -Path "CodeGenDependencies\net46\"
Get-ChildItem "CodeGenDependencies\net46\" | Remove-Item
Copy-Item "API\bin\$AMBVARIANT\*" -Force -Destination "CodeGenDependencies\net46\"
# DANGER, WARNING, FIXME: it is UNSAFE to MERGE the outputs of two publish directories:
Copy-Item "IJob\bin\$AMBVARIANT\*" -Force -Destination "CodeGenDependencies\net46\"

Write-Host "Using variant of AmbrosiaCS.exe: $AMBVARIANT"

Write-Host "Executing codegen command: ..\..\Clients\CSharp\AmbrosiaCS\bin\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a=API\bin\$AMBVARIANT\ServerAPI.dll -a=IJob\bin\$AMBVARIANT\IJob.dll -o=PTIAmbrosiaGeneratedAPINet46 -f=net46 -b=CodeGenDependencies\net46"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
& "..\..\Clients\CSharp\AmbrosiaCS\bin\$AMBVARIANT\AmbrosiaCS.exe" CodeGen -a="API\bin\$AMBVARIANT\ServerAPI.dll" -a="IJob\bin\$AMBVARIANT\IJob.dll" -o="PTIAmbrosiaGeneratedAPINet46" -f="net46" -b="CodeGenDependencies\net46"

# Perform the code-generation step for this example application.

if ( $env:AMBVARIANT ) {
    $AMBVARIANT = $env:AMBVARIANT
} else {
    $AMBVARIANT="x64\Debug\net46"
}

# Create the dependencies folder
New-Item -ItemType Directory -Force -Path "CodeGenDependencies\net46\"
Get-ChildItem "CodeGenDependencies\net46\" | Remove-Item
Copy-Item "API\bin\x64\Debug\net46\*" -Force -Destination "CodeGenDependencies\net46\"
Copy-Item "ClientAPI\bin\x64\Debug\net46\*" -Force -Destination "CodeGenDependencies\net46\"

New-Item -ItemType Directory -Force -Path "CodeGenDependencies\netcoreapp2.0\"
Get-ChildItem "CodeGenDependencies\netcoreapp2.0" | Remove-Item
Copy-Item "API\bin\Debug\netcoreapp2.0\publish\*" -Force -Destination "CodeGenDependencies\netcoreapp2.0\"
Copy-Item "ClientAPI\bin\Debug\netcoreapp2.0\publish\*" -Force -Destination "CodeGenDependencies\netcoreapp2.0\"

Write-Host "Using variant of AmbrosiaCS.exe: $AMBVARIANT"

Write-Host "Executing codegen command: ..\..\Clients\CSharp\AmbrosiaCS\bin\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a=API\bin\$AMBVARIANT\ServerAPI.dll -a=ClientAPI\bin\$AMBVARIANT\ClientAPI.dll -o=PTAmbrosiaGeneratedAPI -f=net46 -f=netcoreapp2.0 -fb=net46;CodeGenDependencies\net46 -fb=netcoreapp2.0;CodeGenDependencies\netcoreapp2.0"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
& "..\..\Clients\CSharp\AmbrosiaCS\bin\$AMBVARIANT\AmbrosiaCS.exe" CodeGen -a="API\bin\$AMBVARIANT\ServerAPI.dll" -a="ClientAPI\bin\$AMBVARIANT\ClientAPI.dll" -o="PTAmbrosiaGeneratedAPI" -f="net46" -f="netcoreapp2.0" -fb="net46;CodeGenDependencies\net46" -fb="netcoreapp2.0;CodeGenDependencies\netcoreapp2.0"
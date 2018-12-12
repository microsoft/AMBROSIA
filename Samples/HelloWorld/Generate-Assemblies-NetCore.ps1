# Perform the code-generation step for this example application.

$env:AMBVARIANT="x64\Debug\netcoreapp2.0"

# FIXME: Change this script to depend on the binary distribution of AMBROSIA
# rather than the source tree.
$ambrosiaPath="..\.."

# Create an empty codegen dependencies folder
New-Item -ItemType Directory -Force -Path "CodeGenDependencies\netcoreapp2.0" | Out-Null
Get-ChildItem "CodeGenDependencies\netcoreapp2.0\" | Remove-Item

Write-Host "Using variant of CodeGen.exe: $env:AMBVARIANT"

Write-Host "Executing codegen command: dotnet $env:AMBROSIATOOLS\x64\Release\netcoreapp2.0\AmbrosiaCS.dll CodeGen -a=ServerAPI\bin\$env:AMBVARIANT\IServer.dll -o=ServerInterfaces -f=netcoreapp2.0 -b=CodeGenDependencies\netcoreapp2.0"

Write-Host "Executing codegen command: dotnet $env:AMBROSIATOOLS\x64\Release\netcoreapp2.0\AmbrosiaCS.dll CodeGen -a=ServerAPI\bin\$env:AMBVARIANT\IServer.dll -a=IClient1\bin\$env:AMBVARIANT\IClient1.dll -o=Client1Interfaces -f=netcoreapp2.0 -b=CodeGenDependencies\netcoreapp2.0"

Write-Host "Executing codegen command: $env:AMBROSIATOOLS\x64\Release\netcoreapp2.0\AmbrosiaCS.dll CodeGen -a=ServerAPI\bin\$env:AMBVARIANT\IServer.dll -a=IClient2\bin\$env:AMBVARIANT\IClient2.dll -o=Client2Interfaces -f=netcoreapp2.0 -b=CodeGenDependencies\netcoreapp2.0"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
& dotnet $env:AMBROSIATOOLS\x64\Release\netcoreapp2.0\AmbrosiaCS.dll CodeGen -a="ServerAPI\bin\$env:AMBVARIANT\IServer.dll" -o=ServerInterfaces -f="netcoreapp2.0" -b="CodeGenDependencies\netcoreapp2.0"
& dotnet $env:AMBROSIATOOLS\x64\Release\netcoreapp2.0\AmbrosiaCS.dll CodeGen -a="ServerAPI\bin\$env:AMBVARIANT\IServer.dll" -a="IClient1\bin\$env:AMBVARIANT\IClient1.dll" -o=Client1Interfaces -f="netcoreapp2.0" -b="CodeGenDependencies\netcoreapp2.0"
& dotnet $env:AMBROSIATOOLS\x64\Release\netcoreapp2.0\AmbrosiaCS.dll CodeGen -a="ServerAPI\bin\$env:AMBVARIANT\IServer.dll" -a="IClient2\bin\$env:AMBVARIANT\IClient2.dll" -o=Client2Interfaces -f="netcoreapp2.0" -b="CodeGenDependencies\netcoreapp2.0"

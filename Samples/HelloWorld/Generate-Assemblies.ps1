# Perform the code-generation step for this example application.

$env:AMBVARIANT="x64\Debug\net46"
$env:AMBROSIATOOLS="..\..\Clients\CSharp\AmbrosiaCS\bin"

# FIXME: Change this script to depend on the binary distribution of AMBROSIA
# rather than the source tree.
$ambrosiaPath="..\.."

# Create an empty codegen dependencies folder
New-Item -ItemType Directory -Force -Path "CodeGenDependencies\net46" | Out-Null
Get-ChildItem "CodeGenDependencies\net46\" | Remove-Item

Write-Host "Using variant of CodeGen.exe: $env:AMBVARIANT"

Write-Host "Executing codegen command: $env:AMBROSIATOOLS\$env:AMBVARIANT\AmbrosiaCS.exe CodeGen -a=ServerAPI\bin\$env:AMBVARIANT\IServer.dll -o=ServerInterfacesNet46 -f=net46 -b=CodeGenDependencies\net46"

Write-Host "Executing codegen command: $env:AMBROSIATOOLS\$env:AMBVARIANT\AmbrosiaCS.exe CodeGen -a=ServerAPI\bin\$env:AMBVARIANT\IServer.dll -a=IClient1\bin\$env:AMBVARIANT\IClient1.dll -o=Client1InterfacesNet46 -f=net46 -b=CodeGenDependencies\net46"

Write-Host "Executing codegen command: $env:AMBROSIATOOLS\$env:AMBVARIANT\AmbrosiaCS.exe CodeGen -a=ServerAPI\bin\$env:AMBVARIANT\IServer.dll -a=IClient2\bin\$env:AMBVARIANT\IClient2.dll -o=Client2InterfacesNet46 -f=net46 -b=CodeGenDependencies\net46"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
& $env:AMBROSIATOOLS\$env:AMBVARIANT\AmbrosiaCS.exe CodeGen -a="ServerAPI\bin\$env:AMBVARIANT\IServer.dll" -o=ServerInterfacesNet46 -f="net46" -b="CodeGenDependencies\net46"
& $env:AMBROSIATOOLS\$env:AMBVARIANT\AmbrosiaCS.exe CodeGen -a="ServerAPI\bin\$env:AMBVARIANT\IServer.dll" -a="IClient1\bin\$env:AMBVARIANT\IClient1.dll" -o=Client1InterfacesNet46 -f="net46" -b="CodeGenDependencies\net46"
& $env:AMBROSIATOOLS\$env:AMBVARIANT\AmbrosiaCS.exe CodeGen -a="ServerAPI\bin\$env:AMBVARIANT\IServer.dll" -a="IClient2\bin\$env:AMBVARIANT\IClient2.dll" -o=Client2InterfacesNet46 -f="net46" -b="CodeGenDependencies\net46"

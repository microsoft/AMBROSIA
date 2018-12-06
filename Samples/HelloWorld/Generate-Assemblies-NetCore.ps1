# Perform the code-generation step for this example application.

$env:AMBVARIANT="x64\Debug\netcoreapp2.0"

Write-Host "Using variant of CodeGen.exe: $env:AMBVARIANT"

Write-Host "Executing codegen command: dotnet D:\FranklinCurrent\CodeGen\bin\$env:AMBVARIANT\CodeGen.dll ServerAPI\bin\$env:AMBVARIANT\IServer.dll ServerInterfaces"

Write-Host "Executing codegen command: dotnet D:\FranklinCurrent\CodeGen\bin\$env:AMBVARIANT\CodeGen.dll ServerAPI\bin\$env:AMBVARIANT\IServer.dll Client1Interfaces"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
& dotnet "D:\FranklinCurrent\CodeGen\bin\$env:AMBVARIANT\CodeGen.dll" "ServerAPI\bin\$env:AMBVARIANT\IServer.dll" "ServerInterfaces"
& dotnet "D:\FranklinCurrent\CodeGen\bin\$env:AMBVARIANT\CodeGen.dll" "ServerAPI\bin\$env:AMBVARIANT\IServer.dll" "Client1Interfaces"

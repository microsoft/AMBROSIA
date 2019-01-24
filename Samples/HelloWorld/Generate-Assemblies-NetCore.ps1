# Perform the code-generation step for this example application.

if ( $env:AMBVARIANTCORE ) {
    $AMBVARIANTCORE=$env:AMBVARIANTCORE
} else {
    $AMBVARIANTCORE = "x64\Debug\netcoreapp2.0"
}

if ( $env:AMBVARIANTCORERELEASE ) {
    $AMBVARIANTCORERELEASE=$env:AMBVARIANTCORERELEASE
} else {
    $AMBVARIANTCORERELEASE = "x64\Release\netcoreapp2.0"
}

if ( $env:AMBROSIATOOLS ) {
    $AMBROSIATOOLS=$env:AMBROSIATOOLS
} else {
    $AMBROSIATOOLS = "..\..\Clients\CSharp\AmbrosiaCS\bin"
}

Write-Host "Using variant of AmbrosiaCS: $AMBVARIANTCORERELEASE"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
Write-Host "Executing codegen command: dotnet $AMBROSIATOOLS\$AMBVARIANTCORERELEASE\AmbrosiaCS.dll CodeGen -a=ServerAPI\bin\$AMBVARIANTCORE\IServer.dll -p=ServerAPI\IServer.csproj -o=ServerInterfaces -f=net46 -f=netcoreapp2.0"
& dotnet $AMBROSIATOOLS\$AMBVARIANTCORERELEASE\AmbrosiaCS.dll CodeGen -a="ServerAPI\bin\$AMBVARIANTCORE\IServer.dll" -p="ServerAPI\IServer.csproj" -o=ServerInterfaces -f="net46" -f="netcoreapp2.0"

Write-Host "Executing codegen command: dotnet $AMBROSIATOOLS\$AMBVARIANTCORERELEASE\AmbrosiaCS.dll CodeGen -a=ServerAPI\bin\$AMBVARIANTCORE\IServer.dll -a=IClient1\bin\$AMBVARIANTCORE\IClient1.dll -p=ServerAPI\IServer.csproj -p=IClient1\IClient1.csproj -o=Client1Interfaces -f=net46 -f=netcoreapp2.0"
& dotnet $AMBROSIATOOLS\$AMBVARIANTCORERELEASE\AmbrosiaCS.dll CodeGen -a="ServerAPI\bin\$AMBVARIANTCORE\IServer.dll" -a="IClient1\bin\$AMBVARIANTCORE\IClient1.dll" -p="ServerAPI\IServer.csproj" -p="IClient1\IClient1.csproj" -o=Client1Interfaces -f="net46" -f="netcoreapp2.0"

Write-Host "Executing codegen command: dotnet $AMBROSIATOOLS\$AMBVARIANTCORERELEASE\AmbrosiaCS.dll CodeGen -a=ServerAPI\bin\$AMBVARIANTCORE\IServer.dll -a=IClient2\bin\$AMBVARIANTCORE\IClient2.dll -p=ServerAPI\IServer.csproj -p=IClient2\IClient2.csproj -o=Client2Interfaces -f=net46 -f=netcoreapp2.0"
& dotnet $AMBROSIATOOLS\$AMBVARIANTCORERELEASE\AmbrosiaCS.dll CodeGen -a="ServerAPI\bin\$AMBVARIANTCORE\IServer.dll" -a="IClient2\bin\$AMBVARIANTCORE\IClient2.dll" -p="ServerAPI\IServer.csproj"  -p="IClient2\IClient2.csproj" -o=Client2Interfaces -f="net46" -f="netcoreapp2.0"

Write-Host "Executing codegen command: dotnet $AMBROSIATOOLS\$AMBVARIANTCORERELEASE\AmbrosiaCS.dll CodeGen -a=ServerAPI\bin\$AMBVARIANTCORE\IServer.dll -a=IClient3\bin\$AMBVARIANTCORE\IClient3.dll -p=ServerAPI\IServer.csproj -p=IClient3\IClient3.csproj -o=Client3Interfaces -f=net46 -f=netcoreapp2.0"
& dotnet $AMBROSIATOOLS\$AMBVARIANTCORERELEASE\AmbrosiaCS.dll CodeGen -a="ServerAPI\bin\$AMBVARIANTCORE\IServer.dll" -a="IClient3\bin\$AMBVARIANTCORE\IClient3.dll" -p="ServerAPI\IServer.csproj"  -p="IClient3\IClient3.csproj" -o=Client3Interfaces -f="net46" -f="netcoreapp2.0"
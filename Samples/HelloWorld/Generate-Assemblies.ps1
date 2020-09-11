# Perform the code-generation step for this example application.
if ( $env:AMBVARIANT ) {
    $AMBVARIANT = $env:AMBVARIANT
} else {
    $AMBVARIANT="x64\Debug\net46"
}

if ( $env:AMBVARIANTCORERELEASE ) {
    $AMBVARIANTCORERELEASE=$env:AMBVARIANTCORERELEASE
} else {
    $AMBVARIANTCORERELEASE = "x64\Release\netcoreapp3.1"
}

if ( $env:AMBROSIATOOLS ) {
    $AMBROSIATOOLS=$env:AMBROSIATOOLS
} else {
    $AMBROSIATOOLS = "..\..\Clients\CSharp\AmbrosiaCS\bin"
}

Write-Host "Using variant of AmbrosiaCS: $AMBVARIANT"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
Write-Host "Executing codegen command: $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a=ServerAPI\bin\Debug\netstandard2.0\IServer.dll -a=IClient3\bin\Debug\netstandard2.0\IClient3.dll -p=ServerAPI\IServer.csproj -p=IClient3\IClient3.csproj -o=ServerInterfaces -f=netstandard2.0"
& $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a="ServerAPI\bin\Debug\netstandard2.0\IServer.dll" -a="IClient3\bin\Debug\netstandard2.0\IClient3.dll" -p="ServerAPI\IServer.csproj" -p="IClient3\IClient3.csproj" -o=ServerInterfaces -f="netstandard2.0"

Write-Host "Executing codegen command: $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a=ServerAPI\bin\Debug\netstandard2.0\IServer.dll -a=IClient1\bin\Debug\netstandard2.0\IClient1.dll -p=ServerAPI\IServer.csproj -p=IClient1\IClient1.csproj -o=Client1Interfaces -f=netstandard2.0"
& $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a="ServerAPI\bin\Debug\netstandard2.0\IServer.dll" -a="IClient1\bin\Debug\netstandard2.0\IClient1.dll" -p="ServerAPI\IServer.csproj" -p="IClient1\IClient1.csproj" -o=Client1Interfaces -f="netstandard2.0"

Write-Host "Executing codegen command: $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a=ServerAPI\bin\Debug\netstandard2.0\IServer.dll -a=IClient2\bin\Debug\netstandard2.0\IClient2.dll -p=ServerAPI\IServer.csproj -p=IClient2\IClient2.csproj -o=Client2Interfaces -f=netstandard2.0"
& $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a="ServerAPI\bin\Debug\netstandard2.0\IServer.dll" -a="IClient2\bin\Debug\netstandard2.0\IClient2.dll" -p="ServerAPI\IServer.csproj" -p="IClient2\IClient2.csproj" -o=Client2Interfaces -f="netstandard2.0"

Write-Host "Executing codegen command: $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a=ServerAPI\bin\Debug\netstandard2.0\IServer.dll -a=IClient3\bin\Debug\netstandard2.0\IClient3.dll -p=ServerAPI\IServer.csproj -p=IClient3\IClient3.csproj -o=Client3Interfaces -f=netstandard2.0"
& $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a="ServerAPI\bin\Debug\netstandard2.0\IServer.dll" -a="IClient3\bin\Debug\netstandard2.0\IClient3.dll" -p="ServerAPI\IServer.csproj" -p="IClient3\IClient3.csproj" -o=Client3Interfaces -f="netstandard2.0"







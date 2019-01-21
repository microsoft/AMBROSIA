# Perform the code-generation step for this example application.
if ( $env:AMBVARIANT ) {
    $AMBVARIANT = $env:AMBVARIANT
} else {
    $AMBVARIANT="x64\Debug\net46"
}

if ( $env:AMBROSIATOOLS ) {
    $AMBROSIATOOLS=$env:AMBROSIATOOLS
} else {
    $AMBROSIATOOLS = "..\..\Clients\CSharp\AmbrosiaCS\bin"
}

Write-Host "Using variant of AmbrosiaCS: $AMBVARIANT"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
Write-Host "Executing codegen command: $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a=ServerAPI\bin\$AMBVARIANT\IServer.dll -o=ServerInterfaces -f=net46 -f=netcoreapp2.0"
& $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a="ServerAPI\bin\$AMBVARIANT\IServer.dll" -o=ServerInterfaces -f="net46" -f="netcoreapp2.0"

Write-Host "Executing codegen command: $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a=ServerAPI\bin\$AMBVARIANT\IServer.dll -a=IClient1\bin\$AMBVARIANT\IClient1.dll -o=Client1Interfaces -f=net46 -f=netcoreapp2.0"
& $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a="ServerAPI\bin\$AMBVARIANT\IServer.dll" -a="IClient1\bin\$AMBVARIANT\IClient1.dll" -o=Client1Interfaces -f="net46" -f="netcoreapp2.0"

Write-Host "Executing codegen command: $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a=ServerAPI\bin\$AMBVARIANT\IServer.dll -a=IClient2\bin\$AMBVARIANT\IClient2.dll -o=Client2Interfaces -f=net46 -f=netcoreapp2.0"
& $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a="ServerAPI\bin\$AMBVARIANT\IServer.dll" -a="IClient2\bin\$AMBVARIANT\IClient2.dll" -o=Client2Interfaces -f="net46" -f="netcoreapp2.0"

Write-Host "Executing codegen command: $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a=ServerAPI\bin\$AMBVARIANT\IServer.dll -a=IClient3\bin\$AMBVARIANT\IClient3.dll -o=Client3Interfaces -f=net46 -f=netcoreapp2.0"
& $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a="ServerAPI\bin\$AMBVARIANT\IServer.dll" -a="IClient3\bin\$AMBVARIANT\IClient3.dll" -o=Client3Interfaces -f="net46" -f="netcoreapp2.0"







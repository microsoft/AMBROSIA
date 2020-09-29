# Perform the code-generation step for this example application.
if ( $env:AMBVARIANT ) {
    $AMBVARIANT = $env:AMBVARIANT
} else {
    $AMBVARIANT="x64\Release\netstandard2.0"
}

if ( $env:AMBROSIATOOLS ) {
    $AMBROSIATOOLS=$env:AMBROSIATOOLS
} else {
    $AMBROSIATOOLS = "..\..\Clients\CSharp\AmbrosiaCS\bin"
}

Write-Host "Using variant of AmbrosiaCS: $AMBVARIANT"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
& $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a="ICommandShellImmortal\bin\Release\netstandard2.0\ICommandShellImmortal.dll" -p="ICommandShellImmortal\ICommandShellImmortal.csproj" -o="ICommandShellImmortalGenerated" -f="netstandard2.0"
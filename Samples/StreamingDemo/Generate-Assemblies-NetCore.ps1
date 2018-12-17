# Perform the code-generation step for this example application.

if ( $env:AMBVARIANTCORE ) {
    $AMBVARIANTCORE=$env:AMBVARIANTCORE
} else {
    $AMBVARIANTCORE = "x64\Debug\netcoreapp2.0"
}

# Create the dependencies folder
New-Item -ItemType Directory -Force -Path "CodeGenDependencies\netcoreapp2.0"
Get-ChildItem "CodeGenDependencies\netcoreapp2.0" | Remove-Item
Copy-Item "DashboardAPI\bin\Debug\netcoreapp2.0\publish\*" -Force -Destination "CodeGenDependencies\netcoreapp2.0\"
Copy-Item "AnalyticsAPI\bin\Debug\netcoreapp2.0\publish\*" -Force -Destination "CodeGenDependencies\netcoreapp2.0\"

Write-Host "Using variant of AmbrosiaCS.exe: $env:AMBVARIANT"

Write-Host "Executing codegen command: dotnet ..\..\Clients\CSharp\AmbrosiaCS\bin\$env:AMBVARIANT\AmbrosiaCS.dll CodeGen -a=DashboardAPI\bin\$env:AMBVARIANT\DashboardAPI.dll -o=DashboardAPIGeneratedNetCore -f=netcoreapp2.0 -b=CodeGenDependencies\netcoreapp2.0"

Write-Host "Executing codegen command: dotnet ..\..\Clients\CSharp\AmbrosiaCS\bin\$AMBVARIANT\AmbrosiaCS.dll CodeGen -a=DashboardAPI\bin\$AMBVARIANT\DashboardAPI.dll -o=DashboardAPIGenerated -f=net46 -f=netcoreapp2.0 -fb=net46;CodeGenDependencies\net46 -fb=netcoreapp2.0;CodeGenDependencies\netcoreapp2.0"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
& dotnet "..\..\Clients\CSharp\AmbrosiaCS\bin\$AMBVARIANT\AmbrosiaCS.dll" CodeGen -a="DashboardAPI\bin\$AMBVARIANT\DashboardAPI.dll" -o="DashboardAPIGenerated" -f="net46" -f="netcoreapp2.0" -fb="net46;CodeGenDependencies\net46" -fb="netcoreapp2.0;CodeGenDependencies\netcoreapp2.0"

Write-Host "Executing codegen command: ..\..\Clients\CSharp\AmbrosiaCS\bin\$AMBVARIANT\AmbrosiaCS.dll CodeGen -a=DashboardAPI\bin\$AMBVARIANT\DashboardAPI.dll -o=DashboardAPIGenerated -f=net46 -f=netcoreapp2.0 -fb=net46;CodeGenDependencies\net46 -fb=netcoreapp2.0;CodeGenDependencies\netcoreapp2.0"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
& dotnet "..\..\Clients\CSharp\AmbrosiaCS\bin\$AMBVARIANT\AmbrosiaCS.dll" CodeGen -a="DashboardAPI\bin\$AMBVARIANT\DashboardAPI.dll" -a="AnalyticsAPI\bin\$AMBVARIANT\AnalyticsAPI.dll" -o="AnalyticsAPIGenerated" -f="net46" -f="netcoreapp2.0" -fb="net46;CodeGenDependencies\net46" -fb="netcoreapp2.0;CodeGenDependencies\netcoreapp2.0"
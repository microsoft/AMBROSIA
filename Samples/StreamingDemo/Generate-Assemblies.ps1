# Perform the code-generation step for this example application.

if ( $env:AMBVARIANT ) {
    $AMBVARIANT = $env:AMBVARIANT
} else {
    $AMBVARIANT="x64\Debug\net46"
}

# Create the dependencies folder
New-Item -ItemType Directory -Force -Path "CodeGenDependencies\net46"
Get-ChildItem "CodeGenDependencies\net46" | Remove-Item
Copy-Item "DashboardAPI\bin\x64\Debug\net46\*" -Force -Destination "CodeGenDependencies\net46\"
Copy-Item "AnalyticsAPI\bin\x64\Debug\net46\*" -Force -Destination "CodeGenDependencies\net46\"

Write-Host "Using variant of AmbrosiaCS.exe: $env:AMBVARIANT"

Write-Host "Executing codegen command: ..\..\Clients\CSharp\AmbrosiaCS\bin\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a=DashboardAPI\bin\$AMBVARIANT\DashboardAPI.dll -o=DashboardAPIGenerated -f=net46 -fb=net46;CodeGenDependencies\net46"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
& "..\..\Clients\CSharp\AmbrosiaCS\bin\$AMBVARIANT\AmbrosiaCS.exe" CodeGen -a="DashboardAPI\bin\$AMBVARIANT\DashboardAPI.dll" -o="DashboardAPIGenerated" -f="net46" -fb="net46;CodeGenDependencies\net46"

Write-Host "Executing codegen command: ..\..\Clients\CSharp\AmbrosiaCS\bin\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a=DashboardAPI\bin\$AMBVARIANT\DashboardAPI.dll -a=AnalyticsAPI\bin\$env:AMBVARIANT\AnalyticsAPI.dll -o=AnalyticsAPIGenerated -f=net46 -fb=net46;CodeGenDependencies\net46"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
& "..\..\Clients\CSharp\AmbrosiaCS\bin\$AMBVARIANT\AmbrosiaCS.exe" CodeGen -a="DashboardAPI\bin\$AMBVARIANT\DashboardAPI.dll" -a="AnalyticsAPI\bin\$AMBVARIANT\AnalyticsAPI.dll" -o="AnalyticsAPIGenerated" -f="net46" -fb="net46;CodeGenDependencies\net46"
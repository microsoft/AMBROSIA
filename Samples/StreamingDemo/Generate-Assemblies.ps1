# Perform the code-generation step for this example application.

if (-not ( $env:AMBVARIANT )) {
    $env:AMBVARIANT="x64\Debug\net46"
}

# Create the dependencies folder
New-Item -ItemType Directory -Force -Path "CodeGenDependencies\net46"
Get-ChildItem "CodeGenDependencies\net46" | Remove-Item
Copy-Item "DashboardAPI\bin\$env:AMBVARIANT\*" -Force -Destination "CodeGenDependencies\net46\"
Copy-Item "AnalyticsAPI\bin\$env:AMBVARIANT\*" -Force -Destination "CodeGenDependencies\net46\"

Write-Host "Using variant of AmbrosiaCS.exe: $env:AMBVARIANT"

Write-Host "Executing codegen command: ..\..\Clients\CSharp\AmbrosiaCS\bin\$env:AMBVARIANT\AmbrosiaCS.exe CodeGen -a=DashboardAPI\bin\$env:AMBVARIANT\DashboardAPI.dll -o=DashboardAPIGeneratedNet46 -f=net46 -b=CodeGenDependencies\net46"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
& "..\..\Clients\CSharp\AmbrosiaCS\bin\$env:AMBVARIANT\AmbrosiaCS.exe" CodeGen -a="DashboardAPI\bin\$env:AMBVARIANT\DashboardAPI.dll" -o="DashboardAPIGeneratedNet46" -f="net46" -b="CodeGenDependencies\net46"

Write-Host "Executing codegen command: ..\..\Clients\CSharp\AmbrosiaCS\bin\$env:AMBVARIANT\AmbrosiaCS.exe CodeGen -a=DashboardAPI\bin\$env:AMBVARIANT\DashboardAPI.dll -a=AnalyticsAPI\bin\$env:AMBVARIANT\AnalyticsAPI.dll -o=AnalyticsAPIGeneratedNet46 -f=net46 -b=CodeGenDependencies\net46"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
& "..\..\Clients\CSharp\AmbrosiaCS\bin\$env:AMBVARIANT\AmbrosiaCS.exe" CodeGen -a="DashboardAPI\bin\$env:AMBVARIANT\DashboardAPI.dll" -a="AnalyticsAPI\bin\$env:AMBVARIANT\AnalyticsAPI.dll" -o="AnalyticsAPIGeneratedNet46" -f="net46" -b="CodeGenDependencies\net46"

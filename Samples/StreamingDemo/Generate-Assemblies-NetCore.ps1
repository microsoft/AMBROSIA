# Perform the code-generation step for this example application.

if (-not ( $env:AMBVARIANT )) {
    $env:AMBVARIANT="x64\Debug\netcoreapp2.0"
}

# Build the API projects
& dotnet publish "DashboardAPI\DashboardAPI.csproj" -f "netcoreapp2.0"
& dotnet publish "AnalyticsAPI\AnalyticsAPI.csproj" -f "netcoreapp2.0"

# Create the dependencies folder
New-Item -ItemType Directory -Force -Path "CodeGenDependencies\netcoreapp2.0"
Get-ChildItem "CodeGenDependencies\netcoreapp2.0" | Remove-Item
Copy-Item "DashboardAPI\bin\Debug\netcoreapp2.0\publish\*" -Force -Destination "CodeGenDependencies\netcoreapp2.0\"
Copy-Item "AnalyticsAPI\bin\Debug\netcoreapp2.0\publish\*" -Force -Destination "CodeGenDependencies\netcoreapp2.0\"

Copy-Item "..\..\Clients\CSharp\AmbrosiaCS\AmbrosiaCS.csproj" -Force -Destination "CodeGenDependencies\netcoreapp2.0\"

Write-Host "Using variant of AmbrosiaCS.exe: $env:AMBVARIANT"

Write-Host "Executing codegen command: dotnet ..\..\Clients\CSharp\AmbrosiaCS\bin\$env:AMBVARIANT\AmbrosiaCS.dll CodeGen -a=DashboardAPI\bin\$env:AMBVARIANT\DashboardAPI.dll -o=DashboardAPIGeneratedNetCore -f=netcoreapp2.0 -b=CodeGenDependencies\netcoreapp2.0"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
& dotnet "..\..\Clients\CSharp\AmbrosiaCS\bin\$env:AMBVARIANT\AmbrosiaCS.dll" CodeGen -a="DashboardAPI\bin\Debug\netcoreapp2.0\publish\DashboardAPI.dll" -o="DashboardAPIGeneratedNetCore" -f="netcoreapp2.0" -b="CodeGenDependencies\netcoreapp2.0"

Write-Host "Executing codegen command: dotnet ..\..\Clients\CSharp\AmbrosiaCS\bin\$env:AMBVARIANT\AmbrosiaCS.dll CodeGen -a=DashboardAPI\bin\$env:AMBVARIANT\DashboardAPI.dll -a=AnalyticsAPI\bin\$env:AMBVARIANT\AnalyticsAPI.dll -o=AnalyticsAPIGeneratedNetCore -f=netcoreapp2.0 -b=CodeGenDependencies\netcoreapp2.0"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
& dotnet "..\..\Clients\CSharp\AmbrosiaCS\bin\$env:AMBVARIANT\AmbrosiaCS.dll" CodeGen -a="DashboardAPI\bin\Debug\netcoreapp2.0\publish\DashboardAPI.dll" -a="AnalyticsAPI\bin\Debug\netcoreapp2.0\publish\AnalyticsAPI.dll" -o="AnalyticsAPIGeneratedNetCore" -f="netcoreapp2.0" -b="CodeGenDependencies\netcoreapp2.0"
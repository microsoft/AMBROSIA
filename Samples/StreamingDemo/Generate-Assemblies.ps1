# Perform the code-generation step for this example application.
if ( $env:AMBVARIANT ) {
    $AMBVARIANT = $env:AMBVARIANT
} else {
    $AMBVARIANT="x64\Debug\net461"
}

if ( $env:AMBROSIATOOLS ) {
    $AMBROSIATOOLS=$env:AMBROSIATOOLS
} else {
    $AMBROSIATOOLS = "..\..\Clients\CSharp\AmbrosiaCS\bin"
}

Write-Host "Using variant of AmbrosiaCS: $AMBVARIANT"

# Generate the assemblies, assumes an .exe which is created by a .Net Framework build:
Write-Host "Executing codegen command: $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a=DashboardAPI\bin\Release\net461\DashboardAPI.dll -p=DashboardAPI\DashboardAPI.csproj -o=DashboardAPIGenerated -f=net461"
& $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a="DashboardAPI\bin\Release\net461\DashboardAPI.dll" -p="DashboardAPI\DashboardAPI.csproj" -o="DashboardAPIGenerated" -f="net461"

Write-Host "Executing codegen command: $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a=DashboardAPI\bin\$AMBVARIANT\DashboardAPI.dll -a=AnalyticsAPI\bin\$AMBVARIANT\AnalyticsAPI.dll -p=DashboardAPI\DashboardAPI.csproj -p=AnalyticsAPI\AnalyticsAPI.csproj -o=AnalyticsAPIGenerated -f=net461"
& $AMBROSIATOOLS\$AMBVARIANT\AmbrosiaCS.exe CodeGen -a="DashboardAPI\bin\Release\net461\DashboardAPI.dll" -a="AnalyticsAPI\bin\Release\net461\AnalyticsAPI.dll" -p="DashboardAPI\DashboardAPI.csproj" -p="AnalyticsAPI\AnalyticsAPI.csproj" -o="AnalyticsAPIGenerated" -f="net461"
@echo off
set BuildConfig=Release
if "%1" == "debug" set BuildConfig=Debug

dotnet publish -o /ambrosia/x64/Release/netcoreapp2.0 -c %BuildConfig% -f netcoreapp2.0 -r win10-x64 Ambrosia/Ambrosia/Ambrosia.csproj
dotnet publish -o /ambrosia/x64/Release/netcoreapp2.0 -c %BuildConfig% -f netcoreapp2.0 -r win10-x64 ImmortalCoordinator/ImmortalCoordinator.csproj
dotnet publish -o /ambrosia/x64/Release/netcoreapp2.0 -c %BuildConfig% -f netcoreapp2.0 -r win10-x64 Clients/CSharp/AmbrosiaCS/AmbrosiaCS.csproj
dotnet publish -o /ambrosia/x64/Release/netcoreapp2.0 -c %BuildConfig% -f netcoreapp2.0 -r win10-x64 Clients/CSharp/AmbrosiaLibCS/AmbrosiaLibCS.csproj
dotnet publish -o /ambrosia/x64/Release/netcoreapp2.0 -c %BuildConfig% -f netcoreapp2.0 -r win10-x64 DevTools/UnsafeDeregisterInstance/UnsafeDeregisterInstance.csproj

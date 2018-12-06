dotnet publish -o /ambrosia/ambrosia/bin/x64/Release/netcoreapp2.0 -c Release -f netcoreapp2.0 -r win10-x64 Ambrosia/Ambrosia/Ambrosia.csproj
dotnet publish -o /ambrosia/ambrosia/bin/x64/Release/netcoreapp2.0 -c Release -f netcoreapp2.0 -r win10-x64 ImmortalCoordinator/ImmortalCoordinator.csproj

dotnet publish -o /ambrosia/ambrosia/bin/x64/Release/netcoreapp2.0 -c Release -f netcoreapp2.0 -r win10-x64 Clients/CSharp/AmbrosiaCS/AmbrosiaCS.csproj
dotnet publish -o ./bin -c Release -f netcoreapp2.0 -r win10-x64 DevTools/UnsafeDeregisterInstance/UnsafeDeregisterInstance.csproj

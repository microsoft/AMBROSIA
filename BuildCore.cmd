dotnet publish -o %AMBROSIATOOLS%\x64\Release\netcoreapp2.0 -c Debug -f netcoreapp2.0 -r win10-x64 Ambrosia/Ambrosia/Ambrosia.csproj
dotnet publish -o %AMBROSIATOOLS%\x64\Release\netcoreapp2.0 -c Debug -f netcoreapp2.0 -r win10-x64 ImmortalCoordinator/ImmortalCoordinator.csproj
dotnet publish -o %AMBROSIATOOLS%\x64\Release\netcoreapp2.0 -c Debug -f netcoreapp2.0 -r win10-x64 Clients/CSharp/AmbrosiaLibCS/AmbrosiaLibCS.csproj
dotnet publish -o %AMBROSIATOOLS%\x64\Release\netcoreapp2.0 -c Debug -f netcoreapp2.0 -r win10-x64 Clients/CSharp/AmbrosiaCS/AmbrosiaCS.csproj
dotnet publish -o %AMBROSIATOOLS%\x64\Release\netcoreapp2.0 -c Debug -f netcoreapp2.0 -r win10-x64 DevTools/UnsafeDeregisterInstance/UnsafeDeregisterInstance.csproj

dotnet publish -o %AMBROSIATOOLS%\x64\Release\net46 -c Debug -f net46 -r win10-x64 Ambrosia/Ambrosia/Ambrosia.csproj
dotnet publish -o %AMBROSIATOOLS%\x64\Release\net46 -c Debug -f net46 -r win10-x64 ImmortalCoordinator/ImmortalCoordinator.csproj
dotnet publish -o %AMBROSIATOOLS%\x64\Release\net46 -c Debug -f net46 -r win10-x64 Clients/CSharp/AmbrosiaLibCS/AmbrosiaLibCS.csproj
dotnet publish -o %AMBROSIATOOLS%\x64\Release\net46 -c Debug -f net46 -r win10-x64 Clients/CSharp/AmbrosiaCS/AmbrosiaCS.csproj
dotnet publish -o %AMBROSIATOOLS%\x64\Release\net46 -c Debug -f net46 -r win10-x64 DevTools/UnsafeDeregisterInstance/UnsafeDeregisterInstance.csproj

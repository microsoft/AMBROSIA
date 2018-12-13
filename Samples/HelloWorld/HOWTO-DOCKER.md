



Building and Running: Docker
----------------------------

docker run -it --rm --env "AZURE_STORAGE_CONN_STRING=$AZURE_STORAGE_CONN_STRING" \
       --env  --env "AMBROSIA_IMMORTALCOORDINATOR_PORT=1600" \
       ambrosia-hello runAmbrosiaService.sh dotnet Client2/publish/Client2.dll $CNAME $SNAME


Building and Running: Windows / Visual Studio
---------------------------------------------

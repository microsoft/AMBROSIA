
AMBROSIA Sample Application: Hello World
========================================

This application ....

   <FINISHME>

Building and Running: CLI and .NET Core
---------------------------------------

To build everything, make sure the AMBROSIA binary distrubition is in
your PATH (e.g. `which AmbrosiaCS`) and run the script:

	./build_dotnetcore.sh

Now you have binaries built under the paths `Server/publish`,
`Client1/publish`, and `Client2/publish`.  The two clients are
*different* examples, and only one or the other should be run at a time.


Before we can run the client/server, we need to register metadata
about these AMBROSIA instances with in the cloud table storage.  Pick
a name for your client and server instances.

    Ambrosia RegisterInstance -i myclient --rp 2000 --sp 2001 -l locallogs
    Ambrosia RegisterInstance -i myserver --rp 2000 --sp 2001 -l locallogs	

We've told AMBROSIA that it will use `./locallogs` for storing logs,
but in a production environment of course logs would need to be on a
remotely-mounted file system that is durable even when the machine fails.

First let's run the server.  Open a terminal, and let's set up some of
the configuration information that will be used by the
`runAmbrosiaService.sh` script to launch your process.

    export AMBROSIA_INSTANCE_NAME=myclient
	export AMBROSIA_IMMORTALCOORDINATOR_PORT=1500
	export AZURE_STORAGE_CONN_STRING=...

To launch a service

docker run -it --rm --env "AZURE_STORAGE_CONN_STRING=$AZURE_STORAGE_CONN_STRING" \
       --env  --env "AMBROSIA_IMMORTALCOORDINATOR_PORT=1600" \
       ambrosia-hello runAmbrosiaService.sh dotnet Client2/publish/Client2.dll $CNAME $SNAME


Building and Running: Docker
----------------------------




Building and Running: Windows / Visual Studio
---------------------------------------------


   <FINISHME>  - move from AmbrosiaDocs.md??



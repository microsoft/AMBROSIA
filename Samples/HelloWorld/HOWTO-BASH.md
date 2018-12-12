
Building and Running: dotnet CLI + Bash
=======================================

This tutorial assumes you are using a Bash shell together with the
`dotnet` CLI (2.0 or greater).  The commands here should work on Mac
OS, Linux, and Windows.

Building
--------

To build everything, make sure the AMBROSIA binary distrubition is in
your PATH (e.g. `which AmbrosiaCS`) and run the script:

	./build_dotnetcore.sh

Now you have binaries built under the paths `Server/publish`,
`Client1/publish`, and `Client2/publish`.  The two clients are
*different* examples, and only one or the other should be run at a time.


Registering
-----------

Before we can run the client/server, we need to register metadata
about these AMBROSIA instances with in the cloud table storage.  Pick
a name for your client and server instances.

    Ambrosia RegisterInstance -i myclient --rp 2000 --sp 2001 -l ./logs
    Ambrosia RegisterInstance -i myserver --rp 2000 --sp 2001 -l ./logs	

We've told AMBROSIA that it will use `./logs` for storing logs locally,
but in a production environment of course logs would need to be on a
remotely-mounted file system that is durable even when the machine fails.


Running
-------

First let's run the server.  Open a terminal, and let's set up some of
the configuration information that will be used by the
`runAmbrosiaService.sh` script to launch your process.

    export AMBROSIA_INSTANCE_NAME=myclient
	export AMBROSIA_IMMORTALCOORDINATOR_PORT=1500
	export AZURE_STORAGE_CONN_STRING=...

To launch a service we're going to use a convenience script called
`runAmbrosiaService.sh` which is included in the binary distribution
of AMBROSIA.  This handles starting the immortal coordinator and
monitorying its health.

	runAmbrosiaService.sh 

You could start ImmortalCoordinator yourself, as well, by using two
separate terminals to run:


Cleanup
-------


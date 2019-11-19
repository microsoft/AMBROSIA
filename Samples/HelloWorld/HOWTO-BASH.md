
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

Running
-------

### Super quickstart for the impatient

If you're feeling lucky, you can try running the setting
AZURE_STORAGE_CONN_STRING and then running two communicating services
locally in one terminal like so:

    ./run_helloworld_both.sh

Or you can tease it apart and open the client and server separately,
in separate terminal windows:

	./run_helloworld_client.sh
	./run_helloworld_server.sh	

After you run, you'll want to cleanup the logs (`ambrosia_logs/`)
before running again, or the system will think it's recovering from a
failure and still part of the previous run.

### Longer version

In order to develop your own AMBROSIA services we'll need to walk
through the steps in a bit more detail.  There are three main steps.

#### (Steps 1/3) Registering

Before we can run the client/server, we need to register metadata
about these AMBROSIA instances with in the cloud table storage.  Pick
a name for your client and server instances.

    Ambrosia RegisterInstance -i myclient --rp 2000 --sp 2001 -l ./ambrosia_logs
    Ambrosia RegisterInstance -i myserver --rp 3000 --sp 3001 -l ./ambrosia_logs

We've told AMBROSIA that it will use `./ambrosia_logs` for storing
logs locally, but in a production environment of course logs would
need to be on a remotely-mounted file system that is durable even when
the machine fails.

#### (Step 2/3) Running an instance.

First let's run the server.  Open a terminal, and let's set up some of
the configuration information that will be used by the
`runAmbrosiaService.sh` script to launch your process.

    export AMBROSIA_INSTANCE_NAME=myserver
	export AMBROSIA_IMMORTALCOORDINATOR_PORT=3500
	export AZURE_STORAGE_CONN_STRING=...

To launch a service we're going to use a convenience script called
`runAmbrosiaService.sh` which is included in the binary distribution
of AMBROSIA.  This handles starting the immortal coordinator and
monitorying its health.

	runAmbrosiaService.sh dotnet Server/publish/Server.dll myserver

You'll see a lot of output, with output from the coordinator tagged
`[ImmortalCoord]`.  Eventually, the coordinator reports "Ready" and 

Alternatively, you could start ImmortalCoordinator yourself, by using
two separate terminals to run:

    ImmortalCoordinator -i myserver -p 3500
    dotnet Server/publish/Server.dll myserver

#### Running another instance.

Now you have the server running, but for this to be interesting, we
need another client to connect to the server.

    export AMBROSIA_INSTANCE_NAME=myclient
	export AMBROSIA_IMMORTALCOORDINATOR_PORT=2500
	export AZURE_STORAGE_CONN_STRING=...
	runAmbrosiaService.sh dotnet Client1/Publish/Client1.dll myclient myserver

#### (Step 3/3) Cleanup

To delete all the metadata we left in the cloud, run the following:

	 UnsafeDeregisterInstance myclient
	 UnsafeDeregisterInstance myserver

Note this is called "unsafe" because one must take great care to not
call it while any part of the service may still be running.


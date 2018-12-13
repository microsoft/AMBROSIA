
AMBROSIA on Kubernetes, README & Quick Start
============================================

This directory contains scripts for launching an AMBROSIA service on a
Kubernetes cluster, and in particular, an Azure Kubernetes (AKS) cluster.

These scripts are launched from a *development machine*, which is
currently assumed to be running Linux, but which in the future may
allow the same Bash scripts to be run on Windows or Mac.  Running the
scripts will locally build Docker containers before pushing them to
the cloud, and thus it assumes some prerequisites:

  * Azure CLI 2.0
  * Kubernetes CLI (kubectl)
  * Docker command line tools

The main entrypoint is "run-end-to-end-perftest-example.sh", which is designed to
be *modified* to suite your application.  It is initially configured
to build and deploy the InternalImmortals/PerformanceTestInterruptable
application, which consists of two pods that communicate with
eachother to test the performance of the RPC channel.

The other scripts in this directory automate various aspects of
deploying on AKS, including the authentication steps.  It is designed
to use with a fresh Azure resource group.

Step 1: configure your deployment
---------------------------------

Use the provided template:

    cp Defs/AmbrosiaAKSConf.sh.template Defs/AmbrosiaAKSConf.sh
	$EDITOR Defs/AmbrosiaAKSConf.sh

Fill in your Azure subscription identifier and select the name of a
new resource group which will be created.  (It is best to isolate this
from other Azure resources you may have running.)

Step 2: provision and run
-------------------------
	
    ./run-end-to-end-perftest-example.sh
	
That's it!  The first time this script runs it will take a long time
to provision the storage account, container registry, file share, and
Kubernetes cluster.  Running it again is safe, and it will run faster
once these things have already been created.

At the end of all that `kubectl get pods` should show two running
pods.  Note that while the steps taken by `./run-end-to-end-perftest-example.sh`
should be idempotent, it does *not* provide safe "incremental builds"
when things change.  Therefore you should take it upon yourself to
clean up when modifying the Azure/AKS-related (e.g. the contents of
AmbrosiaAKSConf.sh):

    ./Clean-AKS.sh <all|most|auth>

In contrast, changes to the *application* logic -- confined to the
application Dockerfile and resulting Docker container -- do not
require cleaning.  They can be rerun on the previously provisioned
Azure resources, simply by reexecuting the
run-end-to-end-perftest-example script.

Note if you are RERUNNING this PerformanceTestInterruptable or another
application then you typically want to *delete* the logs that are
mounted into the Azure Files SMB share (mounted at `/ambrosia_logs/`
by default).  You can do this manually, or `Clean-AKS.sh most` is
sufficient to do it.

Step 3: viewing the output
--------------------------

Use the name shown in `kubectl get pods` to print the logs of the client container:

    kubectl logs -f generated-perftestclient-******* 

Eventually this will show lines of outputs that contain performance measurements:

    *X* 65536       0.0103833476182065
    *X* 32768       0.00985864082367517

These show throughput in GiB/s for a given message size.  When you're
done, you can use `Clean-AKS.sh all` to delete the entire resource
group (or do it yourself in the Azure web portal).

Step 4: testing virtual resiliency, aka IMMORTALITY
---------------------------------------------------

Ambrosia, being the nectar of the gods, confers immortality on the
processes running on it.  What this means is that if you kill a
container/pod, it will be able to restart and use durable storage to
recover exactly the state it was at before.

In order to demonstrate this with the sample application we will
emulate a crashed machine by killing the client application.  Start
the application, then, while it is running, issue this command inside
the container to kill the client:

    kubectl exec -it generated-perftestclient-******* bash 
	kill $(pidof Job)

If you look at `kubectl get pods --watch`, you will see Kubernetes
attempting to automatically restart the container, which will itself
go down as soon as the main executable (Job) is killed.  

(Note, perftestclient may actually take several tries to start back
up, because the quick attempts to restart in the same pod hit an
`System.Net.Sockets.SocketException: Address already in use` error.
A real failure that brought down a machine, and thus one or more
pods, would not have this problem.)


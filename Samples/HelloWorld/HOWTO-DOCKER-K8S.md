
Building and Running: Docker, Kubernets, AKS
============================================


Running HelloWorld on Docker Locally
------------------------------------

For this example you need the "ambrosia" Docker image.  You can pull
it from Dockerhub, along with this hello-world example:

	docker pull ambrosia/ambrosia
    docker pull ambrosia/ambrosia-hello

Or you can build them locally by checking out the source:

    git clone git@github.com:Microsoft/AMBROSIA
    cd AMBROSIA
    ./build_docker_images.sh
    cd Samples/HelloWorld
    docker build -t ambrosia/ambrosia-hello . 

Test it out by executing `docker run -it --rm ambrosia/ambrosia-hello`.
After that, set `$AZURE_STORAGE_CONN_STRING` based on the value
provided under the Azure Portal (Resource group -> Storage account ->
Access keys).

    export AZURE_STORAGE_CONN_STRING=...

Now you're ready to run the HelloWorld.  You can run both client and
server in one container:

    docker run -it --rm --env "AZURE_STORAGE_CONN_STRING=$AZURE_STORAGE_CONN_STRING" ambrosia/ambrosia-hello ./run_helloworld_both.sh

Or you can run two containers that communicate with eachother.  First
set AZURE_STORAGE_CONN_STRING and register the instances by hand using
`Ambrosia RegisterInstance`.  This can be run on the host machine or
inside a Docker container.

    Ambrosia RegisterInstance -i myclient --rp 1000 --sp 1001 -l ./ambrosia_logs
    Ambrosia RegisterInstance -i myserver - -rp 2000 --sp 2001 -l ./ambrosia_logs

Then open up two terminals, and spawn the server container:

    docker run -it --rm --env "AZURE_STORAGE_CONN_STRING=$AZURE_STORAGE_CONN_STRING" ambrosia/ambrosia-hello ./run_helloworld_server.sh

Followed by the client container:

    docker run -it --rm --env "AZURE_STORAGE_CONN_STRING=$AZURE_STORAGE_CONN_STRING" ambrosia/ambrosia-hello ./run_helloworld_client.sh

It will send a message and then wait for input.  Press a key on the
client container to continue.  The purpose of waiting on keyboard
input is to give you a moment to kill (and recover) the process if
desired.


Running HelloWorld on Kubernetes using AKS
------------------------------------------

We can take the same Docker container we used above and deploy into a
Kubernetes cluster in the cloud.  In this example we use the
Azure Kubernetes Service (AKS).  

For this step, you'll need a full source checkout of the AMBROSIA
repository (e.g. use the git clone command above).  Within that
working copy, should read through the documentation [in the
AKS-scripts directory](../AKS-scripts) before continuing here.

Back? Ok, let's proceed.

After you've populated AmbrosiaAKSConf.sh and have things working
within the AKS-scripts directory.  Within that config file, set:

     AMBROSIA_SERVICE_NAME=hello

And change any other parameters you like.  After that, run this script
from the AKS-scripts directory:

    ./run-end-to-end-helloworld-example.sh

After it completes successfully, you should see two pods deploying:

    kubectl get pods

    


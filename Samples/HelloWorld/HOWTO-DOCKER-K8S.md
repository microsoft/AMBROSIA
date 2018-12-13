
Building and Running: Docker, Kubernets, AKS
============================================


Running HelloWorld on Docker Locally
------------------------------------

For this example you need the "ambrosia" Docker image.  You can pull
it from Dockerhub:

	docker pull ambrosia/ambrosia

Or you can build it locally by checking out the source:

    git clone git@github.com:Microsoft/AMBROSIA
    cd AMBROSIA
    ./build_docker_images.sh

Test it out by executing `docker run -it --rm ambrosia/ambrosia`.  Now
you're ready to build the HelloWorld example in this directory:

    docker build -t ambrosia-hello . 

With that, you can run the example in one container:

    docker run -it --rm --env "AZURE_STORAGE_CONN_STRING=$AZURE_STORAGE_CONN_STRING" ./run_helloworld_both.sh

Or you can run two containers that communicate with eachother.  First
set AZURE_STORAGE_CONN_STRING and register the instances (locally or
inside the ambrosia container):

    Ambrosia RegisterInstance -i myclient --rp 1000 --sp 1001 -l ./ambrosia_logs
    Ambrosia RegisterInstance -i myserver - -rp 2000 --sp 2001 -l ./ambrosia_logs

Then open up two terminals, and spawn the server container:

    docker run -it --rm --env "AZURE_STORAGE_CONN_STRING=$AZURE_STORAGE_CONN_STRING" ambrosia-hello ./run_helloworld_server.sh

Followed by the client container:

    docker run -it --rm --env "AZURE_STORAGE_CONN_STRING=$AZURE_STORAGE_CONN_STRING" ambrosia-hello ./run_helloworld_client.sh

Press a key on the client container to continue.


Running HelloWorld on Kubernetes using AKS
------------------------------------------

We can take the same Docker containers we used above and deploy them
into a Kubernetes cluster in the cloud.  In this example we use the
Azure Kubernetes Service (AKS).  

For this step, you'll need a full source checkout of the AMBROSIA
repository (e.g. use the git clone command above).  Within that
working copy, should read through the documentation [in the
AKS-scripts directory](../../AKS-scripts) before continuing here.

Back? Ok, let's proceed.

After you've populated AmbrosiaAKSConf.sh and have things working
within the AKS-scripts directory.  Within that config file, set:

     AMBROSIA_SERVICE_NAME=hello

And now:

    ./run_helloworld_aks.sh


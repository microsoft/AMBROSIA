# AMBROSIA Documentation
## Concepts
What is Ambrosia? What guarantees does it provide?

### Immortals
The basic building blocks of AMBROSIA are *Immortals*, reliable distributed
objects that communicate through RPCs. An Immortal defines a set of persistent
state and a set of RPC handlers that operate on that state. An *instance* of an
Immortal is a named entity that maintains state and executes RPC handlers
according to the Immortal's definition. An AMBROSIA application often has
multiple instances of the same Immortal; for example, an application may define
a single "job" Immortal for running a data-processing job and run multiple
instances of that job operating on different data sets.

### Architecture
The figure below outlines the basic architecture of an AMBROSIA application.
Each box in the figure represents a separate process. Each instance of an
Immortal exists as a software object and thread of control running inside of
an application process. An Immortal instance communicates with other Immortal
instances through an *Immortal Coordinator* process, which durably logs the
instance's RPCs and encapsulates the low-level networking required to send
RPCs.

     ----------------------             ----------------------
    |                      |           |                      |
    | Immortal coordinator |<=========>| Immortal coordinator |
    |                      |           |                      |
     ----------------------             ----------------------
              ||                                 ||
              ||                                 ||
              ||                                 ||
     ----------------------             ----------------------
    |                      |           |                      |
    |  Immortal instance   |           |  Immortal instance   |
    |                      |           |                      |
     ----------------------             ----------------------

## Requirements/Platforms supported for development and deployment

FINISHME - 

## Setup
How to get set up to use Ambrosia

## Compiling and running a "Hello World" Ambrosia application
This section describes how to compile and run a simple Ambrosia application
on .NET Core using the HelloWorld sample project as an example.

For the purpose of this tutorial, we'll assume the following parameters:

* Log directory: C:\logs\
* Client instance name: client1
* Client ImmortalCoordinator receive port: 1000
* Client ImmortalCoordinator send port: 1001
* Client ImmortalCoordinator CRA port: 1500
* Server instance name: server1
* Server ImmortalCoordinator receive port: 2000
* Server ImmortalCoordinator send port: 2001
* Server ImmortalCoordinator CRA port: 2500

When running the commands below, replace `$AMBROSIA` with the directory
containing the Ambrosia executables, and replace `$HELLO_WORLD` with the
directory containing the HelloWorld solution.

### Compiling the application
To compile HelloWorld, open `HelloWorld.sln` in Visual Studio. Set the build
configuration to `Debug` and the platform to `x64`, and build the solution.

### Registering the Immortal instances
Before running the application, you need to register each Immortal instance
so that other Immortal instances can find them. You'll do so using the
`Ambrosia` executable. Open a command prompt and enter the following commands.

    cd $AMBROSIA
    dotnet .\Ambrosia.dll RegisterInstance -i=client1 -rp=1000 -sp=1001 -l=C:\logs\
    dotnet .\Ambrosia.dll RegisterInstance -i=server1 -rp=2000 -sp=2001 -l=C:\logs\

### Running the application
To run the HelloWorld application, you will need to run four command-line
processes, each in a separate window: the HelloWorld client Immortal, the
HelloWorld server Immortal, and two ImmortalCoordinator processes, one for
each Immortal.

To run the client ImmortalCoordinator, open a command prompt and enter these
commands:

    cd $AMBROSIA
    dotnet ImmortalCoordinator.dll --instanceName=client1 --port=1500

To run the server ImmortalCoordinator:

    cd $AMBROSIA
    dotnet ImmortalCoordinator.dll --instanceName=server1 --port=2500

To run the HelloWorld client:

    cd $HELLO_WORLD\Client1\bin\x64\Debug\netcoreapp2.0
    dotnet Client1.dll

To run the HelloWorld server:

    cd $HELLO_WORLD\Server\bin\x64\Debug\netcoreapp2.0
    dotnet Server.dll

After starting all four processes, you should see your client and server
communicate with each other!

### Code generation
The HelloWorld sample contains generated proxy and dispatcher classes for each
of its Immortals that were created by the Ambrosia code generation tool,
`AmbrosiaCS`. If you change an Immortal's public interface (e.g., adding
parameters to `IServer.ReceiveMessage()`), you will need to re-run the code
generation tool to update the generated source files. The sample's root
directory contains a PowerShell script to automate the process of invoking
`AmbrosiaCS`. To run code generation, first build any projects containing
updated Immortal interfaces (in this example, the `ServerAPI` project). Then
open a PowerShell prompt and enter the following commands:

    cd $HELLO_WORLD
    .\Generate-Assemblies-NetCore.ps1

## Developing with Ambrosia
FINISHME - 

small snippet example of how to send messages through ambrosia
how to handle impulses
recovery, active active
live service upgrades
time travel debugging how to
portability

## Deployment of services built on Ambrosia
FINISHME - Windows, Linux

## Best Practices
FINISHME - how to ensure you get all the guarantees that Ambrosia provides

## Examples

FINISHME - 
Brief description of each example and what it demonstrates
instructions for cloning/downloading and modifying examples

## References
FINISHME -  configurations, command line options, parameters

## Roadmap

FINISHME - 
planned future features
other language support
support non-Azure storage

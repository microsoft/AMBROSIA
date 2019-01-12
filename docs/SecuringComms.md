# Securing Ambrosia Communications

This document describes how deployers of the Immortal Coordinator may secure all communications between coordinators. Immortal
Coordinators communicate with each using a reliable network connection library called [CRA](https://github.com/Microsoft/CRA).
CRA uses standard unsecured TCP connections by default. If the deployer wants to secure all communications between Immortal 
Coordinators, they can do so by writing a _communication wrapper_ and providing this information to the coordinator. The deployer
need to provide two pieces of information related to securing connections: an assemby name and an assembly class name. Together, 
this information points to an assembly that implements an interface that is called whenever a TCP connection is created between two
Immortal Coordinators. The interface is called `ISecureStreamConnectionDescriptor`, and is shown [here](https://github.com/Microsoft/CRA/blob/master/src/CRA.ClientLibrary/Security/ISecureStreamConnectionDescriptor.cs).

For example, suppose we wish to use a dummy security wrapper that simply passes the stream without securing it. See 
[here](https://github.com/Microsoft/CRA/blob/master/src/CRA.ClientLibrary/Security/DummySecureStreamConnectionDescriptor.cs)
for such an example. We would provide the information to Ambrosia when invoking the Immortal Coordinator, as follows.

    dotnet ImmortalCoordinator.dll --instanceName=client1 --port=1500 -an=CRA.ClientLibrary -ac=CRA.ClientLibrary.DummySecureStreamConnectionDescriptor
    dotnet ImmortalCoordinator.dll --instanceName=server1 --port=2500 -an=CRA.ClientLibrary -ac=CRA.ClientLibrary.DummySecureStreamConnectionDescriptor

Another example of a security wrapper is provided [here](https://github.com/Microsoft/CRA/blob/master/src/CRA.ClientLibrary/Security/SampleSecureStreamConnectionDescriptor.cs). 
This example uses an X509 Certificate to create SslStream wrappers around the TCP connections.

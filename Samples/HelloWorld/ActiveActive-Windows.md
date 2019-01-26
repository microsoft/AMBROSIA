High Availability Using Active Standbys with Hello World
========================================

UNDER CONSTRUCTION: This is not quite right yet and some very minor changes need to be made.

In Ambrosia, high availability is achieved by running multiple copies of an instance, including one primary which accepts requests, which is responsible for writing the log, and other secondary instances, which remain in recovery, reading any additions to the log made by the primary, until the primary fails. When the primary fails, one of the secondaries becomes the new primary, taking control of all connections to other instances, and also becoming the log writer. All of this is done without modification to the instance code, and with a guarantee of no data loss. Failover times are typically less than a second.

In this walkthrough, we make the server in HelloWorld highly available by running three copies, one of which becomes the primary, and two of which become secondaries. We kill the primary server and observe one of the secondaries becoming primary. This may be repeated to ping-pong the primary responsiblity around. 

This walkthrough assumes that readers have already read [HOWTO-WINDOWS.md](./HOWTO-WINDOWS.md) and [HelloWorldExplained.md](./HelloWorldExplained.md).

We begin in a manner very similar to what is described in [HOWTO-WINDOWS.md](./HOWTO-WINDOWS.md), defining our instances. This time, though, we also logically define replicas for those instances which we will run later:

```bat
cd %AMBROSIATOOLS%\x64\Release\netcoreapp2.0\
dotnet Ambrosia.dll RegisterInstance -i=client -rp=1000 -sp=1001 -l=C:\logs\
dotnet Ambrosia.dll RegisterInstance -i=server -rp=2000 -sp=2001 -l=C:\logs\ -aa
dotnet Ambrosia.dll AddReplica -i=server -rp=3000 -sp=3001 -l=C:\logs\ -r=1
dotnet Ambrosia.dll AddReplica -i=server -rp=4000 -sp=4001 -l=C:\logs\ -r=2
```
Above, we see the two RegisterInstance gestures which define client and server. The only modification is that we added the -aa flag, which indicates that we will add at least one replica. Note that Active/Active requires at least 2 running instances, as all but the first checkpoint will be taken by a secondary, avoiding loss of availability during checkpointing.

After the two RegisterInstance gestures, we see two AddReplica gestures for the first and second replicas of server. Note that we are choosing unique receive and send ports for each replica, since we intend to run all four instance runtimes (client, server0, server1, server2) on a single machine.

To run all these, instead of 2 process pairs, we now need 4 (8 processes and console windows in total). To run the server ImmortalCoordinator:

 ```bat
 cd %AMBROSIATOOLS%\x64\Release\netcoreapp2.0
 dotnet ImmortalCoordinator.dll --instanceName=server --port=2500
```

To run the client ImmortalCoordinator:

```bat
cd %AMBROSIATOOLS%\x64\Release\netcoreapp2.0
dotnet ImmortalCoordinator.dll --instanceName=client --port=1500
```

To run the Immortal Coordinator for the first server replica

 ```bat
 cd %AMBROSIATOOLS%\x64\Release\netcoreapp2.0
 dotnet ImmortalCoordinator.dll --instanceName=server --port=3500 --r=1
```

To run the Immortal Coordinator for the second server replica

 ```bat
 cd %AMBROSIATOOLS%\x64\Release\netcoreapp2.0
 dotnet ImmortalCoordinator.dll --instanceName=server --port=4500 --r=2
```

To run the HelloWorld server:

```bat
cd Server\bin\x64\Debug\netcoreapp2.0
dotnet Server.dll
```

To run the HelloWorld server first replica:

```bat
cd Server\bin\x64\Debug\netcoreapp2.0
dotnet Server.dll -rp=3001 -sp=3000
```

To run the HelloWorld server second replica:

```bat
cd Server\bin\x64\Debug\netcoreapp2.0
dotnet Server.dll -rp=4001 -sp=4000
```

To run the HelloWorld client, in the fourth console window:

```bat
cd Client1\bin\x64\Debug\netcoreapp2.0
dotnet Client1.dll
```


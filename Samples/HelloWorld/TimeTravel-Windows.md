Time Travel Debugging with Hello World
========================================

One of Ambrosia's most compelling features is its ability to debug applications by rerunning them from logs, optionally with a debugger attached. Being able to debug in this fashion frequently eliminates the need for application writers to generate logs in the hopes that information contained in those logs will be useful for later debugging.

Note that debugging is an offline activity, which does not use service metadata, or even the internet. Debugging is performed in a completely isolated manner for an individual instance, and will not have any effect on a running deployment of that instance, or any instances which communicate with the debugged instance.

To make use of this capability, application debuggers need an instance log, and associated instance executables. Note that these instance executables don't need to be identical to the original executables, but must recover from the same state, and handle the API of the original instance. As a result, alternate debug versions of the instance may be substututed for the original, or even a slightly modified version with bug fixes or additional console output.

This walkthough assumes that readers have already read [HOWTO-WINDOWS.md](./HOWTO-WINDOWS.md) and [HelloWorldExplained.md](./HelloWorldExplained.md). We also assume familiarity with debugging in Visual Studio.

In this walkthrough, we first run Client1 and Server to completion, following the instructions in [HOWTO-WINDOWS.md](./HOWTO-WINDOWS.md). This means hitting enter for Client1. To make things more straightforward, exit server and client (and their associated ImmortalCoordinators) by hitting CTRL-C. 

Note that the logs directory was C:\logs\, and observe the log file directories for both client and server, the instance names of our two instances. Go into the server directory. Note the existence of servercheckpt1 and serverlog1. Whenever a new checkpoint is performed, either because the log has grown too large, or because we have recovered after a failure, additional checkpoint and log files are created. Recovery happens from the latest valid checkpoint, and the collection of logs beginning with the number of the recovered checkpoint. In this case, we took an initial checkpoint right after server was started, and generated a log, which contains all method calls to server. Since we didn't hit the log file limit (1GB by default), the whole log is contained in serverlog1.

Like running instances for real, debugging an instance involves running two processes. One, as before, is the actual service code, produced as before, but a debug version. Go ahead and run this in the same manner as before:

```bat

cd Server\bin\x64\Debug\netcoreapp2.0

dotnet Server.dll

```


Note that this process won't actually do anything until we start the second process, so go ahead and attach the debugger, setting breakpoints in OnFirstStart and ReceiveMessageAsync.

Now, instead of starting an ImmortalCoordinator, we run Ambrosia.dll in a different manner than before:

```bat

cd %AMBROSIATOOLS%\x64\Release\netcoreapp2.0

dotnet Ambrosia.dll DebugInstance -i=server -rp=2000 -sp=2001 -l=C:\logs\ -c=1
```


Note the similarity to the parameters we used when registering server with Ambrosia.dll, but with two differences: first, we use the DebugInstance gesture rather than the RegisterInstance gesture. Second, we have a -c=1 parameter, which specifies that we wish to begin debugging from checkpoint # 1, which happens to be our only checkpoint in this example.

When run this way, Ambrosia.dll becomes a substitute ImmortalCoordinator, non-destructively running the instance from the specified checkpoint by reading the logs. This enables running against both live logs, as well as copies of logs.

Note that our breakpoints are hit in the debugger, exactly the way they would have been if the debugger were attached when the instance was running. First stopping on OnFirstStart, and then for each method call from Client1. We also see the same output reproduced in the window where server was running.

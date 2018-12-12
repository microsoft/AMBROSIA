# How to run the HelloWorld Sample (on Windows with .NET Core)

This sample shows two immortals communicating, a client and a server. You can build and run it locally to get a quick idea of how Ambrosia operates. The solution contains two alternate versions of the client (Client1 and Client2), only one of which is used at a time.  Client1 demonstrates basic communication, while Client2 demonstrates nondeterministic input using an impulse handler.

## Quick Build

The sample uses the AmbrosiaLibCS NuGet package to import the necessary Ambrosia binaries. These packages are picked up automatically from the public NuGet feed, so there are no extra steps required to import them.

- Build `HelloWorld.sln` in Visual Studio, using configuration `Debug` and platform `x64`.

As part of a full build, Ambrosia generates code for the interface proxies. To make this sample easy to run, we have already included the generated code, so you don't have to run code generation just to build and run the HelloWorld sample. However, if you want to experiment with it or make any changes to the interfaces, you can run code generation yourself as described in the section "Full Build w/ Code Generation" below.

### Getting the Ambrosia tool binaries

To run the application you'll need the Ambrosia tools that are distributed in compressed folder. To get these,

- Download the compressed folder (either `Ambrosia-win.zip` or `Ambrosa-linux.tgz`)
- Unpack it somewhere on your disk; for example, `C:\Ambrosia-win\`
- Set the `%AMBROSIATOOLS%` environment variable to point to that directory


## Running HelloWorld 

For the purpose of this tutorial, we'll assume the following parameters:

- Log directory: `C:\logs\`
- Client instance name: `client`
- Client ImmortalCoordinator receive port: `1000`
- Client ImmortalCoordinator send port: `1001`
- Client ImmortalCoordinator CRA port: `1500`
- Server instance name: `server`
- Server ImmortalCoordinator receive port: `2000`
- Server ImmortalCoordinator send port: `2001`
- Server ImmortalCoordinator CRA port: `2500`

### Storage Connection String

Ambrosia uses an Azure table to maintain and discover information about your application's immortals and their status. To access this information, all Ambrosia tools and libraries need a connection string to an Azure storage account stored in the environment variable `%AZURE_STORAGE_CONN_STRING%`. To run this sample, you must therefore create an Azure Storage account if you don't already have one, and set the environment variable to contain its connection string.

### Registering the Immortal instances

Before running the application, you need to register each Immortal instance
so that other Immortal instances can find them. Open a command prompt and enter the following commands:

```bat
cd %AMBROSIATOOLS%\x64\Release\netcoreapp2.0\
dotnet Ambrosia.dll RegisterInstance -i=client -rp=1000 -sp=1001 -l=C:\logs\
dotnet Ambrosia.dll RegisterInstance -i=server -rp=2000 -sp=2001 -l=C:\logs\
```

You should see messages "The CRA instance appears to be down. Restart it and this vertex will be instantiated automatically". That means everything is working as expected! We have not started those instances yet - once we start them they'll register automatically.

### Running the application (client 1)

To run the HelloWorld application, you will need to run four command-line
processes, each in a separate window: the HelloWorld client Immortal, the
HelloWorld server Immortal, and two ImmortalCoordinator processes, one for
each Immortal.

To run the client ImmortalCoordinator, open a command prompt and enter these
commands:

```bat
cd %AMBROSIATOOLS%\x64\Release\netcoreapp2.0
dotnet ImmortalCoordinator.dll --instanceName=client --port=1500
```

To run the server ImmortalCoordinator:

 ```bat
 cd %AMBROSIATOOLS%\x64\Release\netcoreapp2.0
 dotnet ImmortalCoordinator.dll --instanceName=server --port=2500
```

To run the HelloWorld client:

```bat
cd Client1\bin\x64\Debug\netcoreapp2.0
dotnet Client1.dll
```

To run the HelloWorld server:

```bat
cd Server\bin\x64\Debug\netcoreapp2.0
dotnet Server.dll
```

After starting all four processes, you should see your client and server
communicate with each other! Specifically:

- The console of the server process prints `Received message from a client: Hello World 1!`
- The console of the client process prints `Press any key to continue`
- (Now press a key in the client process console window)
- The console of the server process prints `Received message from a client: Hello World 2!`
- The console of the server process prints `Received message from a client: Hello World 3!`




### Clearing state and re-running

If you want to run Hello World a second time, it is not enough to just restart the immortals! They will just resume running where they left off (at the end of Hello World). So, to start over, you have to clear the state. It's simple:

- Delete the contents of the `C:\logs` directory.


### Running the application w/ interrupt

(TODO)

### Running the application (client 2)

(TODO)

## Full Build w/ Code Generation

Ambrosia generates code for the interface proxies. To make this sample easy to run, we have already included the generated code, so you don't have to run code generation just to build and run the HelloWorld sample. However, if you want to experiment with it or make any changes to the interfaces, here is how you can run the code generation step.

The code generation step requires the Ambrosia tools that are distributed in compressed folder. So before running code generation the first time: (1) Download the tools (either Ambrosia-win.zip or Ambrosa-linux.tgz), (2) Unpack them somewhere on your disk, and (3) edit the powershell script Generate-Assemblies-NetCore.ps1 to use the correct path to that directory.

Once you have set up the script, here is how you run or re-run code generation:

1. Build the projects `IClient1`, `IClient2` and `IServer`. This generates the binaries used by the code generation step.

2. Run code generation by executing `Generate-Assemblies-NetCore.ps1`. This overwrites the content of the projects `Client1Interfaces`, `Client2Interfaces` and `ServerInterfaces` with generated code.

**CURRENTLY BROKEN :(**

```
Unhandled Exception: System.IO.FileNotFoundException: Could not find file 'C:\home\git\ambrosia\Samples\HelloWorld\CodeGenDependencies\netcoreapp2.0\A
mbrosiaCS.csproj'.
   at System.IO.FileStream.OpenHandle(FileMode mode, FileShare share, FileOptions options)
   at System.IO.FileStream..ctor(String path, FileMode mode, FileAccess access, FileShare share, Int32 bufferSize, FileOptions options)
   at System.IO.FileStream..ctor(String path, FileMode mode, FileAccess access, FileShare share, Int32 bufferSize)
   at System.Xml.XmlDownloadManager.GetStream(Uri uri, ICredentials credentials, IWebProxy proxy, RequestCachePolicy cachePolicy)
   at System.Xml.XmlUrlResolver.GetEntity(Uri absoluteUri, String role, Type ofObjectToReturn)
   at System.Xml.XmlTextReaderImpl.FinishInitUriString()
   at System.Xml.XmlTextReaderImpl..ctor(String uriStr, XmlReaderSettings settings, XmlParserContext context, XmlResolver uriResolver)
   at System.Xml.XmlReaderSettings.CreateReader(String inputUri, XmlParserContext inputContext)
   at System.Xml.XmlReader.Create(String inputUri, XmlReaderSettings settings)
   at System.Xml.Linq.XDocument.Load(String uri, LoadOptions options)
   at Ambrosia.Program.RunCodeGen() in D:\a\1\s\Clients\CSharp\AmbrosiaCS\Program.cs:line 162
   at Ambrosia.Program.Main(String[] args) in D:\a\1\s\Clients\CSharp\AmbrosiaCS\Program.cs:line 31
```

3. Build HelloWorld.sln. This now picks up the freshly generated source files.

 
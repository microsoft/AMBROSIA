<!-- Note: If using VS Code, install the "bierner.markdown-emoji" extension in order to see emoji's in the built-in MarkDown preview window. -->
## :helicopter: The Ambrosia Node.js Language Binding: An Overview
----
### :vertical_traffic_light: Getting Started
The Ambrosia Node.js language binding enables you to write Ambrosia apps/services (**[Immortals](https://github.com/microsoft/AMBROSIA#ambrosia-concepts)**) for Node.js using TypeScript (4.4+). Unless you are developing the Node.js language binding (LB) itself, you obtain it by installing an npm package (for example, `ambrosia-node-2.0.0.tgz`) in the folder where you're building your Ambrosia app. If you _are_ developing the LB itself, see steps 7 and 12 of the **[Developer Machine Setup Guide](DevMachineSetup.md)** for how to build the Node.js LB.

The package includes a complete copy of the source code (in TypeScript) so that you can step right into the LB's code using the debugger (VS Code is recommended, which is the IDE that was used to develop the LB).

The Node.js LB package doesn't ship with the core Ambrosia binaries (which are .Net assemblies), so these also need to be installed:

- Create a folder, for example C:\ambrosia-binaries, then visit **[Ambrosia Releases](https://github.com/microsoft/AMBROSIA/releases)** and click on the installer for your OS (for example, Ambrosia-win-x64.zip) and extract it to C:\ambrosia-binaries.
- Create an `AMBROSIATOOLS` environment variable that points to C:\ambrosia-binaries\x64\Release after checking that this folder exists (for Linux, set `AMBROSIATOOLS` to /ambrosia-binaries/bin/).

Since Ambrosia uses Azure for storing meta-data (and, optionally, storing log and checkpoint files) you will need an Azure storage account, the connection string for which must be stored in the `AZURE_STORAGE_CONN_STRING` environment variable. To help with debugging (especially if developing the LB itself), it's also useful to install **[Azure Storage Explorer](https://azure.microsoft.com/en-us/features/storage-explorer/)**.

> For additional details about developer environment related setup, please review the **[Developer Machine Setup Guide](DevMachineSetup.md)**.

All configuration for the Node.js LB is specified using the `ambrosiaConfig.json` file. This file uses a schema, so if you open it using VS Code you will get **[IntelliSense](https://code.visualstudio.com/docs/editor/intellisense)** for each setting describing what is does and its default value. Pressing `Ctrl+Space` (again, if using VS Code) will show all the remaining available settings. By default, the LB will look for `ambrosiaConfig.json` in the current folder. To use a different `ambrosiaConfig.json`, specify the [optionally pathed] file name using the `ambrosiaConfigFile` command-line parameter, for example `ambrosiaConfigFile=C:\myConfigs\someAmbrosiaConfig.json`.

After installing the `ambrosia-node` package, a default `ambrosiaConfig.json` will be copied into your installation folder. This file is intended to be edited, not used "as-is".

To check that your environment is ready for developing an Ambrosia Node.js app:

1. Edit `ambrosiaConfig.json` by changing these settings as shown:

    ````Json
    "autoRegister": true,
    "icBinFolder": "",
    ````
    Also, edit the `"icLogFolder"` to point to suitable folder on your local machine (the folder will be created if it doesn't exist).

2. Create a `main.ts` file with the following code:

    ````TypeScript
    import Ambrosia = require("ambrosia-node"); 
    import Utils = Ambrosia.Utils;

    main();

    async function main()
    {
        try
        {
            await Ambrosia.initializeAsync();
            Ambrosia.ICTest.startTest();
        }
        catch (error: unknown)
        {
            Utils.tryLog(Utils.makeError(error));
        }
    }
    ````
This program runs the built-in test app, which is typically only used by developers who are working on the Node.js LB itself, but which can also be used a kind of "Hello World" to check that the LB is operational. Because we set `"autoRegister": true`, the LB will automatically register an Immortal instance using the settings from `ambrosiaConfig.json`.  If you prefer, you can instead register the instance manually, which can be accomplished by running `ambrosia.exe` with the `RegisterInstance` verb (see **[here](https://github.com/microsoft/AMBROSIA/blob/master/Samples/HelloWorld/HOWTO-WINDOWS-TwoProc.md#registering-the-immortal-instances)**). If the app starts successfully, you will see output in the console similar to this:

<details>
<summary style="font-style: italic">(Click to show/hide output)</summary>

````
2021/10/25 14:23:24.035: Logging output to C:\src\Git\AMBROSIA\Clients\AmbrosiaJS\Ambrosia-Node\outputLogs\traceLog_20211025_142324.txt
2021/10/25 14:23:24.038: Ambrosia configuration loaded from 'ambrosiaConfig.json'
2021/10/25 14:23:24.438: No existing registration found for 'server'
2021/10/25 14:23:24.454: Registering instance 'server'...
2021/10/25 14:23:24.455: Args: RegisterInstance instanceName=server receivePort=2000 sendPort=2001 log=C:/logs/ currentVersion=0 upgradeVersion=0 logTriggerSize=1024
2021/10/25 14:23:33.639: Instance successfully registered (auto-register)
2021/10/25 14:23:33.642: Reading registration settings...
2021/10/25 14:23:34.046: Registration settings read (from Azure)
2021/10/25 14:23:34.232: Local IC connects to 0 remote ICs 
2021/10/25 14:23:34.234: Warning: 0 log/checkpoint files found [on disk] - Recovery will not run
2021/10/25 14:23:34.236: IC test running: Press 'X' (or 'Enter') to stop, or 'H' to list all available test commands
2021/10/25 14:23:34.239: Using "VCurrent" application code (appVersion: 0, upgradeVersion: 0)
2021/10/25 14:23:34.241: Starting C:\src\Git\PostSledgehammer\AMBROSIA\ImmortalCoordinator\bin\x64\Release\net461\ImmortalCoordinator.exe...
2021/10/25 14:23:34.242: Args: port=2500 instanceName=server receivePort=2000 sendPort=2001 log=C:/logs/ logTriggerSize=1024
2021/10/25 14:23:34.288: 'server' IC started (PID 20140)
2021/10/25 14:23:34.288: LB Connecting to IC receive port (localhost:2000)...
2021/10/25 14:23:34.297: LB retrying to connect to IC receive port (in 2991ms); last attempt failed (reason: connect ECONNREFUSED 127.0.0.1:2000)...
2021/10/25 14:23:36.232: [IC] Starting CRA Worker instance [http://github.com/Microsoft/CRA]
2021/10/25 14:23:36.232: [IC] Instance Name: server0
2021/10/25 14:23:36.236: [IC] IP address: 192.168.1.150
2021/10/25 14:23:36.236: [IC] Port: 2500
2021/10/25 14:23:36.236: [IC] Secure network connections: Disabled
2021/10/25 14:23:36.624: [IC] Disabling dynamic assembly loading
2021/10/25 14:23:36.627: [IC] Enabling sideload for vertex: ambrosia (Ambrosia.AmbrosiaRuntime)
2021/10/25 14:23:37.233: [IC] Waiting for a connection...
2021/10/25 14:23:37.300: LB retrying to connect to IC receive port (in 2998ms); last attempt failed (reason: connect ECONNREFUSED 127.0.0.1:2000)...
2021/10/25 14:23:37.388: [IC] Dynamic assembly loading is disabled. The caller will need to sideload the vertex.
2021/10/25 14:23:38.181: [IC] Ready ...
2021/10/25 14:23:38.181: [IC] Logs directory: C:/logs/
2021/10/25 14:23:40.304: LB connected to IC receive port (localhost:2000)
2021/10/25 14:23:40.305: LB connecting to IC send port (localhost:2001)...
2021/10/25 14:23:40.308: LB connected to IC send port (localhost:2001)
2021/10/25 14:23:41.532: [IC] Exception: System.InvalidOperationException: Nullable object must have a value.
2021/10/25 14:23:41.532: [IC]    at System.ThrowHelper.ThrowInvalidOperationException(ExceptionResource resource)
2021/10/25 14:23:41.532: [IC]    at CRA.ClientLibrary.CRAClientLibrary.<ConnectAsync>d__53.MoveNext()
2021/10/25 14:23:41.532: [IC] Possible reason: The connection-initiating CRA instance appears to be down or could not be found. Restart it and this connection will be completed automatically
2021/10/25 14:23:41.535: Warning: Because this is the first start of the instance after initial registration, the prior [IC] 'System.InvalidOperationException' is expected
2021/10/25 14:23:42.289: [IC] Exception: System.InvalidOperationException: Nullable object must have a value.
2021/10/25 14:23:42.289: [IC]    at System.ThrowHelper.ThrowInvalidOperationException(ExceptionResource resource)
2021/10/25 14:23:42.289: [IC]    at CRA.ClientLibrary.CRAClientLibrary.<ConnectAsync>d__53.MoveNext()
2021/10/25 14:23:42.289: [IC] Possible reason: The connection-initiating CRA instance appears to be down or could not be found. Restart it and this connection will be completed automatically
2021/10/25 14:23:42.291: Warning: Because this is the first start of the instance after initial registration, the prior [IC] 'System.InvalidOperationException' is expected
2021/10/25 14:23:42.573: Received data from IC (4 bytes)
2021/10/25 14:23:42.574: Received data from IC (22 bytes)
2021/10/25 14:23:42.577: Received 'TakeBecomingPrimaryCheckpoint' (2 bytes)
2021/10/25 14:23:42.757: Sending 'InitialMessage' to local IC (8 bytes)
2021/10/25 14:23:42.758: Sending 'Checkpoint' to local IC (6 bytes)
2021/10/25 14:23:42.759: Streaming 'CheckpointDataStream (2097371 bytes)' to local IC...
2021/10/25 14:23:42.766: Stream 'CheckpointDataStream (2097371 bytes)' finished
2021/10/25 14:23:42.768: checkpointProducer: Checkpoint saved
2021/10/25 14:23:42.769: Local instance is now primary
2021/10/25 14:23:42.769: Dispatcher: Normal app processing can begin
2021/10/25 14:23:42.772: [IC] Reading a checkpoint 2097371 bytes
2021/10/25 14:23:43.126: Received data from IC (32 bytes)
2021/10/25 14:23:43.127: Received 'InitialMessage' (8 bytes)
2021/10/25 14:23:43.129: Warning: Local IC not ready to handle self-call RPC's: The response from the IC will be delayed
2021/10/25 14:23:43.130: Posting method '_echo' (version 1) to local IC
2021/10/25 14:23:43.132: Sending 'RPC' to local IC (157 bytes)
2021/10/25 14:23:44.110: [IC] Connecting server:Ambrosiacontrolout:server:Ambrosiacontrolin
2021/10/25 14:23:44.110: [IC] Connecting with killRemote set to false
2021/10/25 14:23:44.371: [IC] Connected!
2021/10/25 14:23:44.374: [IC] Waiting for a connection...
2021/10/25 14:23:44.378: [IC] Connecting server:Ambrosiadataout:server:Ambrosiadatain
2021/10/25 14:23:44.378: [IC] Connecting with killRemote set to false
2021/10/25 14:23:44.391: [IC] Adding input:
2021/10/25 14:23:44.395: [IC] restoring output:
2021/10/25 14:23:44.600: [IC] Connected!
2021/10/25 14:23:44.600: [IC] Waiting for a connection...
2021/10/25 14:23:44.603: [IC] restoring input:
2021/10/25 14:23:44.607: [IC] restoring output:
2021/10/25 14:23:44.637: Received data from IC (180 bytes)
2021/10/25 14:23:44.638: Received 'RPC' (156 bytes)
2021/10/25 14:23:44.640: Posting [Fork] result of method '_echo' to local IC
2021/10/25 14:23:44.641: Sending 'RPC' to local IC (159 bytes)
2021/10/25 14:23:44.644: Received data from IC (182 bytes)
2021/10/25 14:23:44.645: Received 'RPC' (158 bytes)
2021/10/25 14:23:44.646: Intercepted [Fork] RPC call for post method '_echo_Result' [resultType: normal]
2021/10/25 14:23:44.647: Now is: 2021/10/25 14:23:43.128
````
</details><br/>

If the last line reports "Now is: ..." then the LB is operational, and you can exit the test by pressing 'X'.<br/>
Alternatively, if you press the 'H' key, the list of available test commands will be displayed (although, again, be aware that most of these are for **internal** testing only):

<details>
<summary style="font-style: italic">(Click to show/hide output)</summary>

````
2021/10/25 15:12:27.486: ------------------------
2021/10/25 15:12:27.488: Available test commands:
2021/10/25 15:12:27.488: ------------------------
2021/10/25 15:12:27.489: X: Exit (stop) the test
2021/10/25 15:12:27.489: F: Call Fork RPC
2021/10/25 15:12:27.490: I: Call implicitly batched RPC
2021/10/25 15:12:27.490: B: Call explicitly batched RPCs
2021/10/25 15:12:27.491: P: Post RPC
2021/10/25 15:12:27.492: O: Post RPC Timeout Test (10 seconds)
2021/10/25 15:12:27.493: M: Get published methods
2021/10/25 15:12:27.493: T: Get published types
2021/10/25 15:12:27.494: S: Check if a method (of a specific version) is published
2021/10/25 15:12:27.494: R: Request checkpoint
2021/10/25 15:12:27.495: E: Echo
2021/10/25 15:12:27.496: L: Send large message (64 MB)
2021/10/25 15:12:27.496: Z: Run Fork performance test
````
</details><br/>

For example, to repeat the "echoing" of the current date/time, press the 'E' key.

> By default, LB trace output it written to both the console window _and_ to a file (in the above trace, you can see the the output is being logged to C:\src\Git\AMBROSIA\Clients\AmbrosiaJS\Ambrosia-Node\outputLogs\traceLog_20211025_142324.txt).<br/>
You can control output logging via the `"lbOptions.outputLogX"` settings in ambrosiaConfig.json.

&nbsp;
### :stopwatch: A Quick Tour of the Ambrosia Node.js LB API

- The LB API is accessed by first installing the ambrosia-node package (for example, `npm install ambrosia-node-2.0.0.tgz`) in the folder where you're building your app. Then, in your TypeScript code, import the package with:
    ````TypeScript
    import Ambrosia = require("ambrosia-node");
    ````
    To make references to the various namespaces within the API more concise, these can be imported too, for example:
    ````TypeScript
    import IC = Ambrosia.IC;
    import Meta = Ambrosia.Meta;
    import Messages = Ambrosia.Messages;    
    import Utils = Ambrosia.Utils;
    ````
    In this documentation, all methods and properties in the API are prefixed with the namespace they belong to (for example, `Utils.log()`).

- All public parts of the API have **[JSDoc](https://jsdoc.app/about-getting-started.html)** comments that describe their use, so they can be easily explored (for example, by using IntelliSense in VS Code). However, if you see "[Internal]" at the start of the API comment, then you should **not** call it directly; these APIs have been made public only so that other parts of the LB can use them. Anything marked with "[Experimental]" should never be called/used.
- Only a tiny fraction of the available APIs are needed to use the LB. However, if you are are developing the LB itself, then you will use sigificantly more of the API surface.
- From the perspective of using the LB, the API can be divided up by category. These categories, along with some of the most commonly used methods/properties/types in each category, are described below:
  - Publishing and code-gen
      - `Ambrosia.initializeAsync(Ambrosia.LBInitMode.CodeGen)`
      - `Meta.emitTypeScriptFileFromSource()`
      - `Meta.FileGenOptions`
  - Controlling the IC
      - `Configuration.AmbrosiaConfig`
      - `IC.start()`
      - `IC.stop()`
      - `IC.requestCheckpoint()`
  - Calling published methods (for most cases, the wrappers created by code-gen will call these methods for you)
      - `IC.callFork()` 
      - `IC.callImpulse()` 
      - `IC.queueFork()`
      - `IC.queueImpulse()`
      - `IC.flushQueue()`
      - `IC.postFork()` 
      - `IC.postByImpulse()` 
  - Reading the configuration
      - `Configuration.AmbrosiaConfigFile` 
      - `Configuration.loadedConfig()`
      - `Configuration.loadedConfigFileName()`
      - `IC.instanceName()`
  - Utilities (output logging, command-line parsing, etc.)
      - `Utils.getCommandLineArg()` 
      - `Utils.hasCommandLineArg()` 
      - `Utils.log()`
      - `Utils.LoggingLevel`<br/><br/>
- Events raised by the LB (see `Messages.AppEventType`) are handled by defining event handlers (in the code-gen "input" .ts file) which code-gen will then wire-up automatically. Comments starting with "// TODO: Add an exported [non-async] function ..." in the generated `PublisherFramework.g.ts` file describe the signatures of the event handlers you can add. For more information about code-gen, see **[Code Generation for an Ambrosia Node.js App/Service](CodeGen.md)**.

&nbsp;
### :hammer: Building Your First Ambrosia App

In essence, an Ambrosia app/service consist of an application state (which is periodically checkpointed) and methods that make changes to the app state. The method calls are logged by Ambrosia, allowing them to be replayed (after loading the latest checkpoint) when the app recovers when it restarts.

When using the Node.js LB, application state must derive from a special base class (`Ambrosia.AmbrosiaAppState`), and methods must be called using generated wrappers that ensure the methods are logged. The initial application state and wrappers are generated via a code-generation step, which "reflects" on a single TypeScript source file where the methods (and the types used by those methods) have been defined and annotated to be "published". The annotation consist of being statically decorated with a special `@ambrosia` tag. Any time a published method is modified (added/changed/removed), code-generation must be run again. Code-generation is done by calling an API (`Meta.emitTypeScriptFileFromSource`) in the LB.

The following describes the basic steps for creating a "Hello World" Node.js Ambrosia app:

1) In a single .ts file (for example, myAPI.ts), define the first few methods that you want to be callable - either by other instances, or by the local instance (as a self-call). You only need one method to start (and it only needs to be a stub) with since you can add more methods over time as you continue development of the app. Self-calls are an important part of most Ambrosia apps, since this is how an app itself makes changes to app state in a deterministic way (further, there is no requirement that an Immortal instance has to talk to any instances other than itself).

2) Decorate the methods (and types) you want to "publish" (ie. that you want to be available to be called by any instance) with an `@ambrosia` JSDoc tag, for example:
   ````TypeScript
   /** 
    * My first Aambroia method.
    * @ambrosia publish=true, methodID=1
    */
   export function helloWorld(): void
   {
       Utils.log("Hello World!");
   }
   ````

3) Create a main.ts file and add a `codeGen()` function that takes myAPI.ts as input. Set the `codeGen()` function as the entry-point for the program and run it. This will generate `PublisherFramework.g.ts` (used by the 'server' instance) and `ConsumerInterface.g.ts` (used by the 'client' instance):
   > The ".g" in the file name simply denotes that its a generated file, so any manual edits will be lost at the next code-gen (see `FileGenOptions.mergeType`).<br/>
     See **[Code Generation for an Ambrosia Node.js App/Service](CodeGen.md)** for a deeper dive into code-gen.

   ````TypeScript
   import Ambrosia = require("ambrosia-node"); 
   import Meta = Ambrosia.Meta;

   codeGen(); // Entry-point

   async function codeGen()
   {
       // Note: Error handling omitted for brevity
       await Ambrosia.initializeAsync(Ambrosia.LBInitMode.CodeGen);
       Meta.emitTypeScriptFileFromSource("./src/myAPI.ts", { apiName: "MyAPI", fileKind: Meta.GeneratedFileKind.AllKind, mergeType: Meta.FileMergeType.None, outputPath: "./src" });
   }
   ````

4) Import `PublisherFramework.g.ts` into main.ts, and move the `State` namespace from `PublisherFramework.g.ts` to myAPI.ts (so that it can be augmented without risk of being overwritten by a subsequent code-gen). Re-run code-gen to fix up the [now broken] State references in `PublisherFramework.g.ts`. Eventually, you will need to implement the `// TODO` comments in the `AppState` class, but you can skip this for now.
   ````TypeScript
   import * as Framework from "./PublisherFramework.g"; // This is a generated file
   ````

5) In myAPI.ts, create a namespace called `EventHandlers` (the name can be anything, and you don't even need a namespace) and add a handler `onFirstStart(): void` (this name _is_ critical) as per the "call to action" in `PublisherFramework.g.ts`:
   ````TypeScript
   // TODO: Add an exported [non-async] function 'onFirstStart(): void' to ./myAPI.ts, then (after the next code-gen) a call to it will be generated here
   ````
   In you onFirstStart() handler, add a call to the generated wrapper (there will be 4 versions with suffixes `_Fork`, `_Impulse`, `_EnqueueFork`, and `_EnqueueImpulse`) for one of the published methods. Because we're making a self-call, this will require also importing `ConsumerInterface.g.ts`. Note that the `ConsumerInterface.g.ts` automatically includes a `setDestinationInstance()` method, and this must be called at least once before calling any of the published methods.
   ````TypeScript
   import * as Framework from "./PublisherFramework.g"; // This is a generated file
   import * as PublishedAPI from "./ConsumerInterface.g"; // This is a generated file

   /** Namespace for Ambrosia AppEvent handlers. */
   export namespace EventHandlers
   {
       export function onFirstStart(): void
       {
           PublishedAPI.setDestinationInstance(IC.instanceName()); // Send to ourself
           PublishedAPI.helloWorld_Fork(); 
       }
   }
   ````
   Finally, re-run `codeGen()` to wire-up the `onFirstStart()` handler.

6) In main.ts, add a new function `main()` and set it as the entry-point.
   ````TypeScript
   import IC = Ambrosia.IC;
   import Configuration = Ambrosia.Configuration;
   import * as Framework from "./PublisherFramework.g"; // This is a generated file
   import * as MyAPI from "./myAPI";

   main(); // Entry-point

   let _config: Configuration.AmbrosiaConfig | null = null;

   async function main()
   {
       // Note: Error handling omitted for brevity
       await Ambrosia.initializeAsync();
       _config = new Configuration.AmbrosiaConfig(Framework.messageDispatcher, Framework.checkpointProducer, Framework.checkpointConsumer);
       MyAPI.State._appState = IC.start(_config, MyAPI.State.AppState);
   }
   ````
   What this is doing is starting the instance by calling `IC.start()`. This method takes a `Configuration.AmbrosiaConfig` class instance which is used to specify the core Ambrosia event handlers: the handler to dispatch messages, the handler for creating a checkpoint, and the handler for restoring a checkpoint. Fortunately, code-gen creates all these handlers for us in `PublisherFramework.g.ts`, so we can simply pass in these generated handlers. If we wanted to use our own custom handlers (an expert feature), we could provide them instead &ndash; but **never** mix custom and default checkpoint event handlers. `IC.start()` also requires the constructor for your application state. It needs this because the LB is responsible for instantiating the class, both during `IC.start()` and when restoring a checkpoint in `Framework.checkpointConsumer()`. So while the application keeps its own reference to `MyAPI.State._appState` (so that it can use it), it should **never** [re]instatiate it itself.

7) You should now be able to run the instance, and the code in your chosen published method will execute. If the app is restarted, `onFirstStart()` will be called again because the `"lbOptions.deleteLogs"` setting is true in ambrosiaConfig.json. If this is changed to false, then at the next restart `onFirstStart()` will not execute (the `Messages.AppEventType.FirstStart` event only happens when there are no existing checkpoints/logs for the instance), but the published method will _still_ be called by recovery. 

While this process may seem a little complicated at first, several of these steps are one-time only. The pattern of ongoing "additional" work (ie. that's specific to Ambrosia) will quickly become just:
- Adding new published methods (and types used in published method parameters), then re-running code-gen.
- Modifying the `State.AppState` class by adding/updating/removing members, then updating the constructor to handle initialization and re-instantiation from a restored checkpoint.

Another example of this process is described in **[How the PTI App was Created](../../PTI-Node/ReadMe.md#bulb-how-the-pti-app-was-created)**. If desired, the PTI app code can be studied as a more complete (although still artificial) example of a working Ambrosia Node application.

&nbsp;
### :thought_balloon: Application Design Considerations

Adopting Ambrosia requires thinking about program design through the lens of deterministic execution. All application state changes must **only** occur through published method calls. This ensures that the calls are logged so that they can be replayed &ndash; in order &ndash; during recovery. This places considerable limitations on how asynchronous TypeScript language features are used when calling published methods (basically, they need to be avoided). Further, deterministic code has to coexist with non-deterministic code (like handling user input), which is where 'Impulse' methods comes into play. Getting a return value from a published method also requires a specific technique called 'Post' methods.

There are limitations on what kind of TypeScript entities can be published (for example, a class cannot be published), and on what kind of constructs can be included in applicaton state (not all data types in TypeScript can be serialized [to a checkpoint] by the LB). Further, by default, application state is effectively limited to around 100 MB due to the amount of memory (and time) required to serialize it to JSON. And how handlers for RPC messages (method calls) behave is also subject to some specific rules.  Consideration of all these issues must be taken into account when designing/building your app.

You can learn more about Impulse methods, Post methods, and message handling rules **[here](ImpulseExplained.md)**, and about code-gen restrictions **[here](CodeGen.md#code-generation-restrictions)**.

&nbsp;
### :twisted_rightwards_arrows: Command-Line Parameters

After an app has called `Ambrosia.initializeAsync()` it can respond to any of the LB's command-line parameters, all of which are optional:

Name | Description
-|-
`ambrosiaConfigFile=` | Specifies the name (and, optionally, the location) of the `ambrosiaConfig.json` file to use<br/>Note: If omitted, the LB will look in the current folder for `ambrosiaConfig.json`
`autoRegister`&#x00B9; | During app startup, automatically registers (or re-registers) the `instanceName` specified in `ambrosiaConfig.json`<br/>Note: This is the same as setting "autoRegister" to true in `ambrosiaConfig.json`
`registerInstance`&#x00B9; | Same as `autoRegister`, but the app immediately exits after registering<br/>Note: This is the same as setting "autoRegister" to "trueAndExit" in `ambrosiaConfig.json`
`eraseInstance`&#x00B9; | After prompting for confirmation, completely erases all meta-data and checkpoints/logs for the `instanceName` specified in `ambrosiaConfig.json`; the app will immediately exit afterwards<br/>:warning: **Use with caution**
`eraseInstanceAndReplicas`&#x00B9; | Like `eraseInstance`, but repeated for all instance replicas too (see **[Demonstration of Using Active/Active with the Ambrosia Node.js Language Binding](../test/ActiveActive/ReadMe.md)**)<br/>:warning: **Use with caution**

&#x00B9; For example usage, see **[Cleaning Up](../../PTI-Node/ReadMe.md#wastebasket-cleaning-up)** in the Performance Test Interruptible (PTI) documentation.<br/>

&nbsp;
### :books: Further Reading

- [Impulse RPCs Explained](ImpulseExplained.md)
- [Code Generation for an Ambrosia Node.js App/Service](CodeGen.md)
- [Type Checking in the Node.js Language Binding](TypeChecking.md)
- [Upgrading an Ambrosia Node.js App/Service](Upgrade.md)
- [Demonstration of Using Active/Active with the Ambrosia Node.js Language Binding](../test/ActiveActive/ReadMe.md)
- [Performance Test Interruptible (PTI)](../../PTI-Node/ReadMe.md)

&nbsp;

---
<table align="left">
  <tr>
    <td>
      <img src="images/ambrosia_logo.png" width="80" height="80"/>
    </td>
    <td>
      <div>
          <a href="https://github.com/microsoft/AMBROSIA#ambrosia-robust-distributed-programming-made-easy-and-efficient">AMBROSIA</a>
      </div>
      <sub>An Application Platform for Virtual Resiliency</sub>
      <br/>
      <sub>from Microsoft Research</sub>
    </td>
  </tr>
</table>
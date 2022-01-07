<!-- Note: If using VS Code, install the "bierner.markdown-emoji" extension in order to see emoji's in the built-in MarkDown preview window. -->
## Performance Test Interruptible (PTI) for the Node.js Ambrosia Language Binding
----
## :arrow_forward: Getting Started
PTI is a test application that demonstrates using Ambrosia for a basic task (essentially, repeatedly making a simple method call from a 'client' to a 'server' **[Immortal instance](https://github.com/microsoft/AMBROSIA#ambrosia-concepts)**). PTI is primarily used for smoke, performance, and recovery testing of the Immortal Coordinator (IC) and an Ambrosia language binding (LB) - which together form an Immortal instance. There's also a version of PTI for the C# language binding, but this document only concerns the version for the Node.js language binding which is written in TypeScript (4.4).

There are 3 folders under AmbrosiaJS\PTI:
* App - Contains the PTI TypeScript application, and must be built - for example using **[Visual Studio Code](https://code.visualstudio.com/)** - before it can be used. &#x00B9;
* Config - Contains ambrosiaConfig.json files (used by the Node.js LB) for running PTI in various roles.
* Images - Contains images used by this MarkDown file.

**To build the PTI app:**
1. Switch to the ..\Ambrosia-Node folder and run `".\build.ps1"`. This builds the Node.js LB npm package (ambrosia-node-x.x.x.tgz) which PTI uses. &#x00B2;
2. Switch to the PTI-Node\App folder and run `"npm install ..\..\Ambrosia-Node\ambrosia-node-0.0.84.tgz"` (the current version number may by higher).
3. Revert changes to the App\ambrosiaConfig.json file (these arise as a result of installing the ambrosia-node package). Alternatively, you can just copy ambrosiaConfig.json.old over ambrosiaConfig.json. 
4. Build the PTI app (manually) from the PTI-Node\App folder by running `"npx tsc -p .\tsconfig.json '--incremental false'"`. &#x00B3;

The PTI app is capable of running in 3 different roles: `Client`, `Server`, and `Combined`.  Which role the app runs in is specified via the "--instanceRole" command-line parameter. To see all available command-line parameters, specify "--help", eg. from the PTI-Node\App folder run `"node .\out\main.js --help"`. Note that which role a parameter applies to is called out in the displayed help syntax, as is the short-name version of each parameter (for example, you can specify "-ir" instead of  "--instanceRole").

Be aware that while the PTI app is configured via the command-line, the Node.js LB itself is configured via the `ambrosiaConfig.json` file. &#x2074; &#x2075;

**To run your first PTI test:**
1. Edit the `"icLogFolder"` &#x2076; and `"icBinFolder"` settings in `ambrosiaConfig.json`. Be sure to save the file.
2. Set the `"autoRegister"` setting to `true` in `ambrosiaConfig.json`. Be sure to save the file.
3. Set the Ambrosia `AZURE_STORAGE_CONN_STRING` environment variable (see **[here](https://github.com/microsoft/AMBROSIA/blob/master/Samples/HelloWorld/HOWTO-WINDOWS-TwoProc.md#storage-connection-string)**).
4. From the PTI-Node\App folder, run this command:
````PowerShell
node .\out\main.js --instanceRole=Combined --fixedMessageSize --noHealthCheck --expectedFinalBytes=1073741824
````
5. If it was sucesssful, the last output message reported will be:<br/>
`SUCCESS: The expected number of bytes (1073741824) have been received`

> **Tip:** By default, the app will run with the minimal level of output (console) logging. If you run into problems, you can change the `"outputLoggingLevel"` setting in `ambrosiaConfig.json` from `"Minimal"` to `"Verbose"` to log additional output, which is often helpful when troubleshooting.  **Caution:** Additional logging will _significantly_ reduce performance, so `"Verbose"` should only be used when investigating problems, not when running tests.

<br/><u>Footnotes</u><br/>
&#x00B9; Visual Studio Code is the recommended way to build and edit PTI.<br/>
&#x00B2; If you've recently built this package, you can skip this step.<br/>
&#x00B3; Alternatively, using Visual Studio Code, open the PTI-Node\App folder and build.<br/>
&#x2074; By default, the LB will look for `ambrosiaConfig.json` in the current folder. To use a different `ambrosiaConfig.json`, specify the [optionally pathed] file name using the `ambrosiaConfigFile` command-line parameter, eg. `"ambrosiaConfigFile=C:\configs\myAmbrosiaConfig.json"`.<br/>
&#x2075; If you are using Visual Studio Code, you can hover over any setting in `ambrosiaConfig.json` to see its description.<br/>
&#x2076; The specified folder will be automatically created if it doesn't already exist.<br/>

&nbsp;

## :vertical_traffic_light: Running PTI

PTI is an Ambrosia app, so it runs as an Ambrosia Immortal instance. As with all Ambrosia Immortal instances, before a PTI instance can run (in any role) it must be registered. This can be accomplished by setting the `"autoRegister"` parameter to `true` in ambrosiaConfig.json, which will cause registration to run automatically during the app's initialization (`"autoRegister"` will reset itself to `false` if registration succeeds). Alternatively, you can run `ambrosia.exe` with the `RegisterInstance` verb (see **[here](https://github.com/microsoft/AMBROSIA/blob/master/Samples/HelloWorld/HOWTO-WINDOWS-TwoProc.md#registering-the-immortal-instances)**). It's also necessary to set the Ambrosia `AZURE_STORAGE_CONN_STRING` environment variable (see **[here](https://github.com/microsoft/AMBROSIA/blob/master/Samples/HelloWorld/HOWTO-WINDOWS-TwoProc.md#storage-connection-string)**).

> :warning: A separate instance must be registered for <u>each</u> different role (`Client`, `Server`, and `Combined`) you use. If you want to run multiple instances in a given role, then each separate instance will also need to be registered. The instance name is stored in the `"instanceName"` setting in the ambrosiaConfig.json file. By default, PTI will look for this file in the current folder (eg. PTI-Node\App). However, you can also specify the `ambrosiaConfigFile=` command-line parameter to make the app use a different ambrosiaConfig.json file. Along with the `"instanceName"`, the config file also includes other per-instance registration settings such as: `"icLogFolder"`, `"icCraPort"`, `"icReceivePort"` and `"icSendPort"`.

The simplest way to run the PTI app is in the `Combined` role. In this role there is only a single instance which acts as both the client and server, so the app effectively runs in a loop-back configuration where it "only talks to itself". If you run the app directly from Visual Studio Code (ie. from the PTI-Node\App folder), by default, this is the role the app runs in. Because there's only a single instance, it enables easier debugging of both the client-side _and_ server-side of the PTI app. When using Visual Studio Code, the app command-line parameters are specified using the `"args"` setting in `App\.vscode\launch.json`.

If you're not using Visual Studio Code, you can run PTI in the `Combined` role directly from the PTI-Node\App folder. For example:
````PowerShell
node .\out\main.js --instanceRole=Combined --fixedMessageSize --noHealthCheck --expectedFinalBytes=1073741824
````
Because --numOfRounds, --bytesPerRound, and --maxMessageSize are not specified, the app will default to sending 1 round of 1 GB (16,384 messages of 64 KB each) to itself, then the app will exit. If it was sucesssful, the last output message reported will be:

`SUCCESS: The expected number of bytes (1073741824) have been received`

PTI can also be run in "2-party" mode, by launching distinct 'client' and 'server' instances. Each instance must be run using role-specific app parameters (which are called out in the "--help" syntax), and each instance must have its own ambrosiaConfig.json file. Here's an example of running PTI in 2-party mode. In this case, the client will send 128 bytes (2 messages of 64 bytes, in a single batch) to the server, which the server then "echoes" back to the client:

> :warning: If this is your first time running this, then it's necessary to change the `"autoRegister"` setting to `true` in both `PTIServerConfig.json` and `PTIClientConfig.json` in `PTI-Node\Configs`, and save the files.

From command prompt 1, switch to the PTI-Node\App folder, then start the server:
````PowerShell
node .\out\main.js --instanceRole=Server --bidirectional --noHealthCheck --expectedFinalBytes=128 ambrosiaConfigFile=..\Configs\PTIServerConfig.json
````
From command prompt 2, switch to the PTI-Node\App folder, start the client:
````PowerShell
node .\out\main.js --instanceRole=Client --serverInstanceName=PTIServer --numOfRounds=1 --fixedMessageSize --bytesPerRound=128 --maxMessageSize=64 --batchSizeCutoff=128 --expectedEchoedBytes=128 ambrosiaConfigFile=..\Configs\PTIClientConfig.json
````

Unlike when running in the `Combined` role, neither instance will automatically exit when the test completes. When either instance is manually stopped, the other instance will report the disconnection as an error. This is expected IC behavior. It's valid to have more than one client running simultaneously against a given server, but there's no automated support for this (ie. you must specify the same "--serverInstanceName" for each client, and manually compute the "--expectedFinalBytes" from the sum of all data that each client sends).

> :warning: To test recovery scenarios, the `"deleteLogs"` setting in ambrosiaConfig.json must be set to `"false"`.

&nbsp;

## :wastebasket: Cleaning Up

Sometimes it can be necessary to reset a PTI test to a "clean slate" state, where the instances involved are no longer registered and their log/checkpoint files have been deleted. To simplify this, the Node.js LB supports several command-line arguments:

Parameter Name | Description | Example Usage
-|-|-
eraseInstance | For the instanceName/appVersion/replicaNumber in ambrosiaConfig.json, removes all<br/>registration and other meta data from Azure, and deletes all log/checkpoint files | node .\out\main.js eraseInstance
eraseInstanceAndReplicas | Does an 'eraseInstance' for an instance and all its replicas (**[active/active](https://github.com/microsoft/AMBROSIA/blob/master/CONTRIBUTING/AMBROSIA_client_network_protocol.md#activeactive)** secondaries) | node .\out\main.js eraseInstanceAndReplicas
registerInstance | Has the same effect as setting `"autoRegister"` to `"TrueAndExit"` in ambrosiaConfig.json | node .\out\main.js registerInstance
autoRegister | Has the same effect as setting `"autoRegister"` to `true` in ambrosiaConfig.json<br/>(note that the instance will continue to startup after registration completes) | node .\out\main.js autoRegister

> **Tip:** Because these command-line arguments are part of the Node.js LB, not the PTI app, they will work with _any_ Node.js Ambrosia app.

> :warning: Both `eraseInstance` and `eraseInstanceAndReplicas` result in **permanent** and **irreversible** data loss. Consequently, both require user confirmation to proceed, for example:
````
2021/09/10 12:30:04.644: Logging output to C:\src\Git\Franklin\AmbrosiaJS\Ambrosia-Node\outputLogs\traceLog_20210910_123004.txt
2021/09/10 12:30:04.649: Ambrosia configuration loaded from 'ambrosiaConfig.json'
2021/09/10 12:30:04.650: Warning: Are you sure you want to completely erase instance 'serverAA' (y/n)?
````
<br/>

## :speech_balloon: PTI Explained

The following is a deeper dive into what the PTI app does and how it works. Look in App\src and you will see that the app consists of 2 `.ts` files, and 2 generated `.g.ts` files (which were produced by "code-gen").
> :star: Unlike the C# LB, the Node.js LB doesn't have a separate tool for code-generation, rather it provides a simple code-gen API. Code-generation will be covered in more detail in the **[How the PTI App was Created](#%3Abulb%3A-how-the-pti-app-was-created)**.

* `Main.ts` does command-line parsing/validation then starts the Immortal instance; it also handles code-generation for the "published" Ambrosia methods in `PTI.ts` (a "published" method is a method that is callable by an Ambrosia instance, allowing the method to participate in the guarantees provided by the Ambrosia runtime).
* `PTI.ts` contains all the published methods and application state/logic for both the client and server. It consists of a `State` namespace that contains a special `AppState` class (which is a state object used, in our case, by _both_ client and server), a `ClientAPI` namespace, and a `ServerAPI` namespace.

There are 5 published methods:
* doWork (`ServerAPI`)
* reportState (`ServerAPI`)
* checkHealth (`ServerAPI`)
* continueSendingMessages (`ClientAPI`)
* doWorkEcho (`ClientAPI`)

When the client receives the 'InitialMessage' (from the IC), the LB's `onFirstStart()` event handler is called and this makes the initial self-call to 'continueSendingMessages'. This method builds a batch of server 'doWork' method calls then adds a self-call of 'continueSendingMessages' to the batch as a kind of tail-recursion to continue making progress. It then sends the batch to the [local] IC, which sends the calls (as method-invocation messages) to the server and client instances. As the client completes each "round" of work, it asks the server to report its progress by calling the 'reportState' method. While the server runs, it periodically makes a self-call of the 'checkHealth' method as a "heartbeat" to demonstrate that it remains responsive to incoming messages.

The client sends 'doWork' messages in a series of "rounds". The default is 1 round and is controlled by the --numOfRounds command-line parameter. Each round consists of 1 or more batches, with each batch containing 1 or more 'doWork' messages that are of a fixed size. Before starting the next round, the client optionally adjusts the size of the 'doWork' messages by varying the size of the byte-array parameter (the payload) being passed, which is the method's only parameter. The server keeps track of the number and cumulative payload size of the 'doWork' messages received, with the latter being used to determine whether the test has succeeded (note that checking the integrity of the byte-array parameter received is _not_ part of the test). The --expectedFinalBytes parameter tells the server how many 'doWork' message payload bytes it should expect to receive. If --bidirectional is specified, the server will also "echo" the 'doWork' call back to the client by calling the client's 'doWorkEcho' method; the client can verify the echoed messages by specifying a value for --expectedEchoedBytes. To more thoroughly verify the payload of the 'doWork' message (or its "echo" on a client), add the --verifyPayload flag.

The command-line parameters that control round/batch production are shown below, and the validation of these parameters (in `Main.ts`) ensures that they always specify a valid test configuration:

<!-- Note: We use the non-breaking hyphen character (&#x2011;) only for --noDescendingSize because its the longest string in the first column, so it prevents wrapping for ALL other values in column #1 -->
<div style="margin-bottom:10px">

Client Parameter | Description (from "--help")
-|-
--numOfRounds | The number of rounds (of size bytesPerRound) to work through; each round will use a [potentially] different message size; defaults to 1
--bytesPerRound | The total number of message payload bytes that will be sent in a single round; defaults to 1 GB
--batchSizeCutoff | Once the total number of message payload bytes queued reaches (or exceeds) this limit, then the batch will be sent; defaults to 10 MB
--maxMessageSize &#x00B9; | The maximum size (in bytes) of the message payload; must be a power of 2 (eg. 65536) and be at least 64; defaults to 64KB
&#x2011;&#x2011;noDescendingSize | Disables descending (halving) the message size with after each round; instead, a random size [power of 2] between 64 and &#x2011;&#x2011;maxMessageSize will be used
--fixedMessageSize | All messages (in all rounds) will be of size --maxMessageSize; &#x2011;&#x2011;noDescendingSize (if also supplied) will be ignored
</div>

 &#x00B9; By default, the largest value for --maxMessageSize is 32 MB (33554432). This limit is 50% of the `"maxMessageQueueSizeInMB"` setting in ambrosiaConfig.json, which defaults to 64 MB. So if a --maxMessageSize larger than 32 MB is required, then the `"maxMessageQueueSizeInMB"` setting must also be increased.

These parameters allow for very simple tests to be created. For example, here's a `Combined` instance test (run from the PTI-Node\App folder) that sends a single batch of 2 x 64 byte messages that are echoed back to the client (note that the "short-form" parameter names are being used in this example):
````PowerShell
node .\out\main.js -ir=Combined -n=1 -bpr=128 -mms=64 -bsc=128 -fms -bd -nhc -efb=128 -eeb=128
````
...or even more simply, by relying on defaults:
````PowerShell
node .\out\main.js -bpr=128 -mms=64 -bsc=128 -fms -bd -nhc
````

In addition to creating simple/quick "smoke" tests like this, the ability to precisely control message production can be used to help narrow down the repro case for an issue. It also makes debugging easier, since when the number of messages involved is small a more detailed `"outputLoggingLevel"` can be specified without resulting in an enormous/unwieldy output log.

Finally, the `"logTriggerSizeInMB"` in ambrosiaConfig.json is set to 256MB so that 4 checkpoints will be taken for every 1GB of messages sent. This allows testing recovery (from a checkpoint) after halting a participating instance before the test completes, even in the default case (1 round of 1 GB). This can be changed as needed to suit the needs of the test. Again, regarding recovery, be aware that the `"deleteLogs"` setting in ambrosiaConfig.json must be set to `"false"`.

> **Note:** Currently, PTI does not test 'post' method calls (methods that return values), nor does it test non-post methods that pass JSON parameters. PTI _only_ tests non-post methods that pass a single binary blob parameter (ie. "custom parameter encoding") so its **[code coverage](https://en.wikipedia.org/wiki/Code_coverage)** of the LB is limited.

&nbsp;

## :mag_right: Differences with the C# Language Binding version of PTI

While the C# and Node.js PTI apps both test very similar behavior, their differences are worth calling out - especially if you're already familiar with the C# version of PTI.

1. The "--instanceRole" parameter effectively replaces job.exe and server.exe. Further, Node.js PTI supports a single-instanced `Combined` role that simplifies running the test at the expense of being able to test fewer recovery scenarios (the 'client' and 'server' will always terminate at the same time).

2. There are several additional command-line parameters. Run with "--help" to see what each parameter does, which role they apply to, and (where applicable) what the default value is.
<div style="margin-left:40px; margin-bottom:10px">

--instanceRole\
--bytesPerRound (fixed at 1GB in C# PTI)\
--batchSizeCutoff\
--fixedMessageSize\
--noHealthCheck\
--expectedFinalBytes\
--expectedEchoedBytes (specified for the client when --bidirectional is specified for the server)\
--includePostMethod\
--verifyPayload

A few command-line parameters also have slightly different semantics:

The --autoContinue parameter defaults to true (not false) and requires a "=true|false" value (unlike C# PTI).\
The --notBidirectional parameter is replaced with --bidirectional, which defaults to false if not specified.\
The --maxMessageSize can be no smaller than 64 bytes, whereas in C# PTI it can be as small as 16 bytes.\
The --serverName parameter is replaced by --serverInstanceName &#x00B9;.\
The --jobName and --numOfJobs parameters have no equivalent; multiple clients simply specify the same --serverInstanceName.
</div>

3. These job.exe/server.exe command-line parameters are supported via ambrosiaConfig.json rather than as PTI app parameters:
<div style="margin-left:40px">

Parameter Name | Equivalent (in ambrosiaConfig.json)
-|-
--receivePort | "icReceivePort"
--sendPort | "icSendPort"
--ICPort | "icCraPort"
--icDeploymentMode | "icHostingMode" &#x00B2;
--log | "icLogFolder"
--checkpoint | "debugStartCheckpoint"
</div>

4. These server.exe command-line parameters are supported via ambrosiaConfig.json rather than as PTI app parameters:
<div style="margin-left:40px">

Parameter Name | Equivalent (in ambrosiaConfig.json)
-|-
--upgrading | "upgradeVersion"
--currentVersion | "appVersion"
</div>

&#x00B9; The client instance name is encoded into the first 64 bytes of the 'doWork' message payload (after the 4-byte call number and 1-byte instance name length), and so is limited to 59 bytes.<br/>
&#x00B2; The available icHostingMode's are not the same as the icDeploymentMode's; loosely, "SecondProc" (C#) corresponds to "Separated" (JS), and "InProcDeploy" corresponds to "Integrated".

&nbsp;

## :bulb: How the PTI App was Created

While not comprehensive, the following describes the "broad strokes" of how the PTI app was created, with an emphasis on the code-generation steps which are unique to the Node.js LB.

After installing the ambrosia-node npm package (see **[Getting Started](#%3Aarrow_forward%3A-getting-started)**), `PTI.ts` was created. Stubs for the 5 methods to be "published" were written and annotated with a special `@ambrosia` JSDoc tag, like this:

````TypeScript
/** 
 * A method that reports (to the console) the current application state.
 * @ambrosia publish=true, methodID=2
 */
export function reportState(isFinalState: boolean): void
{
}
````
Each published method was manually assigned a unique `methodID` (from 1 to 5) as an attribute of the JSDoc tag. In `Main.ts`, a simple codeGen() function was written that calls the code-gen API (`emitTypeScriptFileFromSource`) in the Node.js LB, specifying `PTI.ts` as the input source file:
````TypeScript
import Ambrosia = require("ambrosia-node"); 
import Meta = Ambrosia.Meta;

async function codeGen()
{
    await Ambrosia.initializeAsync(Ambrosia.LBInitMode.CodeGen);
    Meta.emitTypeScriptFileFromSource("./src/PTI.ts", { apiName: "PTI", fileKind: Meta.GeneratedFileKind.AllKind, mergeType: Meta.FileMergeType.None, outputPath: "./src" });
}
````
When run, this generated the `PublisherFramework.g.ts` and `ConsumerInterface.g.ts` files which could then be imported into `PTI.ts`:
````TypeScript
import * as PublishedAPI from "./ConsumerInterface.g"; // This is a generated file
import * as Framework from "./PublisherFramework.g"; // This is a generated file
````
> **Note:** Only in the case where an Ambrosia app (or service) can be both a 'client' _and_ the 'server' is it necessary to import _both_ the consumer-side (client) and publisher-side (server) generated files. In the typical case, the server instance will only import `PublisherFramework.g.ts`, and clients of the instance will only import `ConsumerInterface.g.ts` (which they must obtain from the publisher of the server).

The 5 stubbed methods were then implemented, since they now had access to the method wrappers (imported from `ConsumerInterface.g.ts`) necessary to call the published functions, eg:

````TypeScript
PublishedAPI.ClientAPI.continueSendingMessages_EnqueueFork(numRPCBytes, iterationWithinRound, startTimeOfRound);
````
The generated `State` namespace in `PublisherFramework.g.ts` was manually moved to `PTI.ts` so that it could be augmented without risk of being overwritten by a subsequent code-gen, and code-gen was re-run. Code-gen will detect the move and not re-generate this namespace (in `PublisherFramework.g.ts`), and will automatically fix up the [now broken] `State` references in `PublisherFramework.g.ts`.

Indeed, any time the name, type, or "shape" of any published entity (like a method) changes, code-gen must be re-run. Because of this, it's usually best to leave your `codeGen()` function in `Main.ts`, even after you switch over to using the app's normal `main()` entry-point. Depending on the changes that were made, it can be necessary to force code-gen to ignore errors in the input source file (`PTI.ts` in our case) by temporarily modifying the `fileOptions` parameter of `emitTypeScriptFileFromSource()` to include the `"ignoreTSErrorsInSourceFile: true"` option. Further, the changes may cause compilation errors in the existing PublisherFramework.g.ts and/or ConsumerInterface.g.ts (both of which your app may import) leading to this popup dialog (when running code-gen using VS Code): 
<div style="width: 600px; display: block; margin-left: auto; margin-right: auto; margin-top: 15px; margin-bottom: 10px">

![VSCode Compilation Errors Dialog](Images\CompilationErrors.png)

</div>
<center>Since you are running code-gen (which rebuilds these files), you can typically just click "Debug Anyway" (after confirming that <b>all</b> the compile errors are confined to the 2 generated files).</center>

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
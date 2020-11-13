// Module for the built-in IC test "app".
import Process = require("process");
import ChildProcess = require("child_process");
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as Configuration from "./Configuration";
import * as IC from "./ICProcess";
import * as Messages from "./Messages";
import * as Meta from "./Meta";
import * as Streams from "./Streams";
import * as StringEncoding from "./StringEncoding";
import * as Root from "./AmbrosiaRoot";
import * as Utils from "./Utils/Utils-Index";

/** This is like a mini Ambrosia-enabled app. */
export function startTest(): void
{
    class AppState extends Root.AmbrosiaAppState
    {
        counter: number = 0;
    }

    let _appState: AppState = new AppState(); 
    let _tempFolder: string = "C:/Bits";
    let _canAcceptKeyStrokes: boolean = false;
    let _isStopping: boolean = false;

    function stopICTest()
    {
        if (!_isStopping)
        {
            _isStopping = true;
            Utils.consoleInputStop();
            IC.stop();
            
            // Give the IC process a chance to emit its "close" event
            // Note: Without calling Process.exit() Node will wait for any pending timers before exiting.
            //       As an alternative, we could keep track of all pending timers and then unref() them 
            //       in IC.stop(), but this would add considerable overhead.
            setTimeout(() => Process.exit(0), 250); 
        }
    }

    /** Handler for errors from the IC process. */
    function onICError(source: string, error: Error, isFatalError: boolean = true): void
    {
        Utils.logWithColor(Utils.ConsoleForegroundColors.Red, `${error.stack}`, `[IC:${source}]`);
        if (isFatalError)
        {
            stopICTest();
        }
    }

    function bootstrapMethod(version: string)
    {
        Utils.log(`JS Language Binding Version: ${version}`, "[App]");
    }

    function greetingsMethod(name: string)
    {
        Utils.log(`Greetings, ${name}!`, "[App]");
    }

    /** This method responds to incoming Ambrosia messages (mainly RPCs, but also the InitialMessage and AppEvents). */
    function messageDispatcher(message: Messages.DispatchedMessage): void
    {
        // Fast (non-async) handler for high-volume messages
        if (!dispatcher(message))
        {
            // Slower async handler, but simpler/cleaner to code because we can use 'await'
            // Note: messageDispatcher() is NOT awaited by the calling code, so we don't await dispatcherAsync(). Consequently, any await's in 
            //       dispatcherAsync() will start independent Promise chains, and these chains are explicitly responsible for managing any 
            //       order-of-execution synchronization issues (eg. if the handling of message n is dependent on the handling of message n - 1).
            dispatcherAsync(message);
        }
    }

    let _maxPerfIteration: number = -1;

    function dispatcher(message: Messages.DispatchedMessage): boolean
    {
        let handled: boolean = false;

        // Special case: This is a very high-frequence message [used during perf-testing], so we want to handle it from a non-async dispatcher()
        if (message.type === Messages.DispatchedMessageType.RPC)
        {
            let rpc: Messages.IncomingRPC = message as Messages.IncomingRPC;
            if (rpc.methodID === 200)
            {
                // Fork perf test
                let buffer: Buffer = Buffer.from(rpc.rawParams.buffer);
                let iteration: number = buffer.readInt32LE(8); // We always need to read this [it changes with every message]
                if (_maxPerfIteration === -1)
                {
                    _maxPerfIteration = buffer.readInt32LE(12); // This is only sent with the first message
                }
                if (iteration === _maxPerfIteration)
                {
                    let startTime: number = Number(buffer.readBigInt64LE(0)); // This is only sent with the last message
                    let elapsedMs: number = Date.now() - startTime;
                    let requestsPerSecond: number = (_maxPerfIteration / elapsedMs) * 1000;
                    Utils.log(`startTime: ${Utils.getTime(startTime)}, iteration: ${iteration}, elapsedMs: ${elapsedMs}, RPS = ${requestsPerSecond.toFixed(2)}`, null, Utils.LoggingLevel.Minimal);
                }
                else
                {
                    if (iteration % 5000 === 0)
                    {
                        // Utils.log(`Received message #${iteration}`, null, Utils.LoggingLevel.Minimal);
                    }
                }
                handled = true;
            }
        }
        return (handled);
    }

    async function dispatcherAsync(message: Messages.DispatchedMessage)
    {
        const loggingPrefix: string = "Dispatcher";

        try
        {
            switch (message.type)
            {
                case Messages.DispatchedMessageType.RPC:
                    let rpc: Messages.IncomingRPC = message as Messages.IncomingRPC;

                    if (Utils.canLog(Utils.LoggingLevel.Normal)) // We add this check because this is a high-volume code path, and rpc.makeDisplayParams() is expensive
                    {
                        Utils.log(`Received ${Messages.RPCType[rpc.rpcType]} RPC call for ${rpc.methodID === IC.POST_METHOD_ID ? `post method '${IC.getPostMethodName(rpc)}' (version ${IC.getPostMethodVersion(rpc)})` : `method ID ${rpc.methodID}`} with params ${rpc.makeDisplayParams()}`, loggingPrefix);
                    }
                    
                    switch (rpc.methodID)
                    {
                        // Note: To get to this point, the post method has been verified as published
                        case IC.POST_METHOD_ID:
                            try
                            {
                                let methodName: string = IC.getPostMethodName(rpc);
                                let methodVersion: number = IC.getPostMethodVersion(rpc); // Use this to do version-specific method behavior
                        
                                switch (methodName)
                                {
                                    case "ComputePI":
                                        let digits: { count: number } = IC.getPostMethodArg(rpc, "digits?") ?? { count: 10 };
                                        let pi: number = Number.parseFloat(Math.PI.toFixed(digits.count));
                                        IC.postResult<number>(rpc, pi);
                                        break;
                                    default:
                                        let errorMsg: string = `Post method '${methodName}' is not implemented`;
                                        Utils.log(`(${errorMsg})`, loggingPrefix)
                                        IC.postError(rpc, new Error(errorMsg));
                                        break;
                                }
                            }
                            catch (error)
                            {
                                Utils.log(error);
                                IC.postError(rpc, error);
                            }
                            break;

                        case 33:
                            let opName: string = rpc.jsonParams["opName"];
                            let formattedXml: string = "";
                            switch (opName)
                            {
                                case "getPublishedMethods":
                                    let methodListXml: string = await Meta.getPublishedMethodsAsync(config.icInstanceName);
                                    Utils.logHeader(`Available methods on '${config.icInstanceName}':`);
                                    formattedXml = Utils.formatXml(Utils.decodeXml(methodListXml));
                                    Utils.log(formattedXml.indexOf(Utils.NEW_LINE) === -1 ? formattedXml : Utils.NEW_LINE + formattedXml);
                                    break;
                                case "getPublishedTypes":
                                    let typeListXml: string = await Meta.getPublishedTypesAsync(config.icInstanceName);
                                    Utils.logHeader(`Available types on '${config.icInstanceName}':`);
                                    formattedXml = Utils.formatXml(typeListXml);
                                    Utils.log(formattedXml.indexOf(Utils.NEW_LINE) === -1 ? formattedXml : Utils.NEW_LINE + formattedXml);
                                    break;
                                case "isPublishedMethod":
                                    let methodName: string = rpc.jsonParams["methodName"];
                                    let methodVersion: number = parseInt(rpc.jsonParams["methodVersion"]);
                                    let isPublished: boolean = await Meta.isPublishedMethodAsync(config.icInstanceName, methodName, methodVersion);
                                    Utils.log(`Method '${methodName}' (version ${methodVersion}) is ${isPublished ? "published" : "not published"}`);
                                    break;
                                case "runForkPerfTest":
                                    // To get the best performance:
                                    // 1) Set the 'outputLoggingLevel' to 'Minimal', and 'outputLogDestination' to 'File'.
                                    // 2) The dispatcher() function should NOT be async.
                                    // 3) Messages should be batched using RPCBatch.
                                    // 4) The IC binary must be a release build (not debug).
                                    // 5) Run the test OUTSIDE of the debugger.
                                    if (Configuration.loadedConfig().lbOptions.outputLoggingLevel !== Utils.LoggingLevel.Minimal)
                                    {
                                        Utils.log("Incorrect configuration for test: 'outputLoggingLevel' must be 'Minimal'");
                                        break;
                                    }
                                    const maxIteration: number = 1000000;
                                    const batchSize: number = 10000;
                                    Utils.log(`Starting Fork performance test [${maxIteration.toLocaleString()} messages in batches of ${batchSize.toLocaleString()}]...`, null, Utils.LoggingLevel.Minimal);
                                    let startTime: number = Date.now();
                                    let buffer: Buffer = Buffer.alloc(16);
                                    _maxPerfIteration = -1;
                                    for (let i = 0; i < maxIteration; i++)
                                    {
                                        let methodID: number = 200;
                                        if (i === maxIteration - 1)
                                        {
                                            buffer.writeBigInt64LE(BigInt(startTime), 0);
                                        }
                                        buffer.writeInt32LE(i + 1, 8);
                                        if (i === 0)
                                        {
                                            buffer.writeInt32LE(maxIteration, 12);
                                        }
                                        IC.queueFork(config.icInstanceName, methodID, buffer);
                                        if (((i + 1) % batchSize === 0) || (i === maxIteration - 1))
                                        {
                                            await IC.flushAsync();
                                            // Utils.log(`Sent batch of #${batchSize} messages`, null, Utils.LoggingLevel.Minimal);
                                        }
                                    }
                                    break;
                                default:
                                    Utils.log(`Error: Unknown Impulse operation '${opName}'`);
                                    break;
                            }
                            break;

                        case 3:
                            let name: string = rpc.jsonParams["name"];
                            greetingsMethod(name);
                            break;
                        case 204:
                            let raw: Uint8Array = rpc.rawParams;
                            let lbVersion = StringEncoding.fromUTF8Bytes(raw);
                            // let lbVersion: string = rpc.jsonParams["languageBindingVersion"];
                            bootstrapMethod(lbVersion);
                            break;
                        default:
                            Utils.log(`(No method is associated with methodID ${rpc.methodID})`, loggingPrefix)
                            break;
                    }
                    break;

                case Messages.DispatchedMessageType.AppEvent:
                    let appEvent: Messages.AppEvent = message as Messages.AppEvent;

                    switch (appEvent.eventType)
                    {
                        case Messages.AppEventType.ICStarting:
                            // let tokens: string[] = Meta.Type.tokenizeComplexType("{ foo: string[], name: { firstName: string, lastName: { middleInitial: { mi: string }[], lastName: string }[][] }[][][], startDate: number }");
                            // Meta.publishType("rpcType", "number", Messages.RPCType);
                            // Meta.publishType("rpcType", "number", Messages.RPCType);
                            // Meta.publishType("surname", "{ middleInitial: string, lastName: string }");
                            // Meta.publishType("name", "{ firstName: string, lastName: surname[][] }"); // Note that this references the 'surname' custom type
                            // Meta.publishType("employee", "{ name: name, startDate: number }");
                            // let employeeInstance: object = {name: {firstName:"foo", lastName: [ [{ middleInitial: "x", lastName: "bar" }] ]}, startDate: 123}; // Alternative to the following definition
                            // // let employeeInstance: object = {name: {firstName:"foo", lastName: []}, startDate: 123}; // Alternative to the previous definition
                            // let employeeRuntimeType: string = Meta.Type.getRuntimeType(employeeInstance);
                            // let employeePublishedType: string = Meta.getPublishedType("employee").expandedDefinition;
                            // let match: boolean = (Meta.Type.compareComplexTypes(employeePublishedType, employeeRuntimeType) === null);
                            // let person1: object = [{ name: { firstName: "Mickey", lastName: { middleInitials: ["x", "y"] , surName: "Mouse" }}}];
                            // let person1RuntimeType: string = Meta.Type.getRuntimeType(person1);
                            // let person2: object = [[ { name: "Mickey"} ]];
                            // let person2RuntimeType: string = Meta.Type.getRuntimeType(person2);
                            // let person3: object = ["foo", "bar"];
                            // let person3RuntimeType: string = Meta.Type.getRuntimeType(person3);

                            // Meta.publishPostMethod("getEmployee", 1, ["employeeID:number"], "{      names: { firstName: string[], lastName: string }, startDate: number[], jobs: {title: string, durationInSeconds: bigint[] }[] }");
                            // Meta.publishType("employee", "   {    names: { firstNames: string[], lastName:string}    ,     startDate: number[], jobs: {title: string, durationInSeconds: bigint[] }      []   }");
                            // Meta.publishPostMethod("getEmployee", 1, ["employeeID:number"], "employee");
                            
                            // let numberArray: number[] = [1, 2, 3];
                            // let stringArray: string[] = ['1', '2', '3'];
                            // let typedArrayArray: Uint8Array[] = [Uint8Array.from([0]), Uint8Array.from([1])];
                            // let type: string = "";
                            // let employee: object = { foo: { bar: "hello" }, name: { firstName: "mickey", lastName: "mouse", aliases: { workAliases: ["Boss"], homeAliases: ["BigEars"] } }, age: 92 };
                            // let employee2: object = { foo: [ { names: [ { name: "hello" }, { name: "world" } ] }, { names: [ { name: "hello!" }, { name: "world!" } ] } ] };
                            // type = Meta.Type.getRuntimeType(type);
                            // type = Meta.Type.getRuntimeType(employee);
                            // type = Meta.Type.getRuntimeType(employee2);
                            // type = Meta.Type.getRuntimeType(numberArray);
                            // type = Meta.Type.getRuntimeType(stringArray);
                            // type = Meta.Type.getRuntimeType(typedArrayArray);

                            // Meta.publishType("TestType", "{ name: { first: string, last: string }, foo: Digits }");
                            // Meta.publishType("TestSimpleType", "Digits[][]");
                            // Meta.publishPostMethod("TestMethod", 1, ["raw: Uint8Array", "foo: string"], "number");

                            Meta.publishType("Digits", "{ count: number }");
                            Meta.publishPostMethod("ComputePI", 1, ["digits?: Digits"], "number");
                            Meta.publishMethod(204, "bootstrap", ["raw:Uint8Array"]);
                            Meta.publishMethod(3, "greetings", ["name:string"]);
                            break;
                        case Messages.AppEventType.ICStopped:
                            let exitCode: number = appEvent.args[0];
                            stopICTest();
                            break;
                        case Messages.AppEventType.RecoveryComplete:
                            Utils.log("Normal app processing can begin", loggingPrefix);
                            _canAcceptKeyStrokes = true;
                            break;
                        case Messages.AppEventType.UpgradeStateAndCode:
                            let upgradeMode: Messages.AppUpgradeMode = appEvent.args[0] as Messages.AppUpgradeMode;
                            Utils.log(`Performed upgrade of app state and code [in '${Messages.AppUpgradeMode[upgradeMode]}' mode]`);
                            break;
                        case Messages.AppEventType.FirstStart:
                            try
                            {
                                let now: number = await IC.replayableValueAsync(Date.now());
                                Utils.log(`Now is: ${Utils.getTime(now)}`);
                            }
                            catch (e)
                            {
                                Utils.log(`Error: ${(e as Error).message}`);
                            }
                            // IC.replayableValue<number>(Date.now(), (value: number, error?: Error) =>
                            // {
                            //     if (!error)
                            //     {
                            //         let now: number = value;
                            //         Utils.log(`Now is: ${Utils.getTime(now)}`);
                            //     }
                            //     else
                            //     {
                            //         Utils.log(`Error: ${error.message}`);
                            //     }
                            // });
                            break;
                    }
                    break;
            }
        }
        catch (error)
        {
            let messageName: string = (message.type === Messages.DispatchedMessageType.AppEvent) ? `AppEvent:${Messages.AppEventType[(message as Messages.AppEvent).eventType]}` : Messages.DispatchedMessage[message.type];
            Utils.log(`Error: Failed to process ${messageName} message`);
            Utils.log(error);
        }
    }

    /** Serializes the app state and returns an OutgoingCheckpoint object. */
    function checkpointProducer(): Streams.OutgoingCheckpoint
    {
        /*
        // Create a stream that will be used as the output for serialized app state
        // TODO: This is just a test stream
        let testCheckpointFileName: string = Path.join(_tempFolder, "GeneratedCheckpoint.dat");
        let checkpointLength: number = 123; // (1024 * 1024 * 100) + 123; // 100 MB

        if ((Utils.getFileSize(testCheckpointFileName) != checkpointLength))
        {
            Utils.createTestFile(testCheckpointFileName, checkpointLength)
        }

        let checkpointStream: Stream.Readable = fs.createReadStream(testCheckpointFileName);
        
        // When the checkpoint stream has been sent, OutgoingCheckpoint.onFinished() will be called.
        return ({ dataStream: checkpointStream, length: checkpointLength, onFinished: onCheckpointSent });
        */

        function onCheckpointSent(error?: Error): void
        {
            Utils.log(`checkpointProducer: ${error ? `Failed (reason: ${error.message})` : "Checkpoint saved"}`)
        }
        return (Streams.simpleCheckpointProducer(Utils.jsonStringify(_appState), onCheckpointSent));
    }

    /** Returns an IncomingCheckpoint object used to receive a checkpoint of app state. */
    function checkpointConsumer(): Streams.IncomingCheckpoint
    {
        /*
        // Create a stream that will be used as the input to deserialize and load a checkpoint of app state
        // TODO: This is just a test stream
        let receiverStream: Stream.Writable = fs.createWriteStream(Path.join(_tempFolder, "ReceivedCheckpoint.dat")); 
        return ({ dataStream: receiverStream, onFinished: null });
        */

        function onCheckpointReceived(jsonAppState: string, error?: Error): void
        {
            if (!error)
            {
                _appState = Utils.jsonParse(jsonAppState);
            }
            Utils.log(`checkpointConsumer: ${error ? `Failed (reason: ${error.message})` : "Checkpoint loaded"}`);
        }
        return (Streams.simpleCheckpointConsumer(onCheckpointReceived));
    }

    Utils.log("IC test running: Press 'X' (or 'Enter') to stop, or 'H' to list all available test commands" );

    let config: Configuration.AmbrosiaConfig = new Configuration.AmbrosiaConfig(messageDispatcher, checkpointProducer, checkpointConsumer, onICError);
    let icProcess: ChildProcess.ChildProcess = IC.start(config, _appState);

    // Detect when 'x' (or 'Enter') is pressed, or other message-generating key (eg. F[ork], I[mpulse], B[atch], [P]ost)
    Utils.consoleInputStart(handleKeyStroke);

    async function handleKeyStroke(char: string)
    {
        let startTime: number = 0;

        if (!_canAcceptKeyStrokes)
        {
            return;
        }

        try
        {
            switch (char)
            {
                case "h":
                    Utils.logHeader("Available test commands:")
                    Utils.log("X: Exit (stop) the test");
                    Utils.log("F: Call Fork RPC");
                    Utils.log("I: Call Impulse RPC");
                    Utils.log("B: Call Batched RPCs");
                    Utils.log("P: Post RPC");
                    Utils.log("M: Get published methods");
                    Utils.log("T: Get published types");
                    Utils.log("S: Check if a method (of a specific version) is published");
                    Utils.log("T: Get published types");
                    Utils.log("Z: Run Fork performance test");
                    break;
                case "x":
                case Utils.ENTER_KEY:
                    Utils.log("IC test stopping");
                    stopICTest();
                    break;
                case "f": // Fork
                    // IC.callFork(config.icInstanceName, 3, { name: "AmbrosiaJS" });
                    IC.callFork(config.icInstanceName, 204, StringEncoding.toUTF8Bytes(Root.languageBindingVersion()));
                    break;
                case "i": // Impulse
                    IC.callImpulse(config.icInstanceName, 3, { name: "AmbrosiaJS" });
                    break;
                case "b": // [Explicit] Batch
                    IC.queueFork(config.icInstanceName, 3, { name: "John" })
                    IC.queueFork(config.icInstanceName, 3, { name: "Paul" })
                    IC.queueFork(config.icInstanceName, 3, { name: "George" })
                    IC.queueFork(config.icInstanceName, 3, { name: "Ringo" })
                    await IC.flushAsync();
                    break;
                case "l": // [Implicit] Batch
                    IC.callFork(config.icInstanceName, 3, { name: "BatchedMsg1" });
                    IC.callFork(config.icInstanceName, 3, { name: "BatchedMsg2" });
                    break;
                case "p": // Post
                    /*
                    if (config.icInstanceName !== "test")
                    {
                        IC.post("test", "ComputePI", 1, (result?: any, errorMsg?: string) => Utils.log(errorMsg ? errorMsg : (result ? `PI = ${result}` : "ComputePI returned void")), 5);
                    }
                    */
                    IC.post(config.icInstanceName, "ComputePI", 1, (result?: number, errorMsg?: string) => Utils.log(errorMsg ? "Error: " + errorMsg : (result ? `PI = ${result}` : "ComputePI returned void")), -1, IC.arg("digits?", { count: 5 }));
                    break;
                case "r": // Replayable
                    startTime = Date.now();
                    let iterations: number = 1;
                    let tasks: Promise<any>[] = [];
                    let results: number[] = [];

                    for (let i = 0; i < iterations; i++)
                    {
                        tasks.push(IC.replayableValueAsync(Date.now()));
                    }
                    
                    results = await Promise.all(tasks);
                    
                    for (let i = 0; i < iterations; i++)
                    {
                        Utils.log(`Now is: ${Utils.getTime(results[i])}`);
                    }

                    Utils.log(`ElapsedMs: ${Date.now() - startTime}`);

                    // let now: number = await IC.replayableValueAsync(Date.now());
                    // Utils.log(`Now is: ${Utils.getTime(now)}`);
                    break;
                case "m": // getPublishedMethods
                    IC.callImpulse(config.icInstanceName, 33, { opName: "getPublishedMethods" });
                    break;
                case "t": // getPublishedTypes
                    IC.callImpulse(config.icInstanceName, 33, { opName: "getPublishedTypes" });
                    break;
                case "s": // isPublishedMethod
                    IC.callImpulse(config.icInstanceName, 33, { opName: "isPublishedMethod", methodName: "ComputePI", methodVersion: 1 });
                    break;
                case "z": // Fork perf test
                    IC.callImpulse(config.icInstanceName, 33, { opName: "runForkPerfTest" });
                    break;
            }
        }
        catch (error)
        {
            Utils.log("Error: " + (error as Error).message);
        }
    }
}
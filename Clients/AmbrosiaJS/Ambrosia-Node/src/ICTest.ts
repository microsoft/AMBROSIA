// Module for the built-in IC test "app".
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as Configuration from "./Configuration";
import * as IC from "./ICProcess";
import * as Messages from "./Messages";
import * as Meta from "./Meta";
import * as Streams from "./Streams";
import * as StringEncoding from "./StringEncoding";
import * as Root from "./AmbrosiaRoot";
import * as Utils from "./Utils/Utils-Index";

/** [Internal] An embedded "Ambrosia-enabled app" used for developing/testing ambrosia-node. */
export function startTest(): void
{
    // TODO: This is just a placeholder while we work on developing AppState.upgrade().
    class AppStateVNext extends Root.AmbrosiaAppState
    {
        counter: number;
        last: Date; // New to AppStateVNext

        constructor(restoredAppState?: AppStateVNext)
        {
            super(restoredAppState);

            if (restoredAppState)
            {
                // Re-initialize application state from restoredAppState
                // WARNING: You MUST reinstantiate all members that are (or contain) class references because restoredAppState is data-only
                this.counter = restoredAppState.counter;
                this.last = restoredAppState.last;
            }
            else
            {
                // Initialize application state
                this.counter = 0;
                this.last = new Date(0);
            }
        }

        static fromPriorAppState(oldAppState: AppState): AppStateVNext
        {
            const appState: AppStateVNext = new AppStateVNext();

            // Upgrading, so transform (as needed) the supplied old state, and - if needed - [re]initialize the new state
            appState.counter = oldAppState.counter;
            appState.last = new Date(Date.now());

            return (appState);
        }
    }

    class AppState extends Root.AmbrosiaAppState
    {
        counter: number = 0;
        padding: Uint8Array;

        constructor(restoredAppState?: AppState)
        {
            super(restoredAppState);

            if (restoredAppState)
            {
                this.counter = restoredAppState.counter;
                this.padding = restoredAppState.padding;
            }
            else
            {
                this.padding = new Uint8Array(1 * 1024 * 1024);
                this.padding[0] = 123; 
                this.padding[this.padding.length - 1] = 170;
            }
        }

        // We must override convert() to support upgrade: convert() is called by AmbrosiaAppState.upgrade(), which we should call when
        // we receive an AppEventType.UpgradeState, eg. let _newAppState: AppStateVNext = _appState.upgrade<AppStateVNext>(AppStateVNext);
        override convert(): AppStateVNext
        {
            return (AppStateVNext.fromPriorAppState(this));
        }
    }

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
        }
    }

    function reportVersion(version: string)
    {
        Utils.log(`JS Language Binding Version: ${version}`, "[App]");
    }

    function greetingsMethod(name: string)
    {
        Utils.log(`Greetings, ${name}!`, "[App]");
    }

    let _maxPerfIteration: number = -1; // The total number of messages that will be sent in a perf test

    /** This method responds to incoming Ambrosia messages (RPCs and AppEvents). */
    function messageDispatcher(message: Messages.DispatchedMessage): void
    {
        // WARNING! Rules for Message Handling:
        //
        // Rule 1: Messages must be handled - to completion - in the order received. For application (RPC) messages only, if there are messages that are known to
        //         be commutative then this rule can be relaxed - but only for RPC messages. 
        // Reason: Using Ambrosia requires applications to have deterministic execution. Further, system messages (like TakeCheckpoint) from the IC rely on being 
        //         handled in the order they are sent to the app. This means being extremely careful about using non-synchronous code (like awaitable operations
        //         or callbacks) inside message handlers: the safest path is to always only use synchronous code.
        //         
        // Rule 2: Before a TakeCheckpoint message can be handled, all handlers for previously received messages must have completed (ie. finished executing).
        //         If Rule #1 is followed, the app is automatically in compliance with Rule #2.
        // Reason: Unless your application has a way to capture (and rehydrate) runtime execution state (specifically the message handler stack) in the serialized
        //         application state (checkpoint), recovery of the checkpoint will not be able to complete the in-flight message handlers. But if there are no 
        //         in-flight handlers at the time the checkpoint is taken (because they all completed), then the problem of how to complete them during recovery is moot. 
        
        dispatcher(message);
    }

    /** 
     * Synchronous Ambrosia message dispatcher.
     * 
     * **WARNING:** Avoid using any asynchronous features (async/await, promises, callbacks, timers, events, etc.). See "Rules for Message Handling" above. 
     */
    function dispatcher(message: Messages.DispatchedMessage): void
    {
        const loggingPrefix: string = "Dispatcher";

        // Special case: This is a very high-frequency message [used during perf-testing], so we handle it immediately
        if (message.type === Messages.DispatchedMessageType.RPC)
        {
            let rpc: Messages.IncomingRPC = message as Messages.IncomingRPC;
            if (rpc.methodID === 200)
            {
                // Fork perf test
                let buffer: Buffer = Buffer.from(rpc.getRawParams().buffer); // Use the ArrayBuffer to create a view [so no data is copied]
                let iteration: number = buffer.readInt32LE(8); // We always need to read this [it changes with every message]
                if (_maxPerfIteration === -1)
                {
                    _maxPerfIteration = buffer.readInt32LE(12); // This is only sent with the first message
                }
                else
                {
                    if (iteration === _maxPerfIteration)
                    {
                        let startTime: number = Number(buffer.readBigInt64LE(0)); // This is only sent with the last message
                        let elapsedMs: number = Date.now() - startTime;
                        let requestsPerSecond: number = (_maxPerfIteration / elapsedMs) * 1000;
                        Utils.log(`startTime: ${Utils.getTime(startTime)}, iteration: ${iteration}, elapsedMs: ${elapsedMs}, RPS = ${requestsPerSecond.toFixed(2)}`, null, Utils.LoggingLevel.Minimal);
                    }
                    else
                    {
                        // if (iteration % 5000 === 0)
                        // {
                        //     Utils.log(`Received message #${iteration}`, null, Utils.LoggingLevel.Minimal);
                        // }
                    }
                }
                return;
            }
        }

        try
        {
            switch (message.type)
            {
                case Messages.DispatchedMessageType.RPC:
                    let rpc: Messages.IncomingRPC = message as Messages.IncomingRPC;

                    if (Utils.canLog(Utils.LoggingLevel.Verbose)) // We add this check because this is a high-volume code path, and rpc.makeDisplayParams() is expensive
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
                                    case "joinNames":
                                        let namesSet: Set<string> = IC.getPostMethodArg(rpc, "namesSet");
                                        let namesArray: string[] = IC.getPostMethodArg(rpc, "namesArray");
                                        IC.postResult<string>(rpc, [...namesSet, ...namesArray].map(v => v || "null").join(","));
                                        break;
                                    case "postTimeoutTest":
                                        let resultDelayInMs: number = IC.getPostMethodArg(rpc, "resultDelayInMs?") ?? -1;
                                        if (resultDelayInMs > 0)
                                        {
                                            // Simulate a delay at the destination instance [although this an imperfect simulation since it delays the send, not the receive]
                                            setTimeout(() => IC.postResult<void>(rpc), resultDelayInMs);
                                        }
                                        else
                                        {
                                            // To [perfectly] simulate an infinite "delay" at the destination we simply don't call IC.postResult()
                                        }
                                        break;
                                    default:
                                        let errorMsg: string = `Post method '${methodName}' is not implemented`;
                                        Utils.log(`(${errorMsg})`, loggingPrefix)
                                        IC.postError(rpc, new Error(errorMsg));
                                        break;
                                }
                            }
                            catch (error: unknown)
                            {
                                const err: Error = Utils.makeError(error);
                                Utils.log(err);
                                IC.postError(rpc, err);
                            }
                            break;

                        case 3:
                            let name: string = rpc.getJsonParam("name");
                            greetingsMethod(name);
                            break;
    
                        case 33:
                            let opName: string = rpc.getJsonParam("opName");
                            switch (opName)
                            {
                                case "attachToBadInstance":
                                    // This is to help investigate bug #187
                                    IC.callFork("serverTen", 3, { name: "Foo!" });
                                    break;
                                case "sendLargeMessage":
                                    const sizeInKB: number = parseInt(rpc.getJsonParam("sizeInKB"));
                                    IC.echo_Post("x".repeat(1024 * sizeInKB), "sendLargeMessage");
                                    break;
                                case "requestCheckpoint":
                                    IC.requestCheckpoint();
                                    break;
                                case "reportVersion":
                                    IC.callFork(config.icInstanceName, 204, StringEncoding.toUTF8Bytes(Root.languageBindingVersion()));
                                    break;
                                case "implicitBatch":
                                    IC.callFork(config.icInstanceName, 3, { name: "BatchedMsg1" });
                                    IC.callFork(config.icInstanceName, 3, { name: "BatchedMsg2" });
                                    break;
                                case "explicitBatch":
                                    IC.queueFork(config.icInstanceName, 3, { name: "John" });
                                    IC.queueFork(config.icInstanceName, 3, { name: "Paul" });
                                    IC.queueFork(config.icInstanceName, 3, { name: "George" });
                                    IC.queueFork(config.icInstanceName, 3, { name: "Ringo" });
                                    IC.flushQueue();
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
                                    let methodID: number = 200;
                                    _maxPerfIteration = -1;

                                    sendBatch(0, batchSize, maxIteration);

                                    function sendBatch(startID: number, batchSize: number, messagesRemaining: number): void
                                    {
                                        batchSize = Math.min(batchSize, messagesRemaining);

                                        for (let i = startID; i < startID + batchSize; i++)
                                        {
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
                                        }

                                        IC.flushQueue();
                                        // Utils.log(`Sent batch of ${batchSize} messages`, null, Utils.LoggingLevel.Minimal);

                                        messagesRemaining -= batchSize;
                                        if (messagesRemaining > 0)
                                        {
                                            setImmediate(sendBatch, startID + batchSize, batchSize, messagesRemaining);
                                        }
                                    }
                                    break;
                                default:
                                    Utils.log(`Error: Unknown Impulse operation '${opName}'`);
                                    break;
                            }
                            break;
                        
                        case 204:
                            let rawParams: Uint8Array = rpc.getRawParams();
                            let lbVersion = StringEncoding.fromUTF8Bytes(rawParams);
                            // let lbVersion: string = rpc.jsonParams["languageBindingVersion"];
                            reportVersion(lbVersion);
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
                        case Messages.AppEventType.ICConnected:
                            // Note: Types and methods are published in this handler so that they're available regardless of the 'icHostingMode'
                            publishEntities(); 
                            break;
                        case Messages.AppEventType.ICStarting:
                            break;
                        case Messages.AppEventType.ICStopped:
                            const exitCode: number = appEvent.args[0];
                            stopICTest();
                            break;
                        case Messages.AppEventType.BecomingPrimary:
                            Utils.log("Normal app processing can begin", loggingPrefix);
                            _canAcceptKeyStrokes = true;
                            break;
                        case Messages.AppEventType.UpgradeState:
                            {
                                const upgradeMode: Messages.AppUpgradeMode = appEvent.args[0];
                                _appState = _appState.upgrade<AppStateVNext>(AppStateVNext);
                                break;
                            }
                        case Messages.AppEventType.UpgradeCode:
                            {
                                const upgradeMode: Messages.AppUpgradeMode = appEvent.args[0];
                                IC.upgrade(messageDispatcher, checkpointProducer, checkpointConsumer, postResultDispatcher); // A no-op code upgrade
                                break;
                            }
                        case Messages.AppEventType.FirstStart:
                            IC.echo_Post(Date.now(), "now");
                            break;
                    }
                    break;
            }
        }
        catch (error: unknown)
        {
            let messageName: string = (message.type === Messages.DispatchedMessageType.AppEvent) ? `AppEvent:${Messages.AppEventType[(message as Messages.AppEvent).eventType]}` : Messages.DispatchedMessageType[message.type];
            Utils.log(`Error: Failed to process ${messageName} message`);
            Utils.log(Utils.makeError(error));
        }
    }

    function publishEntities(): void
    {
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
        // Meta.publishPostMethod("TestMethod", 1, ["rawParams: Uint8Array", "foo: string"], "number");

        Meta.publishType("Digits", "{ count: number }");
        Meta.publishPostMethod("ComputePI", 1, ["digits?: Digits"], "number");
        Meta.publishMethod(204, "bootstrap", ["rawParams:Uint8Array"]);
        Meta.publishMethod(3, "greetings", ["name:string"]);
        Meta.publishPostMethod("joinNames", 1, ["namesSet: Set<string>", "namesArray: string[]"], "string");
        Meta.publishPostMethod("postTimeoutTest", 1, ["resultDelayInMs?: number"], "void");
    }

    // Handler for the results of previously called post methods (in Ambrosia, only 'post' methods return values). See Messages.PostResultDispatcher.
    function postResultDispatcher(senderInstanceName: string, methodName: string, methodVersion: number, callID: number, callContextData: any, result: any, errorMsg: string): boolean
    {
        let handled: boolean = true;

        if (errorMsg)
        {
            Utils.log(`Error: ${errorMsg}`);
        }
        else
        {
            switch (methodName)
            {
                case "_echo": // The result from IC.echo_Post()
                    switch (callContextData)
                    {
                        case "now":
                            const now: number = result;
                            Utils.log(`Now is: ${Utils.getTime(now)}`);
                            break;
                        case "sendLargeMessage":
                            const s: string = result;
                            Utils.log(`Large message size: ${(s.length / 1024.0)} KB`, null, Utils.LoggingLevel.Minimal);
                            break;
                        default:
                            handled = false;
                    }
                    break;
                case "_getPublishedMethods": // The result from Meta.getPublishedMethods_Post()
                    const methodListXml: string = result;
                    const formattedMethodListXml: string = Utils.formatXml(Utils.decodeXml(methodListXml));
                    Utils.logHeader(`Available methods on '${callContextData.targetInstanceName}':`);
                    Utils.log(formattedMethodListXml.indexOf(Utils.NEW_LINE) === -1 ? formattedMethodListXml : Utils.NEW_LINE + formattedMethodListXml);
                    break;
                case "_getPublishedTypes": // The result from Meta.getPublishedTypes_Post()
                    const typeListXml: string = result;
                    const formattedTypeListXml: string = Utils.formatXml(typeListXml);
                    Utils.logHeader(`Available types on '${callContextData.targetInstanceName}':`);
                    Utils.log(formattedTypeListXml.indexOf(Utils.NEW_LINE) === -1 ? formattedTypeListXml : Utils.NEW_LINE + formattedTypeListXml);
                    break;
                case "_isPublishedMethod": // The result from Meta.isPublishedMethod_Post()
                    const isPublished: boolean = result;
                    Utils.log(`Method '${callContextData.targetMethodName}' (version ${callContextData.targetMethodVersion}) is ${isPublished ? "published" : "not published"}`);
                    break;
                case "ComputePI":
                    Utils.log(result ? `PI = ${result}` : "ComputePI returned void");
                    break;
                case "joinNames":
                    Utils.log(`Joined names = ${result}`);
                    break;
                case "_ping":
                    const roundtripTimeInMs: number = result;
                    const pingResult: string = (roundtripTimeInMs === -1) ? `failed [after ${callContextData.timeoutInMs}ms]` : `succeeded [round-trip time: ${roundtripTimeInMs}ms]`;
                    Utils.log(`Ping of '${callContextData.destinationInstance}' ${pingResult}`);
                    break;
                default:
                    handled = false;
            }
        }
        return (handled);
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
        return (Streams.simpleCheckpointProducer(_appState, onCheckpointSent));
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

        function onCheckpointReceived(appState?: Root.AmbrosiaAppState, error?: Error): void
        {
            if (!error)
            {
                if (!appState) // Should never happen
                {
                    throw new Error(`An appState object was expected, not ${appState}`);
                }
                _appState = appState as AppState;
            }
            Utils.log(`checkpointConsumer: ${error ? `Failed (reason: ${error.message})` : "Checkpoint loaded"}`);
        }
        return (Streams.simpleCheckpointConsumer<AppState>(AppState, onCheckpointReceived));
    }

    let config: Configuration.AmbrosiaConfig = new Configuration.AmbrosiaConfig(messageDispatcher, checkpointProducer, checkpointConsumer, postResultDispatcher);
    Utils.log(`IC test running: Press 'X' (or 'Enter') to stop${config.isIntegratedIC && !config.isTimeTravelDebugging ? ", or 'H' to list all available test commands" : ""}`);
    // @ts-tactical-any-cast: Suppress error "Argument of type 'typeof AppState' is not assignable to parameter of type 'new (restoredAppState?: AppStateVNext | AppState | undefined) => AppStateVNext | AppState' ts(2345)" [because we use 'strictFunctionTypes']
    let _appState: AppState | AppStateVNext = IC.start<AppState | AppStateVNext>(config, AppState as any); 

    // Test of AppState upgrade
    // _appState = _appState.upgrade<AppStateVNext>(AppStateVNext);

    // Detect when 'x' (or 'Enter') is pressed, or other message-generating key (eg. F[ork], B[atch], [P]ost)
    Utils.consoleInputStart(handleKeyStroke);

    // Add handler for Ctrl+Break [this only works for Windows and only for Cmd.exe; PowerShell reserves Ctrl+Break to break into the script debugger]
    process.on("SIGBREAK", () =>
    { 
        handleKeyStroke(String.fromCharCode(3)); // 3 is the "Ctrl+C" code
    });

    async function handleKeyStroke(char: string)
    {
        const isCtrlC: boolean = (char.charCodeAt(0) === 3);
        const runningTTDorSeparated: boolean = (config.isIntegratedIC && config.isTimeTravelDebugging) || (config.icHostingMode === Configuration.ICHostingMode.Separated);

        // Always allow Ctrl+C, and always allow 'X' and 'Enter' when in TTD mode (if running Integrated), or when running in 'Separated' IC mode
        if (isCtrlC || (runningTTDorSeparated && ((char === 'x') || (char === Utils.ENTER_KEY))))
        {
            Utils.log("IC test stopping");
            stopICTest();
            return;
        }

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
                    Utils.log("I: Call implicitly batched RPC");
                    Utils.log("B: Call explicitly batched RPCs");
                    Utils.log("P: Post RPC");
                    Utils.log("O: Post RPC Timeout Test (10 seconds)");
                    Utils.log("M: Get published methods");
                    Utils.log("T: Get published types");
                    Utils.log("S: Check if a method (of a specific version) is published");
                    Utils.log("R: Request checkpoint");
                    Utils.log("E: Echo");
                    Utils.log("L: Send large message (64 MB)");
                    Utils.log("Z: Run Fork performance test");
                    break;
                case "x":
                case Utils.ENTER_KEY:
                    Utils.log("IC test stopping");
                    stopICTest();
                    break;
                case "a":
                    IC.callImpulse(config.icInstanceName, 33, { opName: "attachToBadInstance" });
                    break;
                case "r":
                    IC.callImpulse(config.icInstanceName, 33, { opName: "requestCheckpoint" });
                    break;
                case "l":
                    // Note: Using 128 MB requires setting lbOptions.maxMessageQueueSizeInMB to 129 (MB) or higher
                    Utils.log("Sending large message...", null, Utils.LoggingLevel.Minimal);
                    IC.callImpulse(config.icInstanceName, 33, { opName: "sendLargeMessage", sizeInKB: 64 * 1024 })
                    break;
                case "f": // Fork
                    // IC.callFork(config.icInstanceName, 3, { name: "AmbrosiaJS" });
                    IC.callImpulse(config.icInstanceName, 33, { opName: "reportVersion" });
                    break;
                case "b": // [Explicit] Batch
                    IC.callImpulse(config.icInstanceName, 33, { opName: "explicitBatch" });
                    break;
                case "i": // [Implicit] Batch
                    IC.callImpulse(config.icInstanceName, 33, { opName: "implicitBatch" });
                    break;
                case "p": // Post
                    /*
                    if (config.icInstanceName !== "test")
                    {
                        IC.postFork("test", "ComputePI", 1, -1, null, 5);
                    }
                    */
                    // IC.postFork(config.icInstanceName, "ComputePI", 1, -1, null, IC.arg("digits?", { count: 5 }));
                    IC.postByImpulse(config.icInstanceName, "joinNames", 1, -1, null, IC.arg("namesSet", new Set<string>(["a", "b", "c"])), IC.arg("namesArray", [null, "e", null, "g"]));
                    break;
                case "o": // Post timeout
                    IC.postByImpulse(config.icInstanceName, "postTimeoutTest", 1, 10000, null, IC.arg("resultDelayInMs?", -1));
                    break;
                case "e": // Echo
                    IC.echo_PostByImpulse(Date.now(), "now");
                    break;
                case "g": // Ping
                    IC.ping_PostByImpulse(config.icInstanceName);
                    break;
                case "m": // getPublishedMethods
                    // Note: We don't need to pass a callContext object here, we're just doing it for illustration purposes
                    Meta.getPublishedMethods_PostByImpulse(config.icInstanceName, false, false, { targetInstanceName: config.icInstanceName });
                    break;
                case "t": // getPublishedTypes
                    // Note: We don't need to pass a callContext object here, we're just doing it for illustration purposes
                    Meta.getPublishedTypes_PostByImpulse(config.icInstanceName, false, { targetInstanceName: config.icInstanceName });
                    break;
                case "s": // isPublishedMethod
                    // Note: We don't need to pass a callContext object here, we're just doing it for illustration purposes
                    Meta.isPublishedMethod_PostByImpulse(config.icInstanceName, "ComputePI", 1, { targetMethodName: "ComputePI", targetMethodVersion: 1 });
                    break;
                case "z": // Fork perf test
                    // Note: Run with "node .\lib\Demo.js" [use a Release IC.exe] and set 'outputLoggingLevel' to 'Minimal' in ambrosiaConfig.json; 'outputLogDestination' can be 'ConsoleAndFile'
                    IC.callImpulse(config.icInstanceName, 33, { opName: "runForkPerfTest" });
                    break;
            }
        }
        catch (error: unknown)
        {
            Utils.log("Error: " + Utils.makeError(error).message);
        }
    }
}
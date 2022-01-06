// Generated publisher-side framework for the 'PI' Ambrosia Node app/service.
// Note: This file was generated
// Note [to publisher]: You can edit this file, but to avoid losing your changes be sure to specify a 'mergeType' other than 'None' (the default is 'Annotate') when re-running emitTypeScriptFile[FromSource]().
import * as PTM from "./PI"; // PTM = "Published Types and Methods", but this file can also include app-state and app-event handlers
import Ambrosia = require("ambrosia-node"); 
import Utils = Ambrosia.Utils;
import IC = Ambrosia.IC;
import Messages = Ambrosia.Messages;
import Meta = Ambrosia.Meta;
import Streams = Ambrosia.Streams;

// Code-gen: 'AppState' section skipped (using provided state variable 'PTM.State._myAppState' and class 'MyAppState.MyIntermediateAppState' instead)

/** Returns an OutgoingCheckpoint object used to serialize app state to a checkpoint. */
export function checkpointProducer(): Streams.OutgoingCheckpoint
{
    function onCheckpointSent(error?: Error): void
    {
        Utils.log(`checkpointProducer: ${error ? `Failed (reason: ${error.message})` : "Checkpoint saved"}`)
    }
    return (Streams.simpleCheckpointProducer(PTM.State._myAppState, onCheckpointSent));
}

/** Returns an IncomingCheckpoint object used to receive a checkpoint of app state. */
export function checkpointConsumer(): Streams.IncomingCheckpoint
{
    function onCheckpointReceived(appState?: Ambrosia.AmbrosiaAppState, error?: Error): void
    {
        if (!error)
        {
            if (!appState) // Should never happen
            {
                throw new Error(`An appState object was expected, not ${appState}`);
            }
            PTM.State._myAppState = appState as PTM.MyAppState.MyIntermediateAppState;
        }
        Utils.log(`checkpointConsumer: ${error ? `Failed (reason: ${error.message})` : "Checkpoint loaded"}`);
    }
    return (Streams.simpleCheckpointConsumer<PTM.MyAppState.MyIntermediateAppState>(PTM.MyAppState.MyIntermediateAppState, onCheckpointReceived));
}

/** This method responds to incoming Ambrosia messages (RPCs and AppEvents). */
export function messageDispatcher(message: Messages.DispatchedMessage): void
{
    // WARNING! Rules for Message Handling:
    //
    // Rule 1: Messages must be handled - to completion - in the order received. For application (RPC) messages only, if there are messages that are known to
    //         be commutative then this rule can be relaxed. 
    // Reason: Using Ambrosia requires applications to have deterministic execution. Further, system messages (like TakeCheckpoint) from the IC rely on being 
    //         handled in the order they are sent to the app. This means being extremely careful about using non-synchronous code (like awaitable operations
    //         or callbacks) inside message handlers: the safest path is to always only use synchronous code.
    //         
    // Rule 2: Before a TakeCheckpoint message can be handled, all handlers for previously received messages must have completed (ie. finished executing).
    //         If Rule #1 is followed, the app is automatically in compliance with Rule #2.
    // Reason: Unless your application has a way to capture (and rehydrate) runtime execution state (specifically the message handler stack) in the serialized
    //         application state (checkpoint), recovery of the checkpoint will not be able to complete the in-flight message handlers. But if there are no 
    //         in-flight handlers at the time the checkpoint is taken (because they all completed), then the problem of how to complete them during recovery is moot. 
    //
    // Rule 3: Avoid sending too many messages in a single message handler.
    // Reason: Because a message handler always has to run to completion (see Rule #1), if it runs for too long it can monopolize the system leading to performance issues.
    //         Further, this becomes a very costly message to have to replay during recovery. So instead, when an message handler needs to send a large sequence (series)
    //         of independent messages, it should be designed to be restartable so that the sequence can pick up where it left off (rather than starting over) when resuming
    //         execution (ie. after loading a checkpoint that occurred during the long-running - but incomplete - sequence). Restartability is achieved by sending a 
    //         application-defined 'sequence continuation' message at the end of each batch, which describes the remaining work to be done. Because the handler for the 
    //         'sequence continuation' message only ever sends the next batch plus the 'sequence continuation' message, it can run to completion quickly, which both keeps
    //         the system responsive (by allowing interleaving I/O) while also complying with Rule #1.
    //         In addition to this "continuation message" technique for sending a series, if any single message handler has to send a large number of messages it should be 
    //         sent in batches using either explicit batches (IC.queueFork + IC.flushQueue) or implicit batches (IC.callFork / IC.postFork) inside a setImmediate() callback.
    //         This asynchrony is necessary to allow I/O with the IC to interleave, and is one of the few allowable exceptions to the "always only use asynchronous code" 
    //         dictate in Rule #1. Interleaving I/O allows the instance to service self-calls, and allows checkpoints to be taken between batches.
    
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

    try
    {
        switch (message.type)
        {
            case Messages.DispatchedMessageType.RPC:
                let rpc: Messages.IncomingRPC = message as Messages.IncomingRPC;

                switch (rpc.methodID)
                {
                    case IC.POST_METHOD_ID:
                        try
                        {
                            let methodName: string = IC.getPostMethodName(rpc);
                            let methodVersion: number = IC.getPostMethodVersion(rpc); // Use this to do version-specific method behavior
                    
                            switch (methodName)
                            {
                                case "RestFn":
                                    {
                                        const p1: string = IC.getPostMethodArg(rpc, "p1");
                                        const p2: { p3: (number | string)[] }[] = IC.getPostMethodArg(rpc, "p2");
                                        IC.postResult<void>(rpc, PTM.RestFn(p1, ...p2));
                                    }
                                    break;
                                
                                case "myComplexReturnFunction":
                                    IC.postResult<{ r1: string, r2: number | string } | null>(rpc, PTM.myComplexReturnFunction());
                                    break;
                                
                                case "myComplexFunction":
                                    {
                                        const p1: { pn1: number | string, pn2: number } = IC.getPostMethodArg(rpc, "p1");
                                        const p2: string = IC.getPostMethodArg(rpc, "p2?");
                                        IC.postResult<number | string>(rpc, PTM.myComplexFunction(p1, p2));
                                    }
                                    break;
                                
                                case "hello":
                                    {
                                        const name: string = IC.getPostMethodArg(rpc, "name");
                                        IC.postResult<void>(rpc, PTM.StaticStuff.hello(name));
                                    }
                                    break;
                                
                                case "showNicknames":
                                    {
                                        const names: PTM.NickNames = IC.getPostMethodArg(rpc, "names");
                                        IC.postResult<void>(rpc, PTM.showNicknames(names));
                                    }
                                    break;
                                
                                case "bug135":
                                    IC.postResult<void>(rpc, PTM.bug135());
                                    break;
                                
                                case "joinNames":
                                    {
                                        const names: Set<string> = IC.getPostMethodArg(rpc, "names");
                                        IC.postResult<string>(rpc, PTM.joinNames(names));
                                    }
                                    break;
                                
                                case "NewTest":
                                    {
                                        const person: { age: number } = IC.getPostMethodArg(rpc, "person");
                                        IC.postResult<{ age: number }>(rpc, PTM.Test.NewTest(person));
                                    }
                                    break;
                                
                                case "ComputePI":
                                    {
                                        const digits: PTM.Test.Digits = IC.getPostMethodArg(rpc, "digits?");
                                        IC.postResult<number>(rpc, PTM.Test.TestInner.ComputePI(digits));
                                    }
                                    break;
                                
                                default:
                                    {
                                        let errorMsg: string = `Post method '${methodName}' is not implemented`;
                                        Utils.log(`(${errorMsg})`, loggingPrefix)
                                        IC.postError(rpc, new Error(errorMsg));
                                    }
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

                    case 2:
                        {
                            const rawParams: Uint8Array = rpc.getRawParams();
                            PTM.Test.takesCustomSerializedParams(rawParams);
                        }
                        break;
                    
                    case 1:
                        {
                            const dow: PTM.Test.DayOfWeek = rpc.getJsonParam("dow");
                            PTM.Test.DoIt(dow);
                        }
                        break;
                    
                    default:
                        Utils.log(`Error: Method dispatch failed (reason: No method is associated with methodID ${rpc.methodID})`);
                        break;
                }
                break;

            case Messages.DispatchedMessageType.AppEvent:
                let appEvent: Messages.AppEvent = message as Messages.AppEvent;
                
                switch (appEvent.eventType)
                {
                    case Messages.AppEventType.ICStarting:
                        // TODO: Add an exported [non-async] function 'onICStarting(): void' to ./PI.ts, then (after the next code-gen) a call to it will be generated here
                        break;

                    case Messages.AppEventType.ICStarted:
                        // TODO: Add an exported [non-async] function 'onICStarted(): void' to ./PI.ts, then (after the next code-gen) a call to it will be generated here
                        break;

                    case Messages.AppEventType.ICConnected:
                        // Note: Types and methods are published in this handler so that they're available regardless of the 'icHostingMode'
                        Meta.publishType("IntersectionType", "FullName[] & ShortName[]");
                        Meta.publishType("ShortName", "{ first: string }");
                        Meta.publishType("FullName", "{ first: string, last: string }");
                        Meta.publishType("ABNames", "{ name: \"A\" | \"B\" }[]");
                        Meta.publishType("PersonName", "string | null");
                        Meta.publishType("FirstNames", "\"Rahee\" | \"Jonathan\" | \"Darren\" | \"Richard\"");
                        Meta.publishType("Greeting", "`Hello ${FirstNames} at ${\"MSR\" | \"Microsoft\"}!`");
                        Meta.publishType("ArrayWithSpaces", "string[][][]");
                        Meta.publishType("FooBar", "{ abba: { aaa: boolean, bbb: string }[] }");
                        Meta.publishType("NameToNumberDictionary", "Map<string, number>");
                        Meta.publishType("NumberToNameDictionary", "Map<number, string>");
                        Meta.publishType("EmployeeWithGenerics", "{ firstNames: Set<{ name: string, nickNames: NickNames }>, lastName: string, birthYear: number }");
                        Meta.publishType("NickNames", "{ name: string }[]");
                        Meta.publishType("SimpleTypeC", "SimpleTypeB");
                        Meta.publishType("SimpleTypeB", "SimpleTypeA");
                        Meta.publishType("SimpleTypeA", "string[]");
                        Meta.publishType("TypeA", "{ pA: TypeB }");
                        Meta.publishType("TypeB", "{ pB: TypeC }");
                        Meta.publishType("TypeC", "{ pC: string }");
                        Meta.publishType("TestOfNewSerializationTypes", "{ s: Set<Foo>, m: Map<number, string>, d: Date, e: Error[], r: RegExp, again: Foo }");
                        Meta.publishType("Foo", "{ p1: string }");
                        Meta.publishType("DayOfWeek", "number");
                        Meta.publishType("Digits", "{ count: number }");
                        Meta.publishType("Digit2", "{ count: number }");
                        Meta.publishType("Digit3", "{ count: number }");
                        Meta.publishPostMethod("RestFn", 1, ["p1: string", "...p2: { p3: (number | string)[] }[]"], "void");
                        Meta.publishPostMethod("myComplexReturnFunction", 1, [], "{ r1: string, r2: number | string } | null");
                        Meta.publishPostMethod("myComplexFunction", 1, ["p1: { pn1: number | string, pn2: number }", "p2?: string"], "number | string");
                        Meta.publishPostMethod("hello", 1, ["name: string"], "void");
                        Meta.publishPostMethod("showNicknames", 1, ["names: NickNames"], "void");
                        Meta.publishPostMethod("bug135", 1, [], "void");
                        Meta.publishPostMethod("joinNames", 1, ["names: Set<string>"], "string");
                        Meta.publishMethod(2, "takesCustomSerializedParams", ["rawParams: Uint8Array"]);
                        Meta.publishPostMethod("NewTest", 1, ["person: { age: number }"], "{ age: number }");
                        Meta.publishPostMethod("ComputePI", 1, ["digits?: Digits"], "number");
                        Meta.publishMethod(1, "DoIt", ["dow: DayOfWeek"]);
                        // TODO: Add an exported [non-async] function 'onICConnected(): void' to ./PI.ts, then (after the next code-gen) a call to it will be generated here
                        break;

                    case Messages.AppEventType.ICStopped:
                        {
                            const exitCode: number = appEvent.args[0];
                            PTM.onICStopped(exitCode);
                        }
                        break;

                    case Messages.AppEventType.ICReadyForSelfCallRpc:
                        // TODO: Add an exported [non-async] function 'onICReadyForSelfCallRpc(): void' to ./PI.ts, then (after the next code-gen) a call to it will be generated here
                        break;
    
                    case Messages.AppEventType.RecoveryComplete:
                        PTM.onRecoveryComplete();
                        break;

                    case Messages.AppEventType.UpgradeState:
                        // TODO: Add an exported [non-async] function 'onUpgradeState(upgradeMode: Messages.AppUpgradeMode): void' to ./PI.ts, then (after the next code-gen) a call to it will be generated here
                        // Note: You will need to import Ambrosia to ./test/PI.ts in order to reference the 'Messages' namespace.
                        //       Upgrading is performed by calling _appState.upgrade(), for example:
                        //       _appState = _appState.upgrade<AppStateVNext>(AppStateVNext);
                        break;

                    case Messages.AppEventType.UpgradeCode:
                        // TODO: Add an exported [non-async] function 'onUpgradeCode(upgradeMode: Messages.AppUpgradeMode): void' to ./PI.ts, then (after the next code-gen) a call to it will be generated here
                        // Note: You will need to import Ambrosia to ./test/PI.ts in order to reference the 'Messages' namespace.
                        //       Upgrading is performed by calling IC.upgrade(), passing the new handlers from the "upgraded" PublisherFramework.g.ts,
                        //       which should be part of your app (alongside your original PublisherFramework.g.ts).
                        break;

                    case Messages.AppEventType.IncomingCheckpointStreamSize:
                        // TODO: Add an exported [non-async] function 'onIncomingCheckpointStreamSize(): void' to ./PI.ts, then (after the next code-gen) a call to it will be generated here
                        break;
                    
                    case Messages.AppEventType.FirstStart:
                        PTM.Test.TestInner.onFirstStart();
                        break;

                    case Messages.AppEventType.BecomingPrimary:
                        PTM.onBecomingPrimary();
                        break;
                    
                    case Messages.AppEventType.CheckpointLoaded:
                        // TODO: Add an exported [non-async] function 'onCheckpointLoaded(checkpointSizeInBytes: number): void' to ./PI.ts, then (after the next code-gen) a call to it will be generated here
                        break;

                    case Messages.AppEventType.CheckpointSaved:
                        // TODO: Add an exported [non-async] function 'onCheckpointSaved(): void' to ./PI.ts, then (after the next code-gen) a call to it will be generated here
                        break;

                    case Messages.AppEventType.UpgradeComplete:
                        // TODO: Add an exported [non-async] function 'onUpgradeComplete(): void' to ./PI.ts, then (after the next code-gen) a call to it will be generated here
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

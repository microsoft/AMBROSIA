// Generated publisher-side framework for the 'server' Ambrosia Node instance.
// Note: This file was generated
// Note: You can edit this file, but to avoid losing your changes be sure to specify a 'mergeType' other than 'None' (the default is 'Annotate') when re-running emitTypeScriptFile[FromSource]().
import * as PTM from "./../../AmbrosiaTest/AmbrosiaTest/JS_CodeGen_TestFiles/TS_Types"; // PTM = "Published Types and Methods"
import Ambrosia = require("ambrosia-node"); 
import Utils = Ambrosia.Utils;
import IC = Ambrosia.IC;
import Messages = Ambrosia.Messages;
import Meta = Ambrosia.Meta;
import Streams = Ambrosia.Streams;

class AppState extends Ambrosia.AmbrosiaAppState
{
    // TODO: Define your application state here

    constructor()
    {
        super();
        // TODO: Initialize your application state here
    }
}

export let _appState: AppState = new AppState();

/** Returns an OutgoingCheckpoint object used to serialize app state to a checkpoint. */
export function checkpointProducer(): Streams.OutgoingCheckpoint
{
    function onCheckpointSent(error?: Error): void
    {
        Utils.log(`checkpointProducer: ${error ? `Failed (reason: ${error.message})` : "Checkpoint saved"}`)
    }
    return (Streams.simpleCheckpointProducer(Utils.jsonStringify(_appState), onCheckpointSent));
}

/** Returns an IncomingCheckpoint object used to receive a checkpoint of app state. */
export function checkpointConsumer(): Streams.IncomingCheckpoint
{
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

/** Handler for errors from the IC process. */
export function onICError(source: string, error: Error, isFatalError: boolean = true): void
{
    Utils.logWithColor(Utils.ConsoleForegroundColors.Red, `${error.stack}`, `[IC:${source}]`);
    if (isFatalError)
    {
        IC.stop();
    }
}

/** This method responds to incoming Ambrosia messages (mainly RPCs, but also the InitialMessage and AppEvents). */
export function messageDispatcher(message: Messages.DispatchedMessage): void
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

/** Synchronous message dispatcher. */
function dispatcher(message: Messages.DispatchedMessage): boolean
{
    let handled: boolean = false;

    try
    {
        if (message.type === Messages.DispatchedMessageType.RPC)
        {
            let rpc: Messages.IncomingRPC = message as Messages.IncomingRPC;

            switch (rpc.methodID)
            {
                // TODO: Add case-statements for your high-volume methods here
            }
        }
    }
    catch (error)
    {
        let messageName: string = (message.type === Messages.DispatchedMessageType.AppEvent) ? `AppEvent:${Messages.AppEventType[(message as Messages.AppEvent).eventType]}` : Messages.DispatchedMessage[message.type];
        Utils.log(`Error: Failed to process ${messageName} message`);
        Utils.log(error);
    }

    return (handled);
}

/** Asynchronous message dispatcher. */
async function dispatcherAsync(message: Messages.DispatchedMessage)
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
                                case "BasicTypes":
                                    let isFalse: boolean = IC.getPostMethodArg(rpc, "isFalse");
                                    let height: number = IC.getPostMethodArg(rpc, "height");
                                    let mystring: string = IC.getPostMethodArg(rpc, "mystring?");
                                    let mystring2: string = IC.getPostMethodArg(rpc, "mystring2?");
                                    let my_array: number[] = IC.getPostMethodArg(rpc, "my_array?");
                                    let notSure: any = IC.getPostMethodArg(rpc, "notSure?");
                                    IC.postResult<void>(rpc, PTM.Test.BasicTypes(isFalse, height, mystring, mystring2, my_array, notSure));
                                    break;
                                
                                case "getMedia":
                                    let mediaName: string = IC.getPostMethodArg(rpc, "mediaName");
                                    IC.postResult<PTM.Test.PrintMedia>(rpc, PTM.Test.getMedia(mediaName));
                                    break;
                                
                                case "warnUser":
                                    IC.postResult<void>(rpc, PTM.Test.warnUser());
                                    break;
                                
                                case "makeName":
                                    let firstName: string = IC.getPostMethodArg(rpc, "firstName?");
                                    let lastName: string = IC.getPostMethodArg(rpc, "lastName?");
                                    IC.postResult<PTM.Test.Names>(rpc, PTM.Test.makeName(firstName, lastName));
                                    break;
                                
                                case "return_number":
                                    let strvalue: string = IC.getPostMethodArg(rpc, "strvalue");
                                    IC.postResult<number>(rpc, PTM.Test.return_number(strvalue));
                                    break;
                                
                                case "returnstring":
                                    let numvalue: number = IC.getPostMethodArg(rpc, "numvalue");
                                    IC.postResult<string>(rpc, PTM.Test.returnstring(numvalue));
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

                    // Code-gen: Fork/Impulse method handlers will go here
                    
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
                        Meta.publishType("PrintMedia", "number");
                        Meta.publishType("PrintMediaReverse", "number");
                        Meta.publishType("Name", "{ first: string, last: string, priorNames: Names[] }");
                        Meta.publishType("Names", "Name[]");
                        Meta.publishType("Nested", "{ abc: { a: Uint8Array, b: { c: Names } } }");
                        Meta.publishPostMethod("BasicTypes", 1, ["isFalse: boolean", "height: number", "mystring?: string", "mystring2?: string", "my_array?: number[]", "notSure?: any"], "void");
                        Meta.publishPostMethod("getMedia", 1, ["mediaName: string"], "PrintMedia");
                        Meta.publishPostMethod("warnUser", 1, [], "void");
                        Meta.publishPostMethod("makeName", 1, ["firstName?: string", "lastName?: string"], "Names");
                        Meta.publishPostMethod("return_number", 1, ["strvalue: string"], "number");
                        Meta.publishPostMethod("returnstring", 1, ["numvalue: number"], "string");
                        // TODO: Add an exported function 'onICStarting(): void' to ../../AmbrosiaTest/AmbrosiaTest/JS_CodeGen_TestFiles/TS_Types.ts then (after the next code-gen) a call to it will be generated here
                        break;

                    case Messages.AppEventType.ICStarted:
                        // TODO: Add an exported function 'onICStarted(): void' to ../../AmbrosiaTest/AmbrosiaTest/JS_CodeGen_TestFiles/TS_Types.ts then (after the next code-gen) a call to it will be generated here
                        break;

                    case Messages.AppEventType.ICStopped:
                        // TODO: Add an exported function 'onICStopped(exitCode: number): void' to ../../AmbrosiaTest/AmbrosiaTest/JS_CodeGen_TestFiles/TS_Types.ts then (after the next code-gen) a call to it will be generated here
                        break;

                    case Messages.AppEventType.ICReadyForSelfCallRpc:
                        // TODO: Add an exported function 'onICReadyForSelfCallRpc(): void' to ../../AmbrosiaTest/AmbrosiaTest/JS_CodeGen_TestFiles/TS_Types.ts then (after the next code-gen) a call to it will be generated here
                        break;
    
                    case Messages.AppEventType.RecoveryComplete:
                        // TODO: Add an exported function 'onRecoveryComplete(): void' to ../../AmbrosiaTest/AmbrosiaTest/JS_CodeGen_TestFiles/TS_Types.ts then (after the next code-gen) a call to it will be generated here
                        break;

                    case Messages.AppEventType.UpgradeStateAndCode:
                        // TODO: Add an exported [non-async] function 'onUpgradeStateAndCode(): void' to ../../AmbrosiaTest/AmbrosiaTest/JS_CodeGen_TestFiles/TS_Types.ts then (after the next code-gen) a call to it will be generated here
                        break;

                    case Messages.AppEventType.IncomingCheckpointStreamSize:
                        // TODO: Add an exported function 'onIncomingCheckpointStreamSize(): void' to ../../AmbrosiaTest/AmbrosiaTest/JS_CodeGen_TestFiles/TS_Types.ts then (after the next code-gen) a call to it will be generated here
                        break;
                    
                    case Messages.AppEventType.FirstStart:
                        // TODO: Add an exported function 'onFirstStart(): void' to ../../AmbrosiaTest/AmbrosiaTest/JS_CodeGen_TestFiles/TS_Types.ts then (after the next code-gen) a call to it will be generated here
                        break;

                    case Messages.AppEventType.BecomingPrimary:
                        // TODO: Add an exported function 'onBecomingPrimary(): void' to ../../AmbrosiaTest/AmbrosiaTest/JS_CodeGen_TestFiles/TS_Types.ts then (after the next code-gen) a call to it will be generated here
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

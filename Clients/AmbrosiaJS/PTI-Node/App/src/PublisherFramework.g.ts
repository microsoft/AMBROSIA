// Generated publisher-side framework for the 'PTI' Ambrosia Node app/service.
// Note: This file was generated on 2021/12/14 at 13:42:30.482.
// Note [to publisher]: You can edit this file, but to avoid losing your changes be sure to specify a 'mergeType' other than 'None' (the default is 'Annotate') when re-running emitTypeScriptFile[FromSource]().
import * as PTM from "./PTI"; // PTM = "Published Types and Methods", but this file can also include app-state and app-event handlers
import Ambrosia = require("ambrosia-node"); 
import Utils = Ambrosia.Utils;
import IC = Ambrosia.IC;
import Messages = Ambrosia.Messages;
import Meta = Ambrosia.Meta;
import Streams = Ambrosia.Streams;

// Code-gen: 'AppState' section skipped (using provided state variable 'PTM.State._appState' and class 'State.AppState' instead)

/** Returns an OutgoingCheckpoint object used to serialize app state to a checkpoint. */
export function checkpointProducer(): Streams.OutgoingCheckpoint
{
    function onCheckpointSent(error?: Error): void
    {
        Utils.log(`checkpointProducer: ${error ? `Failed (reason: ${error.message})` : "Checkpoint saved"}`)
    }
    return (Streams.simpleCheckpointProducer(PTM.State._appState, onCheckpointSent));
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
            PTM.State._appState = appState as PTM.State.AppState;
        }
        Utils.log(`checkpointConsumer: ${error ? `Failed (reason: ${error.message})` : "Checkpoint loaded"}`);
    }
    return (Streams.simpleCheckpointConsumer<PTM.State.AppState>(PTM.State.AppState, onCheckpointReceived));
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
                                case "incrementValue":
                                    {
                                        const value: number = IC.getPostMethodArg(rpc, "value");
                                        IC.postResult<number>(rpc, PTM.ServerAPI.incrementValue(value));
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

                    case 1:
                        {
                            const rawParams: Uint8Array = rpc.getRawParams();
                            PTM.ServerAPI.doWork(rawParams);
                        }
                        break;
                    
                    case 2:
                        {
                            const isFinalState: boolean = rpc.getJsonParam("isFinalState");
                            PTM.ServerAPI.reportState(isFinalState);
                        }
                        break;
                    
                    case 3:
                        {
                            const currentTime: number = rpc.getJsonParam("currentTime");
                            PTM.ServerAPI.checkHealth(currentTime);
                        }
                        break;
                    
                    case 4:
                        {
                            const numRPCBytes: number = rpc.getJsonParam("numRPCBytes");
                            const iterationWithinRound: number = rpc.getJsonParam("iterationWithinRound");
                            const startTimeOfRound: number = rpc.getJsonParam("startTimeOfRound");
                            PTM.ClientAPI.continueSendingMessages(numRPCBytes, iterationWithinRound, startTimeOfRound);
                        }
                        break;
                    
                    case 5:
                        {
                            const rawParams: Uint8Array = rpc.getRawParams();
                            PTM.ClientAPI.doWorkEcho(rawParams);
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
                        // TODO: Add an exported [non-async] function 'onICStarting(): void' to ./PTI.ts, then (after the next code-gen) a call to it will be generated here
                        break;

                    case Messages.AppEventType.ICStarted:
                        // TODO: Add an exported [non-async] function 'onICStarted(): void' to ./PTI.ts, then (after the next code-gen) a call to it will be generated here
                        break;

                    case Messages.AppEventType.ICConnected:
                        // Note: Types and methods are published in this handler so that they're available regardless of the 'icHostingMode'
                        // Code-gen: Published types will go here
                        Meta.publishMethod(1, "doWork", ["rawParams: Uint8Array"]);
                        Meta.publishMethod(2, "reportState", ["isFinalState: boolean"]);
                        Meta.publishMethod(3, "checkHealth", ["currentTime: number"]);
                        Meta.publishPostMethod("incrementValue", 1, ["value: number"], "number");
                        Meta.publishMethod(4, "continueSendingMessages", ["numRPCBytes: number", "iterationWithinRound: number", "startTimeOfRound: number"]);
                        Meta.publishMethod(5, "doWorkEcho", ["rawParams: Uint8Array"]);
                        PTM.EventHandlers.onICConnected();
                        break;

                    case Messages.AppEventType.ICStopped:
                        // TODO: Add an exported [non-async] function 'onICStopped(exitCode: number): void' to ./PTI.ts, then (after the next code-gen) a call to it will be generated here
                        break;

                    case Messages.AppEventType.ICReadyForSelfCallRpc:
                        // TODO: Add an exported [non-async] function 'onICReadyForSelfCallRpc(): void' to ./PTI.ts, then (after the next code-gen) a call to it will be generated here
                        break;
    
                    case Messages.AppEventType.RecoveryComplete:
                        PTM.EventHandlers.onRecoveryComplete();
                        break;

                    case Messages.AppEventType.UpgradeState:
                        {
                            const upgradeMode: Messages.AppUpgradeMode = appEvent.args[0];
                            PTM.EventHandlers.onUpgradeState(upgradeMode);
                        }
                        break;

                    case Messages.AppEventType.UpgradeCode:
                        {
                            const upgradeMode: Messages.AppUpgradeMode = appEvent.args[0];
                            PTM.EventHandlers.onUpgradeCode(upgradeMode);
                        }
                        break;

                    case Messages.AppEventType.IncomingCheckpointStreamSize:
                        // TODO: Add an exported [non-async] function 'onIncomingCheckpointStreamSize(): void' to ./PTI.ts, then (after the next code-gen) a call to it will be generated here
                        break;
                    
                    case Messages.AppEventType.FirstStart:
                        PTM.EventHandlers.onFirstStart();
                        break;

                    case Messages.AppEventType.BecomingPrimary:
                        PTM.EventHandlers.onBecomingPrimary();
                        break;
                    
                    case Messages.AppEventType.CheckpointLoaded:
                        {
                            const checkpointSizeInBytes: number = appEvent.args[0];
                            PTM.EventHandlers.onCheckpointLoaded(checkpointSizeInBytes);
                        }
                        break;

                    case Messages.AppEventType.CheckpointSaved:
                        PTM.EventHandlers.onCheckpointSaved();
                        break;

                    case Messages.AppEventType.UpgradeComplete:
                        PTM.EventHandlers.onUpgradeComplete();
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

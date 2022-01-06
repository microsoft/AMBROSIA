// [TOKEN:Name=Header,StartingIndent=0]
// import Ambrosia = require("./src/Ambrosia"); // TODO: This is for development ONLY: Use the import below for the "release" version [DEV-ONLY COMMENT]
// @ts-ignore (for TS:2308 "Cannot find module 'ambrosia-node' or its corresponding type declarations.") [DEV-ONLY COMMENT]
import Ambrosia = require("ambrosia-node"); 
import Utils = Ambrosia.Utils;
import IC = Ambrosia.IC;
import Messages = Ambrosia.Messages;
// [DEV-ONLY COMMENT] Note: The 'Meta' import will be used after token replacement
import Meta = Ambrosia.Meta;
import Streams = Ambrosia.Streams;

// [TOKEN:Name=AppState,StartingIndent=0]

/** Returns an OutgoingCheckpoint object used to serialize app state to a checkpoint. */
export function checkpointProducer(): Streams.OutgoingCheckpoint
{
    function onCheckpointSent(error?: Error): void
    {
        Utils.log(`checkpointProducer: ${error ? `Failed (reason: ${error.message})` : "Checkpoint saved"}`)
    }
    // @ts-ignore (for TS:2304 "Cannot find name 'State'") [DEV-ONLY COMMENT]
    return (Streams.simpleCheckpointProducer(State._appState, onCheckpointSent));
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
            // @ts-ignore (for TS:2304 "Cannot find name 'State'") and TS:2503 "Cannot find namespace 'State'") [DEV-ONLY COMMENT]
            State._appState = appState as State.AppState;
        }
        Utils.log(`checkpointConsumer: ${error ? `Failed (reason: ${error.message})` : "Checkpoint loaded"}`);
    }
    // @ts-ignore (for TS:2503 "Cannot find namespace 'State'") [DEV-ONLY COMMENT]
    return (Streams.simpleCheckpointConsumer<State.AppState>(State.AppState, onCheckpointReceived));
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
                                // [TOKEN:Name=PostMethodHandlers,StartingIndent=32]
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

                    // [TOKEN:Name=NonPostMethodHandlers,StartingIndent=20]
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
                        // [TOKEN:Name=ICStartingEventHandler,StartingIndent=24]
                        break;

                    case Messages.AppEventType.ICStarted:
                        // [TOKEN:Name=ICStartedEventHandler,StartingIndent=24]
                        break;

                    case Messages.AppEventType.ICConnected:
                        // Note: Types and methods are published in this handler so that they're available regardless of the 'icHostingMode'
                        // [TOKEN:Name=PublishTypes,StartingIndent=24]
                        // [TOKEN:Name=PublishMethods,StartingIndent=24]
                        // [TOKEN:Name=ICConnectedEventHandler,StartingIndent=24]
                        break;

                    case Messages.AppEventType.ICStopped:
                        // [TOKEN:Name=ICStoppedEventHandler,StartingIndent=24]
                        break;

                    case Messages.AppEventType.ICReadyForSelfCallRpc:
                        // [TOKEN:Name=ICReadyForSelfCallRpcEventHandler,StartingIndent=24]
                        break;
    
                    case Messages.AppEventType.RecoveryComplete:
                        // [TOKEN:Name=RecoveryCompleteEventHandler,StartingIndent=24]
                        break;

                    case Messages.AppEventType.UpgradeState:
                        // [TOKEN:Name=UpgradeStateEventHandler,StartingIndent=24]
                        break;

                    case Messages.AppEventType.UpgradeCode:
                        // [TOKEN:Name=UpgradeCodeEventHandler,StartingIndent=24]
                        break;

                    case Messages.AppEventType.IncomingCheckpointStreamSize:
                        // [TOKEN:Name=IncomingCheckpointStreamSizeEventHandler,StartingIndent=24]
                        break;
                    
                    case Messages.AppEventType.FirstStart:
                        // [TOKEN:Name=FirstStartEventHandler,StartingIndent=24]
                        break;

                    case Messages.AppEventType.BecomingPrimary:
                        // [TOKEN:Name=BecomingPrimaryEventHandler,StartingIndent=24]
                        break;
                    
                    case Messages.AppEventType.CheckpointLoaded:
                        // [TOKEN:Name=CheckpointLoadedEventHandler,StartingIndent=24]
                        break;

                    case Messages.AppEventType.CheckpointSaved:
                        // [TOKEN:Name=CheckpointSavedEventHandler,StartingIndent=24]
                        break;

                    case Messages.AppEventType.UpgradeComplete:
                        // [TOKEN:Name=UpgradeCompleteEventHandler,StartingIndent=24]
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
// [TOKEN:Name=MethodImplementations,StartingIndent=0]
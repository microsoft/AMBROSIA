// [TOKEN:Name=Header,StartingIndent=0]
// import Ambrosia = require("./src/Ambrosia"); // TODO: This is for development ONLY: Use the import below for the "release" version [DEV-ONLY COMMENT]
// @ts-ignore (for TS:2308 "Cannot find module 'ambrosia-node' or its corresponding type declarations.") [DEV-ONLY COMMENT]
import Ambrosia = require("ambrosia-node"); 
import Utils = Ambrosia.Utils;
import IC = Ambrosia.IC;
import Messages = Ambrosia.Messages;
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
    // @ts-ignore (for TS:2304 "Cannot find name '_appState'") [DEV-ONLY COMMENT]
    return (Streams.simpleCheckpointProducer(Utils.jsonStringify(_appState), onCheckpointSent));
}

/** Returns an IncomingCheckpoint object used to receive a checkpoint of app state. */
export function checkpointConsumer(): Streams.IncomingCheckpoint
{
    function onCheckpointReceived(jsonAppState: string, error?: Error): void
    {
        if (!error)
        {
            // @ts-ignore (for TS:2304 "Cannot find name '_appState'") [DEV-ONLY COMMENT]
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
                                // [TOKEN:Name=PostMethodHandlers,StartingIndent=32]
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

                    // [TOKEN:Name=NonPostMethodHandlers,StartingIndent=20]
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
                        // [TOKEN:Name=PublishTypes,StartingIndent=24]
                        // [TOKEN:Name=PublishMethods,StartingIndent=24]
                        // [TOKEN:Name=ICStartingEventHandler,StartingIndent=24]
                        break;

                    case Messages.AppEventType.ICStarted:
                        // [TOKEN:Name=ICStartedEventHandler,StartingIndent=24]
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

                    case Messages.AppEventType.UpgradeStateAndCode:
                        // [TOKEN:Name=UpgradeStateAndCodeEventHandler,StartingIndent=24]
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
// [TOKEN:Name=MethodImplementations,StartingIndent=0]
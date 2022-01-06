// Module for for Ambrosia messages.
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as Configuration from "./Configuration";
import * as DataFormat from "./DataFormat";
import * as IC from "./ICProcess";
import * as Streams from "./Streams";
import * as StringEncoding from "./StringEncoding";
import * as Root from "./AmbrosiaRoot";
import * as Utils from "./Utils/Utils-Index";
import Path = require("path");
import File = require("fs");

const RESERVED_0: Uint8Array = new Uint8Array([0]);
export const EMPTY_BYTE_ARRAY: Uint8Array = new Uint8Array(0);
let _isRecoveryRunning: boolean = false;
let _bytePool: DataFormat.BytePool; // Used to speed-up makeRpcMessage() [for messages that are under 33% of the pool size]
let _completeLiveUpgradeAtNextTakeCheckpoint: boolean = false;

/** Whether recovery (replay) is currently running. */
export function isRecoveryRunning(): boolean
{   
    return (_isRecoveryRunning);
}

/** Type of a handler for [dispatchable] messages. */
export type MessageDispatcher = (message: DispatchedMessage) => void;

/** 
 * Type of a handler for the results of all post method calls.\
 * Must return true only if the result (or error) was handled.
 * 
 * **WARNING:** To ensure replay integrity, a PostResultDispatcher should only use state that comes from one or more of these sources:
 * 1) Checkpointed application state.
 * 2) Post method arguments.
 * 3) Runtime state that is repeatably deterministic, ie. that will be identical during both real-time and replay.
 *    This includes program state that is [re]computed from checkpointed application state.
 */
export type PostResultDispatcher = (senderInstanceName: string, methodName: string, methodVersion: number, callID: number, callContextData: any, result: any, errorMsg: string) => boolean;

/** The sub-type of an RPC message. */
export enum RPCType
{
    // Note: The JS LB does not support RPCType 0 (Async) like the C# LB does, because it requires C#-specific compiler features.
    //       Instead, the JS LB supports 'post' (which is built on Fork) to enable receiving method return values.
    /** 
     * A deterministic RPC. Replayed [by the IC] during recovery. Unlike a Impulse message, a Fork message must **only** ever
     * be created by a deterministic event during real-time, so that it will always be re-created during recovery. 
     */
    Fork = 1,
    /** 
     * A non-deterministic RPC (eg. arising from user input). Replayed [by the IC] during recovery, but only if logged. 
     * Unlike a Fork message, an Impulse message must **only** ever be created by a non-deterministic event during 
     * real-time; it must never be re-created during recovery, and it is invalid to attempt to do so. An Impulse
     * is essentially a non-deterministic trigger for a deterministic chain of messages (Forks).
     */
    Impulse = 2
}

/** 
 * The types of messages that can be sent to and received from the IC.\
 * Note: It is unknown (and immaterial) to the LB as to whether it is running in an active/active configuration. 
 *       When running standalone (ie. non-active/active) it will still become the "primary".
 */
export enum MessageType
{
    /** A method call. Sent and received. */
    RPC = 0,
    /** Requests the IC to connect to a remote (non-self) destination instance. Sent only. */
    AttachTo = 1,
    /** A request to produce/send a checkpoint. Sent and Received. Has no data. */
    TakeCheckpoint = 2,
    /** A batch of RPC's. Sent and received. */
    RPCBatch = 5,
    /** A checkpoint of application state. Sent and received. */
    Checkpoint = 8,
    /** The first message when an application starts for the first time (ie. before there are any logs to recover from). Sent and received. */
    InitialMessage = 9,
    /** 
     * A request to perform an code/state upgrade (live), and become the primary. "Live" upgrade is typically only used in an 
     * active/active configuration, but it can be used for a standalone instance too. Received only. Has no data.
     */
    UpgradeTakeCheckpoint = 10,
    /** Received when the IC has become the primary [in an active/active configuration] and **should** take a checkpoint. Received only. Has no data. */
    TakeBecomingPrimaryCheckpoint = 11,
    /** A request to perform a "what-if" code/state upgrade (test). Received only. Has no data. */
    UpgradeService = 12,
    /** A batch of RPC's that also includes a count of the replayable (ie. Fork) messages in the batch. Received only. */
    CountReplayableRPCBatchByte = 13,
    /** Received when the IC has become the primary [in an active/active configuration] but **should not** take a checkpoint. Received only. Has no data. */
    BecomingPrimary = 15
}

/** The MessageType's (plus AppEvent) that can be passed to the app's MessageDispatcher (AmbrosiaConfig.dispatcher). */
export enum DispatchedMessageType
{
    /** A method call. */
    RPC = MessageType.RPC,
    /** Note: This is an LB-generated message used for notifying the app of an event (see AppEventType). It is NOT an IC-generated message. */
    AppEvent = 256 // Deliberately outside the range of a byte to avoid any possibility of conflicting with a real IC MessageType
}

/** Events (conditions and state-changes) that can be signalled to the app via it's MessageDispatcher (AmbrosiaConfig.dispatcher). */
// Note: There is no 'CheckpointSent' or 'CheckpointReceived' event, even though it would seem natural for there to be such events.
//       But because the responsibility for providing the CheckpointProducer and CheckpointConsumer methods lies with the developer
//       (via the AmbrosiaConfig parameter of IC.start()), these [logical] events are handled via the 'onFinished' callback of the
//       OutgoingCheckpoint and IncomingCheckpoint objects (returned by CheckpointProducer/CheckpointConsumer).
export enum AppEventType
{
    /** 
     * Signals that the Immortal Coordinator (IC) is starting up.\
     * Note: Only raised when icHostingMode is 'Integrated'.
     */
    ICStarting = 1,

    /** 
     * Signals that the Immortal Coordinator (IC) has started (although the LB is not yet connected to it). 
     * However, "normal" app processing should NOT begin until the 'BecomingPrimary' event is received.\
     * Note: Only raised when icHostingMode is 'Integrated'.
     */
    ICStarted = 2,

    /** Signals that the Language Binding (LB) has not yet successfully connected to the Immortal Coordinator (IC). */
    WaitingToConnectToIC = 3,

    /** Signals that the Language Binding (LB) has successfully connected to the Immortal Coordinator (IC). */
    ICConnected = 4,

    /** 
     * Signals that the Immortal Coordinator (IC) has stopped. The first (and only) parameter of this event is the exit code.\
     * If the IC does not stop within 500ms of being requested to stop [using IC.stop()] then the exit code will be 101.\
     * Note: Only raised when icHostingMode is 'Integrated'.
     */
    ICStopped = 5,
    
    /** 
     * Signals that the IC is now capable of handling (ie. immediately responding to) self-call RPCs.\
     * Note: Only raised when icHostingMode is 'Integrated'.
     */
    ICReadyForSelfCallRpc = 6,
    
    /** 
     * Signals that the replay phase of recovery has completed.\
     * This event will **not** be signalled in the "first-start" case.
     */
    RecoveryComplete = 7,
    
    /**
     * Signals that the app should immediately upgrade its state. The handler for this event must not return until
     * the app state has been upgraded. The first (and only) parameter of this event is an AppUpgradeMode enum value.
     * 
     * Upgrading is performed by calling _appState.upgrade(), for example:\
     * _appState = _appState.upgrade&lt;AppStateVNext>(AppStateVNext);
     */
    UpgradeState = 8,

    /**
     * Signals that the app should immediately upgrade its code. The handler for this event must not return until
     * the app code has been upgraded. The first (and only) parameter of this event is an AppUpgradeMode enum value.
     * 
     * Upgrading is performed by calling IC.upgrade() passing the new handlers from the "upgraded" PublisherFramework.g.ts,
     * which must be included in your app (alongside the original PublisherFramework.g.ts).
     */
    UpgradeCode = 9,
    
    /** Notification of the size (in bytes) of the checkpoint that is about to start being streamed to AmbrosiaConfig.checkpointConsumer. */
    IncomingCheckpointStreamSize = 10,

    /** Signals that the 'InitialMessage' has been received. */
    FirstStart = 11, 

    /** 
     * Signals that this immortal instance is now the Primary so the app's normal processing can begin (mainly receiving/sending RPC messages).\
     * Note: Becoming the Primary can happen when the instance is running either standalone or in active/active. 
     */
    BecomingPrimary = 12,

    /** 
     * Signals that a checkpoint of application state has been successfully loaded (received) from the Immortal Coordinator (IC). 
     * Raised after the onFinished() callback of the Streams.IncomingCheckpoint object has been called.\
     * This event includes a checkpointSizeInBytes parameter.
     */
    CheckpointLoaded = 13,
    
    /** 
     * Signals that a checkpoint of application state has been successfully saved (sent) to the Immortal Coordinator (IC). 
     * Raised after the onFinished() callback of the Streams.OutgoingCheckpoint object has been called.
     */
    CheckpointSaved = 14,

    /** 
     * Signals that a "live" upgrade has completed successfully.\
     * This event will **not** be raised when doing a "test" upgrade. 
     */
    UpgradeComplete = 15
}

/** The kind of app/service upgrade to perform. */
export enum AppUpgradeMode
{
    /** 
     * Perform a "what-if" test. This allows messages to be replayed against a test instance of an upgraded app/service to 
     * verify if the changes cause bugs. This helps catch regressions in the changes before actually upgrading the live 
     * app/service.\
     * Logs ands checkpoints are only read (never written) in this mode, so it's fully repeatable. Note also that recovery
     * will never reach completion in this mode.
     */
    Test = 0,
    /** 
     * Performs a upgrade of a "live" (running in production) app/service. Will result in a new checkpoint being taken and
     * normal processing (after the upgrade). 
     */
    Live = 1
}

// These are frequently used, so we don't want to repeatedly create them
const RPC_TYPE_FORK: Uint8Array = new Uint8Array([RPCType.Fork]);
const RPC_TYPE_IMPULSE: Uint8Array = new Uint8Array([RPCType.Impulse]);
const MESSAGE_TYPE_BYTE_RPC = new Uint8Array([MessageType.RPC]);
const MESSAGE_TYPE_BYTE_RPCBATCH = new Uint8Array([MessageType.RPCBatch]);
const MESSAGE_TYPE_BYTE_INITIALMESSAGE = new Uint8Array([MessageType.InitialMessage]);
const MESSAGE_TYPE_BYTE_CHECKPOINT = new Uint8Array([MessageType.Checkpoint]);
const MESSAGE_TYPE_BYTE_ATTACHTO = new Uint8Array([MessageType.AttachTo]);
const MESSAGE_TYPE_BYTE_TAKECHECKPOINT = new Uint8Array([MessageType.TakeCheckpoint]);

/** Class representing the meta-data for a message. */
class MessageMetaData
{
    /** The length of the message, excluding the size bytes. */
    size: number;
    /** The type of the message (eg. RPC). */
    messageType: MessageType;
    /** The length of the data portion of the message. */
    dataLength: number;
    /** The start position (byte index) of the message within the receiveBuffer. */
    startOfMessageIndex: number;
    /** The [non-inclusive] end position (byte index) of the message within the receiveBuffer. This is the same as the start index of the next message [if any] in the receiveBuffer. */
    endOfMessageIndex: number;
    /** The length of the message, including the size bytes. */
    totalLength: number;
    /** The start position (byte index) of the data portion of the message within the receiveBuffer. */
    startOfDataIndex: number;

    constructor(receiveBuffer: Buffer, startIndex: number)
    {
        let pos: number = startIndex;
        let sizeVarInt: DataFormat.varIntResult = DataFormat.readVarInt32(receiveBuffer, pos);

        this.startOfMessageIndex = startIndex;
        this.size = sizeVarInt.value;
        this.totalLength = sizeVarInt.length + this.size;
        this.endOfMessageIndex = startIndex + this.totalLength; // This is the non-inclusive end-index (ie. it's the start of the next message [if any])
        this.dataLength = this.size - 1; // -1 for MessageType
        pos += sizeVarInt.length;
        this.messageType = receiveBuffer[pos++];
        this.startOfDataIndex = pos;
    }
}

/** 
 * Initializes the message byte pool (used for optimizing message construction). The supplied 'sizeInMB' must be between 2 and 256.\
 * Returns true if the byte pool was initialized, or false if it's already been initialized.
 */
export function initializeBytePool(sizeInMB: number = 2): boolean
{
    if (!_bytePool)
    {
        sizeInMB = Math.min(Math.max(sizeInMB, 2), 256);
        _bytePool = new DataFormat.BytePool(sizeInMB * 1024 * 1024);
        return (true);
    }
    return (false);
}

/** Class representing a received message which can be passed to the app's MessageDispatcher (AmbrosiaConfig.dispatcher). */
export class DispatchedMessage
{
    receivedTime: number;
    type: DispatchedMessageType;

    constructor(type: DispatchedMessageType)
    {
        this.receivedTime = Date.now();
        this.type = type;
    }
}

/** Class representing an Ambrosia application event which can be passed to the app's MessageDispatcher (AmbrosiaConfig.dispatcher). */
export class AppEvent extends DispatchedMessage
{
    eventType: AppEventType;
    args: any[] = [];

    constructor(eventType: AppEventType, ...args: any[])
    {
        super(DispatchedMessageType.AppEvent);
        this.eventType = eventType;
        this.args = args;
    }
}

/** Class representing a received RPC message. */
export class IncomingRPC extends DispatchedMessage
{
    private _methodID: number;
    private _rpcType: RPCType;
    private _jsonParams: Utils.SimpleObject | null = null; // Will be null when rawParams is set
    private _rawParams: Uint8Array | null = null; // Will be null when jsonParams is set; this is used for all serialization formats other than JSON
    private _jsonParamNames: string[] = []; // Cached for lookup performance

    /** The unique ID of the method [being called by the RPC]. */
    get methodID(): number { return (this._methodID); }

    /** The type (Fork or Impulse) of the method [being called by the RPC]. */
    get rpcType(): RPCType { return (this._rpcType); }

    /** 
     * The names of all the JSON parameters (if any) sent with the RPC.
     * 
     * Note that method parameters (as opposed to internal parameters) begin with Poster.METHOD_PARAMETER_PREFIX.
     */
    get jsonParamNames(): string[] { return (this._jsonParamNames); }

    constructor(receiveBuffer: Buffer, dataStartIndex: number, dataEndIndex: number)
    {
        super(DispatchedMessageType.RPC);

        let pos: number = dataStartIndex;

        pos++; // Skip over reserved byte
        let methodIDVarInt: DataFormat.varIntResult = DataFormat.readVarInt32(receiveBuffer, pos);
        this._methodID = methodIDVarInt.value
        pos += methodIDVarInt.length;
        this._rpcType = receiveBuffer[pos++];

        // Parse the serialized parameters, which can either be a UTF-8 JSON string, or a raw byte array (for all other serialization formats)
        if (pos < dataEndIndex)
        {
            const isRaw: boolean = (receiveBuffer[pos] !== 123); // 123 = '{'

            if (isRaw)
            {
                // Note: We throw away the first byte, since the "protocol" for raw-format is that the first byte can be any value EXCEPT 123 (0x7B) and will be stripped
                let startIndex: number = pos + 1;
                this._rawParams = new Uint8Array(dataEndIndex - startIndex);
                receiveBuffer.copy(this._rawParams, 0, startIndex, dataEndIndex); // We want to make a copy
            }
            else
            {
                const jsonString: string = StringEncoding.fromUTF8Bytes(receiveBuffer, pos, dataEndIndex - pos).trim();
                this._jsonParams = Utils.jsonParse(jsonString);
                if (this._jsonParams)
                {
                    this._jsonParamNames = Object.keys(this._jsonParams);
                }
            }
        }
    }

    /** Returns the raw (byte) parameters of the RPC, or throws if there are no raw parameters. */
    getRawParams(): Uint8Array
    {
        if (this._rawParams)
        {
            return (this._rawParams);
        }
        else
        {
            throw new Error(`There are no rawParams for this RPC (methodID ${this._methodID})${this._jsonParams ? `, but there are jsonParams ("${this._jsonParamNames.join(", ")}")` : ""}`);
        }
    }

    /** 
     * Returns the value of the specified JSON parameter of the RPC, which may be _undefined_ if the requested 'paramName' isn't present.\
     * Throws if JSON parameters were not sent with the RPC.
     * 
     * Note that method parameters (as opposed to internal parameters) begin with Poster.METHOD_PARAMETER_PREFIX.
     */
    getJsonParam(paramName: string): any
    {
        if (this._jsonParams)
        {
            return (this._jsonParams[paramName]);
        }
        else
        {
            throw new Error(`There are no jsonParams for this RPC (methodID ${this._methodID})${this._rawParams ? `, but there are rawParams (${this._rawParams.length} bytes)` : ""}`);
        }
    }

    /** Returns true if the RPC has a JSON parameter of the specified name. */
    hasJsonParam(paramName: string): boolean
    {
        return (this._jsonParamNames.indexOf(paramName) !== -1);
    }

    makeDisplayParams(): string
    {
        let allowed: boolean = Configuration.loadedConfig().lbOptions.allowDisplayOfRpcParams;
        let params: string = "";
        
        if (allowed === true)
        {
            if (this._jsonParams) { params = Utils.jsonStringify(this._jsonParams); }
            if (this._rawParams)  { params = `(${this._rawParams.length} bytes) ${Utils.makeDisplayBytes(this._rawParams)}`; }
        }
        else
        {
            params = `[Unavailable: The 'lbOptions.allowDisplayOfRpcParams' setting is ${allowed}]`;
        }
        return (params);
    }
}

function makeMessage(type: Uint8Array, ...sections: Uint8Array[]): Uint8Array
{
    let dataLength: number = 0;
    for (let i = 0; i < sections.length; i++)
    {
        dataLength += sections[i].length;
    }
    let messageSize: Uint8Array = DataFormat.writeVarInt32(type.length + dataLength);
    let totalLength: number = messageSize.length + type.length + dataLength;
    let message: Uint8Array = Buffer.concat([messageSize, type, ...sections], totalLength);
    return (message);
}

/** [Internal] Constructs the wire-format (binary) representation of an RPC message. */
export function makeRpcMessage(rpcType: RPCType, destinationInstance: string, methodID: number, jsonOrRawArgs: Utils.SimpleObject | Uint8Array): Uint8Array
{
    if ((rpcType === RPCType.Impulse) && (_isRecoveryRunning || !IC.isPrimary()))
    {
        const methodIdentity: string = (methodID === IC.POST_METHOD_ID) && !(jsonOrRawArgs instanceof Uint8Array) ? `methodName: ${jsonOrRawArgs["methodName"]}` : `methodID: ${methodID}`;
        const whenCondition: string = _isRecoveryRunning ? "during recovery" : "before the local IC has become the Primary";
        throw new Error(`It is a violation of the recovery protocol to send an Impulse RPC ${whenCondition} (destination: '${destinationInstance}', ${methodIdentity})`);
    }

    let isSelfCall: boolean = IC.isSelf(destinationInstance);
    let destination: Uint8Array = isSelfCall ? EMPTY_BYTE_ARRAY : StringEncoding.toUTF8Bytes(destinationInstance);
    let rpcTypeByte: Uint8Array;
    let messageSize: number = 0; // Just used to track progress as we add bytes to the message
    let maxArgsSize: number = _bytePool.size / 3; // ie. enough room for 2 messages
    let canOptimize: boolean = (jsonOrRawArgs instanceof Uint8Array) ? (jsonOrRawArgs.length < maxArgsSize) : true; // For jsonArgs we had to postpone computing the length (for perf. reasons)
    let jsonArgs: Uint8Array | null = null;

    // If needed, prepare the IC to talk to the destination; when the IC receives this message it adds
    // some rows to the CRA connection table (in Azure) which causes the TCP connections to be made.
    // Note that 'destinationInstance' MUST have been previously registered; if not, the IC will 
    // report "Error attaching [localInstance] to [destinationInstance]".
    if (!isSelfCall && IC.isNewDestination(destinationInstance))
    {
        // Note: Even if the caller of makeRpcMessage() decides not to send the returned RPC message,
        //       the ATTACHTO message below will still have been sent (ie. this is a true side-effect).
        // Note: By setting 'immediateFlush' to true when we send we are attempting to limit the [performance] damage caused by "polluting"
        //       the queue with a non-RPC message [queued messages can only be sent as an RPCBatch if they are all RPC messages].
        //       If the queue is empty there will be no damage: the ATTACHTO to will simply be sent as a singleton.
        //       If the queue already has RPC's in it [for a different destination instance] then those messages will not be able to be
        //       sent as an RPCBatch, but at least all the RPC's added afterwards [for the new ATTACHTO destination] will be able to.
        let attachToMessage: Uint8Array = makeMessage(MESSAGE_TYPE_BYTE_ATTACHTO, destination);
        IC.sendMessage(attachToMessage, MessageType.AttachTo, destinationInstance, true);
    }

    if (_isRecoveryRunning)
    {
        if (rpcType === RPCType.Fork)
        {
            IC._counters.sentForkMessageCount++;
        }
        if (!isSelfCall)
        {
            IC._counters.remoteSentMessageCount++;
        }
    }

    switch (rpcType)
    {
        case RPCType.Fork:
            rpcTypeByte = RPC_TYPE_FORK;
            break;
        case RPCType.Impulse:
            rpcTypeByte = RPC_TYPE_IMPULSE;
            break;
        default:
            throw new Error(`Unsupported rpcType '${rpcType}'`);
    }

    if (canOptimize)
    {
        _bytePool.startBlock();
        messageSize += _bytePool.addBuffer(MESSAGE_TYPE_BYTE_RPC);
        messageSize += isSelfCall ? _bytePool.addBytes([0]) : _bytePool.addVarInt32(destinationInstance.length);
        messageSize += isSelfCall ? 0 : _bytePool.addBuffer(destination);
        messageSize += _bytePool.addBuffer(RESERVED_0);
        messageSize += _bytePool.addVarInt32(methodID);
        messageSize += _bytePool.addBuffer(rpcTypeByte);
        if (jsonOrRawArgs instanceof Uint8Array)
        {
            messageSize += _bytePool.addBytes([255]); // The only requirement here is that this byte NOT be 123; it will be stripped when parsing (see IncomingRPC constructor)
            messageSize += _bytePool.addBuffer(jsonOrRawArgs);
        }
        else
        {
            if (jsonOrRawArgs)
            {
                // Yes, this is a whacky way to do this, but - on V8 - it's significantly slower to call the next line any place but here
                const encodedJsonArgs: Uint8Array = StringEncoding.toUTF8Bytes(Utils.jsonStringify(jsonOrRawArgs));
                if (encodedJsonArgs.length < maxArgsSize)
                {
                    messageSize += _bytePool.addBuffer(encodedJsonArgs);
                }
                else
                {
                    canOptimize = false;
                    jsonArgs = encodedJsonArgs;
                    _bytePool.cancelBlock();
                }
            }
        }
        if (canOptimize) // See earlier whackiness
        {
            let messageBody: Uint8Array = _bytePool.endBlock(false); // We only need a [fast] temp-copy because we're going to immediately use it in a [slow] full-copy

            _bytePool.startBlock();
            _bytePool.addVarInt32(messageBody.length); // Should match messageSize
            _bytePool.addBuffer(messageBody);
            let message: Uint8Array = _bytePool.endBlock();
            return (message);
        }
    }

    if (!canOptimize)
    {
        // We can't optimize, so fall-back to not using the _bytePool
        let rawArgs: Uint8Array | null = (jsonOrRawArgs instanceof Uint8Array) ? jsonOrRawArgs : null;
        let destinationLength: Uint8Array = isSelfCall ? RESERVED_0 : DataFormat.writeVarInt32(destinationInstance.length);
        let rpcBody: Uint8Array = makeRpcBody(methodID, rpcTypeByte, jsonArgs, rawArgs);
        let message: Uint8Array = makeMessage(MESSAGE_TYPE_BYTE_RPC, destinationLength, destination, rpcBody);
        return (message);
    }

    throw new Error("makeRpcMessage() did not return an RPC; this is an internal coding error");
}

function makeRpcBody(methodID: number, rpcType: Uint8Array, jsonArgs: Uint8Array | null, rawArgs: Uint8Array | null): Uint8Array
{
    let serializedArgs: Uint8Array = EMPTY_BYTE_ARRAY;
    
    if (rawArgs)
    {
        let newRaw: Uint8Array = new Uint8Array(rawArgs.length + 1);
        newRaw[0] = 255; // The only requirement here is that this byte NOT be 123; it will be stripped when parsing (see IncomingRPC constructor)
        newRaw.set(rawArgs, 1);
        serializedArgs = newRaw;
    }
    if (jsonArgs)
    {
        serializedArgs = jsonArgs;
    }

    let varIntMethodID: Uint8Array = DataFormat.writeVarInt32(methodID);
    let totalLength: number = RESERVED_0.length + varIntMethodID.length + rpcType.length + serializedArgs.length;
    let rpcBody: Uint8Array = Buffer.concat([RESERVED_0, varIntMethodID, rpcType, serializedArgs], totalLength);
    return (rpcBody);
}

/** [Internal] Creates an RPCBatch message from the supplied RPC messages. */
/*
export function makeRpcBatch(...rpcMessages: Uint8Array[]): Uint8Array
{
    // First, check that the batch ONLY contains RPC messages
    canUseRPCBatch(rpcMessages, true);

    let messageCount: Uint8Array = DataFormat.writeVarInt32(rpcMessages.length);
    let message: Uint8Array = makeMessage(MESSAGE_TYPE_BYTE_RPCBATCH, messageCount, ...rpcMessages);
    return (message);
}
*/

/** 
 * [Internal] Returns the "header" portion of an RPCBatch message, assuming that the batch has 'rpcCount' RPC messages that collectively contain 'totalRpcLength' bytes.
 * The batch can contain both Fork and Impulse RPC's.\
 * **WARNING:** After sending this header you MUST immediately send the matching block of RPC messages.
 */
export function makeRPCBatchMessageHeader(rpcCount: number, totalRpcLength: number): Uint8Array
{
    let type: Uint8Array = MESSAGE_TYPE_BYTE_RPCBATCH;
    let messageCount: Uint8Array = DataFormat.writeVarInt32(rpcCount);
    let messageSize: Uint8Array = DataFormat.writeVarInt32(type.length + messageCount.length + totalRpcLength);
    let totalLength: number = messageSize.length + type.length + messageCount.length;
    let rpcBatchMessageHeader: Uint8Array = Buffer.concat([messageSize, type, messageCount], totalLength);
    return (rpcBatchMessageHeader);
}

/** [Internal] Returns true if all the supplied messages are RPC messages (both Fork and Impulse RPC's are allowed). */
export function canUseRPCBatch(rpcMessages: Uint8Array[], throwOnNonRPC: boolean = false): boolean
{
    let canBatch: boolean = true;
    for (let i = 0; i < rpcMessages.length; i++)
    {
        let sizeVarInt: DataFormat.varIntResult = DataFormat.readVarInt32(rpcMessages[i], 0);
        let messageType: MessageType = rpcMessages[i][sizeVarInt.length];
        if (messageType !== MessageType.RPC)
        {
            if (throwOnNonRPC)
            {
                throw new Error(`An RPCBatch can only contain RPC messages; the message at index ${i} of the batch is a '${MessageType[messageType]}'`);
            }
            canBatch = false;
            break;
        }
    }
    return (canBatch);
}

/*
function makeIncomingRpcMessage(methodID: number, rpcType: Uint8Array, jsonOrRawArgs: object | Uint8Array): Uint8Array
{
    let message: Uint8Array = makeMessage(MESSAGE_TYPE_BYTE_RPC, makeRpcBody(methodID, rpcType, jsonOrRawArgs));
    return (message);
}
*/

/** [Internal] Creates an IncomingRPC from an outgoing RPC message buffer. */
export function makeIncomingRpcFromOutgoingRpc(outgoingRpc: Uint8Array): IncomingRPC
{
    let buffer: Buffer = Buffer.from(outgoingRpc);
    let metaData: MessageMetaData = new MessageMetaData(buffer, 0);
    let destinationLengthVarInt: DataFormat.varIntResult = DataFormat.readVarInt32(buffer, metaData.startOfDataIndex);
    let startPos: number = metaData.startOfDataIndex + destinationLengthVarInt.length + destinationLengthVarInt.value;
    let incomingRpc: IncomingRPC = new IncomingRPC(buffer, startPos /* Strip off the destination */, metaData.endOfMessageIndex);
    return (incomingRpc);
}

/** [Internal] Creates a 'TakeCheckpoint' message. */
export function makeTakeCheckpointMessage(): Uint8Array
{
    let message: Uint8Array = makeMessage(MESSAGE_TYPE_BYTE_TAKECHECKPOINT);
    return (message);
}

function makeInitialMessage(dataPayload: Uint8Array): Uint8Array
{
    let message: Uint8Array = makeMessage(MESSAGE_TYPE_BYTE_INITIALMESSAGE, dataPayload);
    return (message);
}

function makeCheckpointMessage(size: number)
{
    let sizeBytes: Uint8Array = DataFormat.writeVarInt64(size);
    let message: Uint8Array = makeMessage(MESSAGE_TYPE_BYTE_CHECKPOINT, sizeBytes);
    return (message);
}

let _lastCompleteLogPageSequenceID: bigint = BigInt(-99); // The last [non-negative] log page sequence ID for a completely read log page
let _lastLogPageSequenceID: bigint = BigInt(-99); // The last log page sequence ID that was read (even if for an incomplete log page); used only for debugging

/** [ReadOnly][Internal] Returns the last [non-negative] log page sequence ID for a completely read log page. Returns -99 if no value is available. */
export function lastCompleteLogPageSequenceID(): bigint { return (_lastCompleteLogPageSequenceID); }

/** [ReadOnly][Internal] Returns the last log page sequence ID that was read (even if for an incomplete log page). Returns -99 if no value is available. */
export function lastLogPageSequenceID(): bigint { return (_lastLogPageSequenceID); }

/** 
 * [Internal] Returns the length of the first log page in receiveBuffer, but only if the buffer contains [at least] one complete log page. 
 * Otherwise, returns -1 (indicating that the caller should keep accumulating bytes in receiveBuffer). 
 */
export function getCompleteLogPageLength(receiveBuffer: Buffer, bufferLength: number): number
{
    if (bufferLength >= 24) // committerID (4 bytes) + pageSize (4 bytes) + checksum (8 bytes) + pageSequenceID (8 bytes)
    {
        const logPageLength: number = DataFormat.readInt32Fixed(receiveBuffer, 4); // receiveBuffer.readInt32LE(4);

        // This is to catch problems with us incorrectly parsing the header (not to check if the IC has a bug), although it adds some performance overhead to the "hot path"
        // Note: We ignore negative pageSequenceID's, which have special meaning:
        //       -1 indicates a log page that only contains a 'TakeBecomingPrimaryCheckpoint', 'UpgradeTakeCheckpoint', 'TakeCheckpoint', or 'UpgradeService' message
        //       -2 indicates a log page that only contains a 'Checkpoint' message
        //       Using negative pageSequenceID's allows the IC to easily skip replaying these log pages during TTD.
        const logPageSequenceID: bigint = DataFormat.readInt64Fixed(receiveBuffer, 16); // receiveBuffer.readBigInt64LE(16);
        const expectedPageSequenceID: bigint = _lastCompleteLogPageSequenceID + BigInt(1);
        if ((logPageSequenceID >= 0) && (_lastCompleteLogPageSequenceID >= 0) && (logPageSequenceID !== expectedPageSequenceID))
        {
            throw new Error(`The log page currently being read has a sequence ID (${logPageSequenceID}) that's not the expected value (${expectedPageSequenceID})`);
        }

        _lastLogPageSequenceID = logPageSequenceID;

        if (logPageLength <= bufferLength)
        {
            // The receiveBuffer contains [at least] one complete log page
            if (logPageSequenceID >= 0)
            {
                _lastCompleteLogPageSequenceID = logPageSequenceID;
            }
            return (logPageLength);
        }
    }
    return (-1);
}

/** [Internal] Returns the length of the first log page in receiveBuffer, or -1 if not enough of the first log page's header has been read. */
export function readLogPageLength(receiveBuffer: Buffer, bufferLength: number): number
{
    if (bufferLength >= 8) // committerID (4 bytes) + pageSize (4 bytes)
    {
        const logPageLength: number = DataFormat.readInt32Fixed(receiveBuffer, 4); // receiveBuffer.readInt32LE(4);
        return (logPageLength);
    }
    return (-1);
}

const _interruptedLogPages: { pageBuffer: Buffer, startingMessageNumber: number }[] = []; // Used to defer processing of log pages that generate "too much" pressure on the outgoing message queue
const _emptyPageBuffer: Buffer = Buffer.alloc(0); // A "placeholder" log page buffer

/** Returns the number of log pages that are currently backlogged due to interruption. */
export function interruptedLogPageBacklogCount(): number
{
    return (_interruptedLogPages.length);
}

/**
 * [Internal] Finishes processing any previous log pages that were temporarily interrupted because they were generating too much outgoing data.\
 * Returns the number of interrupted log pages that still need to be processed.
 * 
 * Note: The purpose of interrupting a page is purely to let I/O with the IC interleave, ie. to let the outgoing message stream empty (at least partially).
 */
export function processInterruptedLogPages(config: Configuration.AmbrosiaConfig): number
{
    while (_interruptedLogPages.length > 0)
    {
        // Note: If not enough data was flushed from the outgoing message stream, then the page may get interrupted again (potentially leading to an infinite loop)
        const pageResult: number = processLogPage(_interruptedLogPages[0].pageBuffer, config, _interruptedLogPages[0].startingMessageNumber);
        if (pageResult === -2)
        {
            // The page was interrupted again, so check if startingMessageNumber advanced
            if (_interruptedLogPages[_interruptedLogPages.length - 1].pageBuffer === _emptyPageBuffer)
            {
                _interruptedLogPages[0].startingMessageNumber = _interruptedLogPages[_interruptedLogPages.length - 1].startingMessageNumber;
                _interruptedLogPages.pop(); // Throw away the enqueued empty page (this entry was just the mechanism used to communicate that the startingMessageNumber has advanced)
            }
            break; // There's no point trying to finish any more pages [for now]
        }
        if (pageResult === -1)
        {
            // The page was fully processed, so we can remove it
            _interruptedLogPages.shift();
            if (_interruptedLogPages.length === 0)
            {
                Utils.traceLog(Utils.TraceFlag.LogPageInterruption, `Interrupted log page backlog cleared`);
            }
        }
    }
    return (_interruptedLogPages.length);
}

/** 
 * [Internal] Processes all the messages in a log page.
 * Returns the number of checkpoint bytes to read (if any) before the next log page will arrive, or:\
 * -1 meaning "The page was fully processed", or\
 * -2 meaning "The page was interrupted (to allow I/O to occur)".
 */
export function processLogPage(receiveBuffer: Buffer, config: Configuration.AmbrosiaConfig, startingMessageNumber: number = 0): number
{
    // Parse the header
    // Note: committerID can be negative
    // let committerID: number = DataFormat.readInt32Fixed(receiveBuffer, 0); // receiveBuffer.readInt32LE(0);
    let logPageLength: number = DataFormat.readInt32Fixed(receiveBuffer, 4); // receiveBuffer.readInt32LE(4);
    // let checkSum: bigint = DataFormat.readInt64Fixed(receiveBuffer, 8); // receiveBuffer.readBigInt64LE(8);
    let pageSequenceID: bigint = DataFormat.readInt64Fixed(receiveBuffer, 16); // receiveBuffer.readBigInt64LE(16);
    let pos: number = 24;
    let outgoingCheckpoint: Streams.OutgoingCheckpoint;
    let checkpointMessage: Uint8Array;
    let messageNumberInPage: number = 0;

    // Parse/process the message(s)
    while (pos < logPageLength)
    {
        messageNumberInPage++;

        // Check if this and/or previous log pages have resulted in too much outgoing data (in which case we need to interrupt it to allow I/O with the IC to occur).
        // This can happen if the [user provided] incoming RPC message handlers are generating large and/or a high number of outgoing messages (eg. in a tight loop).
        // The user can still easily write code that overwhelms the output stream's buffer (which will result in OutgoingMessageStream.checkInternalBuffer() failing),
        // but this at least helps mitigate the problem - especially during recovery, when messages previously sent "spaced out" are arriving extremely rapidly in a 
        // consecutive batch of log pages. See bug #194 for more details.
        if (IC.isOutgoingMessageStreamGettingFull(0.75)) // Allow "space" for the current message to add outgoing messages [although whatever space we leave can still end up being insufficient]
        {
            if (startingMessageNumber === 0)
            {
                // Initial interruption
                const pageBuffer: Buffer = Buffer.alloc(logPageLength);
                receiveBuffer.copy(pageBuffer, 0, 0, logPageLength);
                _interruptedLogPages.push({ pageBuffer: pageBuffer, startingMessageNumber: messageNumberInPage });
                Utils.traceLog(Utils.TraceFlag.LogPageInterruption, `Outgoing message stream is getting full (${IC.outgoingMessageStreamBacklog()} bytes); interrupting log page ${pageSequenceID} (${logPageLength} bytes) at message ${messageNumberInPage}`);
            }
            else
            {
                // Re-interruption [ie. we are being called via processInterruptedLogPages()].
                // This is a no-op unless we've made additional progress through the page.
                if (messageNumberInPage > startingMessageNumber)
                {
                    // Rather than needlessly re-allocate/copy and re-enqueue the same page, we just need to communicate [to processInterruptedLogPages()] that the startingMessageNumber has advanced
                    _interruptedLogPages.push({ pageBuffer: _emptyPageBuffer, startingMessageNumber: messageNumberInPage });
                    Utils.traceLog(Utils.TraceFlag.LogPageInterruption, `Outgoing message stream is getting full (${IC.outgoingMessageStreamBacklog()} bytes); re-interrupting log page ${pageSequenceID} at message ${messageNumberInPage}`);
                }
            }

            // Give the outgoing message stream a chance to be flushed to the IC (ie. allow I/O with the IC to interleave)
            setImmediate(() => processInterruptedLogPages(config));
            return (-2); // The page was interrupted
        }

        // Parse the message "header"
        const msg: MessageMetaData = new MessageMetaData(receiveBuffer, pos);
        pos = msg.startOfDataIndex;

        // If a startingMessageNumber was specified, then skip messages until we reach it
        if (startingMessageNumber > 0)
        {
            if (messageNumberInPage < startingMessageNumber)
            {
                pos = msg.endOfMessageIndex;
                continue;
            }
            if (messageNumberInPage === startingMessageNumber)
            {
                Utils.traceLog(Utils.TraceFlag.LogPageInterruption, `Resuming processing of interrupted log page ${pageSequenceID} at message ${startingMessageNumber}`);
            }
        }

        if (Utils.canLog(Utils.LoggingLevel.Verbose)) // We add this check because this is a high-volume code path, and Utils.log() is expensive
        {
            let showBytes: boolean = config.lbOptions.debugOutputLogging; // For debugging
            Utils.log(`Received '${MessageType[msg.messageType]}' (${msg.totalLength} bytes)` + (showBytes ? `: ${Utils.makeDisplayBytes(receiveBuffer, msg.startOfMessageIndex, msg.totalLength)}` : ""));
        }

        // A [user-supplied] handler for a received message (typically an RPC) may have called IC.stop(), in which case we can't continue to process messages in the log page
        if (Configuration.loadedConfig().isIntegratedIC && !IC.isRunning())
        {
            break;
        }

        // Parse the message "data" section
        switch (msg.messageType)
        {
            case MessageType.BecomingPrimary: // No data
                IC.isPrimary(true);
                IC.emitAppEvent(AppEventType.BecomingPrimary);
                IC.checkSelfConnection();

                // An active/active secondary (including the checkpointing secondary) remains in constant recovery until
                // it becomes the primary (although only a non-checkpointing secondary can ever become the primary)
                if (_isRecoveryRunning)
                {
                    _isRecoveryRunning = false;
                    IC.onRecoveryComplete();
                    IC.emitAppEvent(AppEventType.RecoveryComplete);
                }
                else
                {
                    throw new Error(`This [non-checkpointing] active/active secondary (instance '${IC.instanceName()}' replica #${config.replicaNumber}) was not in recovery as expected`);
                }
                break;

            case MessageType.UpgradeTakeCheckpoint: // No data
            case MessageType.UpgradeService: // No data
                const mode: AppUpgradeMode = (msg.messageType === MessageType.UpgradeTakeCheckpoint) ? AppUpgradeMode.Live : AppUpgradeMode.Test;

                if (!_isRecoveryRunning)
                {
                    // A Checkpoint must have preceded this message
                    throw new Error(`"Unexpected message '${MessageType[msg.messageType]}'; a '${MessageType[MessageType.Checkpoint]}' message should have preceded this message`);
                }
                else
                {
                    if (mode === AppUpgradeMode.Live)
                    {
                        _isRecoveryRunning = false;
                        IC.onRecoveryComplete();
                        IC.emitAppEvent(AppEventType.RecoveryComplete);
                    }
                }

                // Tell the app to upgrade. Note: The handlers for these events MUST execute synchronously (ie. they must not return until the upgrade is complete).
                Utils.log(`Upgrading app (state and code) [in '${AppUpgradeMode[mode]}' mode]...`, null, Utils.LoggingLevel.Minimal);
                IC.clearUpgradeFlags();
                IC.emitAppEvent(AppEventType.UpgradeState, mode);
                IC.emitAppEvent(AppEventType.UpgradeCode, mode);
                if (!IC.checkUpgradeFlags())
                {
                    throw new Error(`Upgrade incomplete: Either IC.upgrade() and/or the upgrade() method of your AmbrosiaAppState instance was not called`);
                }
                else
                {
                    Utils.log("Upgrade of state and code complete", null, Utils.LoggingLevel.Minimal);
                }

                if (mode === AppUpgradeMode.Test)
                {
                    // When doing a 'test mode' upgrade there will be no 'completion' message from the IC signalling when 
                    // message playback is complete, so we will not end up emitting a 'RecoveryComplete' event to the app
                    // (and neither will the 'UpgradeComplete' event be emitted)
                    Utils.log("Running recovery after app upgrade...", null, Utils.LoggingLevel.Minimal); 
                }

                if (mode === AppUpgradeMode.Live)
                {
                    // Take a checkpoint (of the [now upgraded] app state)
                    outgoingCheckpoint = config.checkpointProducer();
                    checkpointMessage = makeCheckpointMessage(outgoingCheckpoint.length);
                    IC.sendMessage(checkpointMessage, MessageType.Checkpoint, IC.instanceName());
                    IC.sendCheckpoint(outgoingCheckpoint, () =>
                    // Note: This lambda runs ONLY if sendCheckpoint() succeeds
                    {
                        IC.isPrimary(true);
                        IC.emitAppEvent(AppEventType.BecomingPrimary);
                        IC.checkSelfConnection();

                        // The "live" upgrade is not actually complete at this point: it is complete after the next 'TakeCheckpoint' message is
                        // received (which will usually be the next message received, but there can also be other messages that come before it).
                        // Note: Another 'tell' that the upgrade has completed is that the <instanceTable>.CurrentVersion will change to the upgradeVersion.
                        _completeLiveUpgradeAtNextTakeCheckpoint = true;
                    });
                }
                break;

            case MessageType.TakeBecomingPrimaryCheckpoint: // No data
                outgoingCheckpoint = config.checkpointProducer();
                checkpointMessage = makeCheckpointMessage(outgoingCheckpoint.length);
                
                if (!_isRecoveryRunning)
                {
                    const initialMessage: Uint8Array = makeInitialMessage(StringEncoding.toUTF8Bytes(Root.languageBindingVersion())); // Note: We could send any arbitrary bytes
                    IC.sendMessage(initialMessage, MessageType.InitialMessage, IC.instanceName());
                }
                else
                {
                    _isRecoveryRunning = false;
                    IC.onRecoveryComplete();
                    IC.emitAppEvent(AppEventType.RecoveryComplete);
                }

                IC.sendMessage(checkpointMessage, MessageType.Checkpoint, IC.instanceName());
                IC.sendCheckpoint(outgoingCheckpoint, () =>
                // Note: This lambda runs ONLY if sendCheckpoint() succeeds
                {
                    IC.isPrimary(true);
                    IC.emitAppEvent(AppEventType.BecomingPrimary);
                    IC.checkSelfConnection();
                });
                break;

            case MessageType.InitialMessage:
                // The data of an InitialMessage is whatever we set it to be in our 'TakeBecomingPrimaryCheckpoint' response. We simply ignore it.
                IC.emitAppEvent(AppEventType.FirstStart);
                updateReplayStats(msg.messageType);
                break;

            case MessageType.RPC:
                let rpc: IncomingRPC = new IncomingRPC(receiveBuffer, msg.startOfDataIndex, msg.endOfMessageIndex);
                config.dispatcher(rpc);
                updateReplayStats(msg.messageType, rpc);
                break;
            
            case MessageType.TakeCheckpoint: // No data
                // TODO: JonGold needs to address issue #158 ("Clarify Ambrosia protocol") since the C# LB has (obsolete?) code that:
                //       a) Handles 'TakeCheckpoint' at startup (a case which is not in the protocol spec), and
                //       b) Sends an 'InitialMessage' in response to a TakeCheckpoint (again, this is not in the protocol spec)
                //       The protocol spec can be found here: https://github.com/microsoft/AMBROSIA/blob/master/CONTRIBUTING/AMBROSIA_client_network_protocol.md#communication-protocols
                outgoingCheckpoint = config.checkpointProducer();
                checkpointMessage = makeCheckpointMessage(outgoingCheckpoint.length);
                IC.sendMessage(checkpointMessage, MessageType.Checkpoint, IC.instanceName());
                IC.sendCheckpoint(outgoingCheckpoint, () =>
                // Note: This lambda runs ONLY if sendCheckpoint() succeeds
                {
                    if (_completeLiveUpgradeAtNextTakeCheckpoint)
                    {
                        try
                        {
                            _completeLiveUpgradeAtNextTakeCheckpoint = false;
                            // The "live" upgrade is now complete, so update the ambrosiaConfig.json file so that we're prepared for the next restart.
                            // However, we only do this if the upgrade was requested via the ambrosiaConfig.json. If the upgrade was requested via an
                            // explicit call to "Ambrosia.exe RegisterInstance" then we assume the user is handling the upgrade process manually (or
                            // via an 'upgrade orchestration service' they built).
                            if (Configuration.loadedConfig().isLiveUpgradeRequested)
                            {
                                Configuration.loadedConfig().updateSetting("appVersion", Configuration.loadedConfig().upgradeVersion);
                                Configuration.loadedConfig().updateSetting("activeCode", Configuration.ActiveCodeType[Configuration.ActiveCodeType.VNext]);
                                // The IC doesn't update the registered currentVersion after the upgrade (it only updates <instanceTable>.CurrentVersion),
                                // so we have to re-register at the next restart to update it [without this, AmbrosiaConfigFile.initializeAsync() will throw]
                                Configuration.loadedConfig().updateSetting("autoRegister", true); 
                            }
                            else
                            {
                                // Note: Because the upgrade wasn't requested via ambrosiaConfig.json, we can't report the upgradeVersion that currentVersion must be set to
                                Utils.log(`Warning: You must re-register the '${IC.instanceName()}' instance (to update --currentVersion) to finish the upgrade`);
                            }
                            Utils.log("Upgrade complete", null, Utils.LoggingLevel.Minimal);
                            // The app may want to do its own post-upgrade work (eg. custom handling of re-registration / code-switching, or signalling 
                            // to any orchestrating infrastructure that may be managing an active/active upgrade).
                            // Note: Because this event is raised only as a consequence of a non-replayable message (UpgradeTakeCheckpoint), whatever 
                            //       actions (if any) the app takes when responding to it are not part of a deterministic chain.
                            IC.emitAppEvent(AppEventType.UpgradeComplete);
                        }
                        catch (error: unknown)
                        {
                            Utils.log(Utils.makeError(error));
                        }
                    }
                });
                break;

            case MessageType.Checkpoint:
                let checkpointLengthVarInt: DataFormat.varIntResult = DataFormat.readVarInt64(receiveBuffer, pos);
                let checkpointLength: number = checkpointLengthVarInt.value;
                Utils.log(`Starting recovery [load checkpoint (${checkpointLength} bytes) and replay messages]...`);
                _isRecoveryRunning = true;

                // We keep track of the number of received/resent messages during recovery
                IC._counters.remoteSentMessageCount = IC._counters.receivedMessageCount = IC._counters.receivedForkMessageCount = IC._counters.sentForkMessageCount = 0; 

                // By returning the checkpoint length we tell the caller to read the next checkpointLength bytes as checkpoint data.
                // Note: It's safe to return here as a 'Checkpoint' will always be the only message in the log page.
                //       The caller will skip to the first byte AFTER the current log page [whose length will include
                //       checkpointLengthVarInt.numBytesRead] so there's no need to also pass back checkpointLengthVarInt.numBytesRead.
                return (checkpointLength); // Note: Can be 0
            
            case MessageType.RPCBatch:
            case MessageType.CountReplayableRPCBatchByte:
                let rpcCountVarInt: DataFormat.varIntResult = DataFormat.readVarInt32(receiveBuffer, pos);
                let rpcCount: number = rpcCountVarInt.value;
                pos += rpcCountVarInt.length;
                if (msg.messageType === MessageType.CountReplayableRPCBatchByte)
                {
                    let forkRpcCountVarInt: DataFormat.varIntResult = DataFormat.readVarInt32(receiveBuffer, pos);
                    let forkRpcCount: number = forkRpcCountVarInt.value;
                    pos += forkRpcCountVarInt.length;
                    Utils.log(`Processing RPC batch (of ${rpcCount} messages, ${forkRpcCount} of which are replayable)...`);
                }
                else
                {
                    Utils.log(`Processing RPC batch (of ${rpcCount} messages)...`);
                }
                // Note: We'll let the processing of each contained message update _replayedMessageCount and _receivedForkMessageCount, so we don't
                //       call updateReplayStats() here (effectively, we ignore the batch "wrapper" for the purpose of tracking replay stats)
                continue;

            default:
                throw new Error(`Log page ${pageSequenceID} contains a message of unknown type (${msg.messageType}); message: ${Utils.makeDisplayBytes(receiveBuffer, msg.startOfMessageIndex, msg.totalLength)}`);
        }

        pos = msg.endOfMessageIndex;
    }

    return (-1); // The page was fully processed
}

function updateReplayStats(messageType: MessageType, message?: DispatchedMessage): void
{
    if (_isRecoveryRunning)
    {
        if ((messageType === MessageType.InitialMessage) || (messageType === MessageType.RPC)) 
        {
            if ((messageType === MessageType.RPC) && ((message as IncomingRPC).rpcType === RPCType.Fork))
            {
                IC._counters.receivedForkMessageCount++;
            }
            IC._counters.receivedMessageCount++;
        }
    }
}
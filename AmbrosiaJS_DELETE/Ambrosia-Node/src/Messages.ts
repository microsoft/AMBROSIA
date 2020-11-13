// Module for for Ambrosia messages.
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as Configuration from "./Configuration";
import * as DataFormat from "./DataFormat";
import * as IC from "./ICProcess";
import * as Streams from "./Streams";
import * as StringEncoding from "./StringEncoding";
import * as Root from "./AmbrosiaRoot";
import * as Utils from "./Utils/Utils-Index";

const RESERVED_0: Uint8Array = new Uint8Array([0]);
export const EMPTY_BYTE_ARRAY: Uint8Array = new Uint8Array(0);
let _isRecoveryRunning: boolean = false;
let _bytePool: DataFormat.BytePool = new DataFormat.BytePool(); // Used to speed-up makeRpcMessage() [for messages that are under 33% of the pool size]

/** Whether recovery (replay) is currently running. */
export function isRecoveryRunning(): boolean
{   
    return (_isRecoveryRunning);
}

/** Type of a handler for [dispatchable] messages. */
export type MessageDispatcher = (message: DispatchedMessage) => void;

/** The sub-type of an RPC message. */
export enum RPCType
{
    // Note: The JS LB does not support RPCType 0 (Async) like the C# LB does, because it requires C#-specific compiler features.
    //       Instead, the JS LB supports 'post' (which is built on Fork) to enable receiving method return values.
    /** A deterministic RPC. Replayed [by the IC] during recovery. */
    Fork = 1,
    /** 
     * A non-deterministic RPC (eg. arising from user input). Replayed [by the IC] during recovery, but only if logged. 
     * Unlike a Fork message, an Impulse message is only ever created by a non-determintic source during 
     * real-time - it is never re-created during recovery (and it is invalid to attempt to do so). An Impulse
     * is essentially a non-deterministic trigger for a deterministic chain of messages (Forks).
     */
    Impulse = 2
}

/** The types of messages that can be sent to and received from the IC. */
export enum MessageType
{
    /** A method call. Sent and received. */
    RPC = 0,
    /** Requests the IC to connect to a remote (non-self) destination instance. Sent only. */
    AttachTo = 1,
    /** A request to produce/send a checkpoint. Received only. Has no data. */
    TakeCheckpoint = 2,
    /** A batch of RPC's. Sent and received. */
    RPCBatch = 5,
    /** A checkpoint of application state. Sent and received */
    Checkpoint = 8,
    /** The first message when an application start for the first time. Sent and received. */
    InitialMessage = 9,
    /** A request to perform an app/state upgrade (live). Received only. Has no data. */
    UpgradeTakeCheckpoint = 10,
    /** A special type of 'TakeCheckpoint', received when either starting for the first time or when recovering (but not upgrading). Received only. Has no data. */
    TakeBecomingPrimaryCheckpoint = 11,
    /** A request to perform a "what-if" app/state upgrade (test). Received only. Has no data. */
    UpgradeService = 12,
    /** A batch of RPC's that also includes a count of the replayable (ie. Fork) messages in the batch. Received only. */
    CountReplayableRPCBatchByte = 13
}

/** The MessageType's (plus AppEvent) that can be passed to the app's MessageDispatcher (AmbrosiaConfig.dispatcher). */
export enum DispatchedMessageType
{
    RPC = MessageType.RPC,
    /** Note: This is an LB-generated message used for notifying the app of an event (see AppEventType). It is NOT an IC-generated message. */
    AppEvent = 256 // Deliberately outside the range of a byte to avoid any possibility of conflicting with a real IC MessageType
}

/** Events (conditions and state-changes) that can be signalled to the app via it's MessageDispatcher (AmbrosiaConfig.dispatcher). */
export enum AppEventType
{
    /** Signals that the Immortal Coordinator (IC) is starting up. */
    ICStarting = 1,

    /** 
     * Signals that the Immortal Coordinator (IC) has started (specifically, that it has reported "Ready"). 
     * However, "normal" app processing should NOT begin until the 'RecoveryComplete' event is received.
     */
    ICStarted = 2,

    /** Signals that the Immortal Coordinator (IC) has stopped. The first (and only) parameter of this event is the exit code. */
    ICStopped = 3,
    
    /** Signals that the IC is now capable of handling (ie. immediately responding to) self-call RPCs. Occurs after 'RecoveryComplete'. */
    ICReadyForSelfCallRpc = 4,
    
    /** 
     * Signals that the app's normal processing can begin (mainly receiving/sending RPC messages).
     * This event will be signalled even if recovery does not run (ie. the "first-start" case).
     */
    RecoveryComplete = 5,
    
    /**
     * Signals that the app should immediately upgrade its state and code. The handler for this event must not return until BOTH
     * app state and app code have been upgraded. The first (and only) parameter of this event is an AppUpgradeMode enum value.
     */
    UpgradeStateAndCode = 6,
    
    /** Notification of the size (in bytes) of the checkpoint that is about to start being streamed to AmbrosiaConfig.checkpointConsumer. */
    IncomingCheckpointStreamSize = 7,

    /** Signals that the 'InitialMessage' has been received. */
    FirstStart = 8, 

    /** Signals that this immortal instance is now the Primary. */
    BecomingPrimary = 9
}

export enum AppUpgradeMode
{
    Test = 0,
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
    methodID: number;
    rpcType: RPCType;
    jsonParams: object = null; // Will be null when rawParams is set
    rawParams: Uint8Array = null; // Will be null when jsonParams is set; this is used for all serialization formats other than JSON

    constructor(receiveBuffer: Buffer, dataStartIndex: number, dataEndIndex: number)
    {
        super(DispatchedMessageType.RPC);

        let pos: number = dataStartIndex;

        pos++; // Skip over reserved byte
        let methodIDVarInt: DataFormat.varIntResult = DataFormat.readVarInt32(receiveBuffer, pos);
        this.methodID = methodIDVarInt.value
        pos += methodIDVarInt.length;
        this.rpcType = receiveBuffer[pos++];

        // Parse the serialized parameters, which can either be a UTF-8 JSON string, or a raw byte array (for all other serialization formats)
        if (pos < dataEndIndex)
        {
            let jsonString: string = null;
            let isRaw: boolean = (receiveBuffer[pos] !== 123); // 123 = '{'

            if (isRaw)
            {
                // Note: We throw away the first byte, since the "protocol" for raw-format is that the first byte can be any value EXCEPT 123 (0x7B) and will be stripped
                let startIndex: number = pos + 1;
                this.rawParams = new Uint8Array(dataEndIndex - startIndex);
                receiveBuffer.copy(this.rawParams, 0, startIndex, dataEndIndex); // We want to make a copy
            }
            else
            {
                jsonString = StringEncoding.fromUTF8Bytes(receiveBuffer, pos, dataEndIndex - pos).trim();
                this.jsonParams = Utils.jsonParse(jsonString);
            }
        }
    }

    makeDisplayParams(): string
    {
        let allowed: boolean = Configuration.loadedConfig().lbOptions.allowDisplayOfRpcParams;
        let params: string = null;
        
        if (allowed === true)
        {
            params = this.jsonParams ? Utils.jsonStringify(this.jsonParams) : `(${this.rawParams.length} bytes) ${Utils.makeDisplayBytes(this.rawParams)}`;
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
    for (let i = 0 ; i < sections.length; i++)
    {
        dataLength += sections[i].length;
    }
    let messageSize: Uint8Array = DataFormat.writeVarInt32(type.length + dataLength);
    let totalLength: number = messageSize.length + type.length + dataLength;
    let message: Uint8Array = Buffer.concat([messageSize, type, ...sections], totalLength);
    return (message);
}

/** [Internal] Constructs the wire-format (binary) representation of an RPC message. */
export function makeRpcMessage(rpcType: RPCType, destinationInstanceName: string, methodID: number, jsonOrRawArgs: object | Uint8Array): Uint8Array
{
    if (_isRecoveryRunning && (rpcType === RPCType.Impulse))
    {
        throw new Error(`It is a violation of the recovery protocol to send an Impulse RPC during recovery (destination: '${destinationInstanceName}', methodID: ${methodID})`);
    }

    let isSelfCall: boolean = IC.isSelf(destinationInstanceName);
    let destination: Uint8Array = isSelfCall ? EMPTY_BYTE_ARRAY : StringEncoding.toUTF8Bytes(destinationInstanceName);
    let rpcTypeByte: Uint8Array = null;
    let messageSize: number = 0; // Just used to track progress as we add bytes to the message
    let maxArgsSize: number = _bytePool.size / 3; // ie. enough room for 2 messages
    let canOptimize: boolean = (jsonOrRawArgs instanceof Uint8Array) ? (jsonOrRawArgs.length < maxArgsSize) : true; // For jsonArgs we had to postpone computing the length (for perf. reasons)
    let jsonArgs: Uint8Array = null;

    // If needed, prepare the IC to talk to the destination; when the IC receives this message it adds
    // some rows to the CRA connection table (in Azure) which causes the TCP connections to be made.
    // Note that 'destinationInstanceName' MUST have been previously registered; of not, the IC will 
    // report "Error attaching [localInstanceName] to [destinationInstanceName]".
    if (!isSelfCall && IC.isNewDestination(destinationInstanceName))
    {
        // Note: Even if the caller of makeRpcMessage() decides not to send the returned RPC message,
        //       the ATTACHTO message below will still have been sent (ie. this is a true side-effect).
        // Note: By setting 'immediateFlush' to true when we send we are attempting to limit the [performance] damage caused by "polluting"
        //       the queue with a non-RPC message [queued messages can only be sent as an RPCBatch if they are all RPC messages].
        //       If the queue is empty there will be no damage: the ATTACHTO to will simply be sent as a singleton.
        //       If the queue already has RPC's in it [for a different destination instance] then those messages will not be able to be
        //       sent as an RPCBatch, but at least all the RPC's added afterwards [for the new ATTACHTO destination] will be able to.
        let attachToMessage: Uint8Array = makeMessage(MESSAGE_TYPE_BYTE_ATTACHTO, destination);
        IC.sendMessage(attachToMessage, MessageType.AttachTo, destinationInstanceName, true);
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

    if (rpcType === RPCType.Fork) { rpcTypeByte = RPC_TYPE_FORK };
    if (rpcType === RPCType.Impulse) { rpcTypeByte = RPC_TYPE_IMPULSE };
    if (rpcTypeByte === null)
    {
        throw new Error(`Unsupported rpcType '${rpcType}'`);
    }

    if (canOptimize)
    {
        _bytePool.startBlock();
        messageSize += _bytePool.addBuffer(MESSAGE_TYPE_BYTE_RPC);
        messageSize += isSelfCall ? _bytePool.addBytes([0]) : _bytePool.addVarInt32(destinationInstanceName.length);
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
        // Fall-back to not using the _bytePool
        let rawArgs: Uint8Array = (jsonOrRawArgs instanceof Uint8Array) ? jsonOrRawArgs : null;
        let destinationLength: Uint8Array = isSelfCall ? RESERVED_0 : DataFormat.writeVarInt32(destinationInstanceName.length);
        let rpcBody: Uint8Array = makeRpcBody(methodID, rpcTypeByte, jsonArgs, rawArgs);
        let message: Uint8Array = makeMessage(MESSAGE_TYPE_BYTE_RPC, destinationLength, destination, rpcBody);
        return (message);
    }
}

function makeRpcBody(methodID: number, rpcType: Uint8Array, jsonArgs: Uint8Array, rawArgs: Uint8Array): Uint8Array
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

/** 
 * Returns the length of the first log page in receiveBuffer, but only if the buffer contains [at least] one complete log page. 
 * Otherwise, returns -1 (indicating that the caller should keep accumulating bytes in receiveBuffer). 
 */
export function getLogPageLength(receiveBuffer: Buffer): number
{
    if (receiveBuffer.length >= 8) // commiterID (4 bytes) + pageSize (4 bytes)
    {
        let logPageLength: number = DataFormat.readInt32Fixed(receiveBuffer, 4); // receiveBuffer.readInt32LE(4);
        if (logPageLength <= receiveBuffer.length)
        {
            return (logPageLength);
        }
    }
    return (-1);
}

/** 
 * Processes all the messages in a log page.
 * Returns the number of checkpoint bytes to read (if any) before the next log page will arrive.
 */
export function processLogPage(receiveBuffer: Buffer, config: Configuration.AmbrosiaConfig): number
{
    // Parse the header
    // Note: commiterID can be negative
    let commiterID: number = DataFormat.readInt32Fixed(receiveBuffer, 0); // receiveBuffer.readInt32LE(0);
    let logPageLength: number = DataFormat.readInt32Fixed(receiveBuffer, 4); // receiveBuffer.readInt32LE(4);
    let checkSum: bigint = DataFormat.readInt64Fixed(receiveBuffer, 8); // receiveBuffer.readBigInt64LE(8);
    let pageSequenceID: bigint = DataFormat.readInt64Fixed(receiveBuffer, 16); // receiveBuffer.readBigInt64LE(16);
    let pos: number = 24;
    let outgoingCheckpoint: Streams.OutgoingCheckpoint = null;
    let checkpointMessage: Uint8Array = null;

    // Parse/process the message(s)
    while (pos < logPageLength)
    {
        // Parse the message "header"
        let msg: MessageMetaData = new MessageMetaData(receiveBuffer, pos);
        pos = msg.startOfDataIndex;

        if (Utils.canLog(Utils.LoggingLevel.Normal)) // We add this check because this is a high-volume code path, and Utils.log() is expensive
        {
            let showBytes: boolean = config.lbOptions.verboseOutputLogging; // For debugging
            Utils.log(`Received '${MessageType[msg.messageType]}' (${msg.totalLength} bytes)` + (showBytes ? `: ${Utils.makeDisplayBytes(receiveBuffer, msg.startOfMessageIndex, msg.totalLength)}` : ""));
        }

        // Parse the message "data" section
        switch (msg.messageType)
        {
            case MessageType.TakeBecomingPrimaryCheckpoint: // No data
            case MessageType.UpgradeTakeCheckpoint: // No data
                let initialMessage: Uint8Array = makeInitialMessage(StringEncoding.toUTF8Bytes(Root.languageBindingVersion())); // Note: We could send any arbitrary bytes

                if (msg.messageType === MessageType.UpgradeTakeCheckpoint)
                {
                    if (!_isRecoveryRunning)
                    {
                        // A Checkpoint must have preceded this message
                        throw new Error(`"Unexpected message '${MessageType[msg.messageType]}'; a '${MessageType[MessageType.Checkpoint]}' message should have preceded this message`);
                    }
                    // Tell the app to upgrade BEFORE we take a new checkpoint. Note: The handler for this event MUST execute synchronously.
                    Utils.log("Upgrading app (state and code)...");
                    IC.emitAppEvent(AppEventType.UpgradeStateAndCode, AppUpgradeMode.Live);
                }

                outgoingCheckpoint = config.checkpointProducer();
                checkpointMessage = makeCheckpointMessage(outgoingCheckpoint.length);
                
                if (!_isRecoveryRunning)
                {
                    IC.sendMessage(initialMessage, MessageType.InitialMessage);
                }
                IC.sendMessage(checkpointMessage, MessageType.Checkpoint);
                IC.sendCheckpoint(outgoingCheckpoint, () =>
                // Note: This lambda runs ONLY if sendCheckpoint() succeeds
                {
                    if (_isRecoveryRunning)
                    {
                        _isRecoveryRunning = false;
                        IC.onRecoveryComplete();
                    }
                    
                    if (msg.messageType === MessageType.TakeBecomingPrimaryCheckpoint)
                    {
                        IC.emitAppEvent(AppEventType.BecomingPrimary);
                    }    
                    
                    IC.emitAppEvent(AppEventType.RecoveryComplete);
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
                outgoingCheckpoint = config.checkpointProducer();
                checkpointMessage = makeCheckpointMessage(outgoingCheckpoint.length);
                IC.sendMessage(checkpointMessage, MessageType.Checkpoint);
                IC.sendCheckpoint(outgoingCheckpoint);
                break;

            case MessageType.Checkpoint:
                let checkpointLengthVarInt: DataFormat.varIntResult = DataFormat.readVarInt64(receiveBuffer, pos);
                let checkpointLength: number = checkpointLengthVarInt.value;
                Utils.log("Starting recovery [load checkpoint and replay messages]...");
                _isRecoveryRunning = true;

                // We keep track of the number of received/resent messages during recovery
                IC._counters.remoteSentMessageCount = IC._counters.receivedMessageCount = IC._counters.receivedForkMessageCount = IC._counters.sentForkMessageCount = 0; 

                // By returning the checkpoint length we tell the caller to read the next checkpointLength bytes as checkpoint data.
                // Note: It's safe to return here as a 'Checkpoint' will always be the last (only) message in the log page.
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

            case MessageType.UpgradeService: // No data
                // The 'test mode' upgrade
                if (!_isRecoveryRunning)
                {
                    // A Checkpoint must have preceded this message
                    throw new Error(`"Unexpected message '${MessageType[msg.messageType]}'; a '${MessageType[MessageType.Checkpoint]}' message should have preceded this message`);
                }
                // Tell the app to upgrade. Note: The handler for this event MUST execute synchronously.
                Utils.log("Upgrading app (state and code) [in Test Mode]...");
                IC.emitAppEvent(AppEventType.UpgradeStateAndCode, AppUpgradeMode.Test);
                
                // When doing a 'test mode' upgrade there will be no 'completion' message from the IC signalling when 
                // message playback is complete, so we will not end up emitting a 'RecoveryComplete' event to the app
                Utils.log("Resuming recovery after app upgrade..."); 
                break;

            default:
                throw new Error(`Log page ${pageSequenceID} contains a message of unknown type (${msg.messageType}); message: ${Utils.makeDisplayBytes(receiveBuffer, msg.startOfMessageIndex, msg.totalLength)}`);
        }

        pos = msg.endOfMessageIndex;
    }

    return (-1);
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
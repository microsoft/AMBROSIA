import Ambrosia = require("ambrosia-node"); 
import IC = Ambrosia.IC;
import Messages = Ambrosia.Messages;
import StringEncoding = Ambrosia.StringEncoding;
import Utils = Ambrosia.Utils;
import * as PublishedAPI from "./ConsumerInterface.g"; // This is a generated file
import * as Framework from "./PublisherFramework.g"; // This is a generated file [only needed to support code upgrade]

const PAYLOAD_FILL_BYTE: number = 170; // 170 = 10101010
const ONE_MB: number = 1024 * 1024
const ONE_GB: number =  ONE_MB * 1024;
const ONE_HUNDRED_MB: number = ONE_MB * 100;
const CLIENT_START_TIMESTAMP_LENGTH: number = 7; // One byte for each digit pair in the client original start timestamp ([YY][YY][MM][DD][HH][MM][SS])
let _healthTimer: NodeJS.Timer;

/** The roles that the local instance can act as in the test. */
export enum InstanceRoles
{
    /** The local instance is acting as a client in the test (there can be more than one client, but only when there is an instance running the 'Server' role). */
    Client,
    /** The local instance is acting as the server in the test (there can only be one server). */
    Server,
    /** 
     * The local instance is acting as **both** the client and the server in the test. This is the simplest way
     * to run the test. In this role, additional clients should not target this instance as their server.
     */
    Combined
}

/** Namespace for application state. */
export namespace State
{
    export class AppState extends Ambrosia.AmbrosiaAppState
    {
        // Server-side state:

        /** The total number of bytes [from the doWork() message payload] received since the server started running. */
        bytesReceived: number = 0;
        /** The total number of doWork() calls received since the server started running. */
        numCalls: number = 0;
        /** The call number from the previously received doWork() call (for a given client). */
        lastCallNum: { [clientName: string]: number } = {};
        /** The total number of times the checkHealth() method has been called. */
        checkHealthCallNumber: number = 0;
        /** When true, disables the periodic server health check (requested via an impulse message). */
        noHealthCheck: boolean = false;
        /** When true, enables echoing the 'doWork' method call back to the client(s). */
        bidirectional: boolean = false;
        /** The total number of bytes expected to be received from all clients. The server will report a "success" message when this number of bytes have been received. */
        expectedFinalBytesTotal: number = 0

        // Client-side state:

        /** The name of the instance that's acting in the 'Server' role for the test. */
        serverInstanceName: string = "";
        /** The total number of [variable sized] message payload bytes that will be sent in a single round. */
        bytesPerRound: number = 0;
        /** Once the total number of [variable sized] message payload bytes queued reaches (or exceeds) this limit, then the batch will be sent. */
        batchSizeCutoff: number = 0;
        /** The maximum size (in bytes) of the message payload; should be a power of 2 (eg. 65536), and be at least 64. */
        maxMessageSize: number = 0;
        /** The requested number of rounds (of size bytesPerRound). */
        numRounds: number = 0;
        /** The number of rounds (of size bytesPerRound) still left to process. */
        numRoundsLeft: number = 0;
        /** How far through the current round the client is (as a percentage). */
        lastRoundCompletionPercentage: number = 0;
        /** When true, disables descending (halving) message size; instead, a random [power of 2] size between 64 and maxMessageSize will be used. */
        useDescendingSize: boolean = false;
        /** When true, all messages (in all rounds) will be of size maxMessageSize and useDescendingSize will be ignored. */
        useFixedMessageSize: boolean = false;
        /** The call number of the last 'doWork' call sent. */
        callNum: number = 0;
        /** The total number of bytes [from the doWorkEcho() message payload] received since the client started running. */
        echoedBytesReceived: number = 0
        /** The call number from the previously received doWorkEcho() call. */
        echoedLastCallNum: number = 0;
        /** The total number of bytes [from the doWorkEcho() message payload] expected to be received from the server. The client will report a "success" message when this number of bytes have been received. */
        expectedEchoedBytesTotal: number = 0;
        /** The date/time when the client was originally started. This will never change (once set). */
        originalStartDate: number = 0;
        /** Whether to include a post method call in the test. */
        includePostMethod: boolean = false;
        /** The number of times the post method result handler (for 'incrementValue') has been called. */
        postMethodResultCount: number = 0;
        /** The result received by the previously called post method result handler (for 'incrementValue'). */
        lastPostMethodResultValue: number = 1;

        // Common state:

        /** The role of this instance in the test. */
        instanceRole: InstanceRoles = InstanceRoles.Combined;
        /** Optional 'Padding' used to simulate large checkpoints. Each member of the array will be no larger than 100MB. */
        checkpointPadding?: Array<Uint8Array>;
        /** The prefix to use when writing to the output log [this is only used to test upgrade]. */
        loggingPrefix: string = "";
        /** Whether to check the doWork() / doWorkEcho() payload fill bytes. */
        verifyPayload: boolean = false;

        /** Whether the local instance is acting in the 'Server' role for the test. */
        get isServer(): boolean
        {
            return ((this.instanceRole === InstanceRoles.Server) || (this.instanceRole === InstanceRoles.Combined));
        }

        /** Whether the local instance is acting in the 'Client' role for the test. */
        get isClient(): boolean
        {
            return ((this.instanceRole === InstanceRoles.Client) || (this.instanceRole === InstanceRoles.Combined));
        }

        /**
         * @param restoredAppState Supplied only when loading (restoring) a checkpoint, or (for a "VNext" AppState) when upgrading from the prior AppState.\
         * **WARNING:** When loading a checkpoint, restoredAppState will be an object literal, so you must use this to reinstantiate any members that are (or contain) class references.
         */
        constructor(restoredAppState?: AppState)
        {
            super(restoredAppState);

            if (restoredAppState)
            {
                // WARNING: You MUST reinstantiate all members that are (or contain) class references because restoredAppState is data-only

                // Server-side state:
                this.bytesReceived = restoredAppState.bytesReceived;
                this.numCalls = restoredAppState.numCalls;
                this.lastCallNum = restoredAppState.lastCallNum;
                this.checkHealthCallNumber = restoredAppState.checkHealthCallNumber;
                this.noHealthCheck = restoredAppState.noHealthCheck;
                this.bidirectional = restoredAppState.bidirectional;
                this.expectedFinalBytesTotal = restoredAppState.expectedFinalBytesTotal;

                // Client-side state:
                this.serverInstanceName = restoredAppState.serverInstanceName;
                this.bytesPerRound = restoredAppState.bytesPerRound;
                this.batchSizeCutoff = restoredAppState.batchSizeCutoff;
                this.maxMessageSize = restoredAppState.maxMessageSize;
                this.numRounds = restoredAppState.numRounds;
                this.numRoundsLeft = restoredAppState.numRoundsLeft;
                this.lastRoundCompletionPercentage = restoredAppState.lastRoundCompletionPercentage;
                this.useDescendingSize = restoredAppState.useDescendingSize;
                this.useFixedMessageSize = restoredAppState.useFixedMessageSize;
                this.callNum = restoredAppState.callNum;
                this.echoedBytesReceived = restoredAppState.echoedBytesReceived;
                this.echoedLastCallNum = restoredAppState.echoedLastCallNum;
                this.expectedEchoedBytesTotal = restoredAppState.expectedEchoedBytesTotal;
                this.originalStartDate = restoredAppState.originalStartDate;
                if (restoredAppState["includePostMethod"] !== undefined) // So that we can still load older PTI checkpoints
                {
                    this.includePostMethod = restoredAppState.includePostMethod;
                }
                if (restoredAppState["postMethodResultCount"] !== undefined) // So that we can still load older PTI checkpoints
                {
                    this.postMethodResultCount = restoredAppState.postMethodResultCount;
                }
                if (restoredAppState["lastPostMethodResultValue"] !== undefined) // So that we can still load older PTI checkpoints
                {
                    this.lastPostMethodResultValue = restoredAppState.lastPostMethodResultValue;
                }

                // Common state:
                this.instanceRole = restoredAppState.instanceRole;
                this.checkpointPadding = restoredAppState.checkpointPadding;
                this.loggingPrefix = restoredAppState.loggingPrefix;
                if (restoredAppState["verifyPayload"] !== undefined) // So that we can still load older PTI checkpoints
                {
                    this.verifyPayload = restoredAppState.verifyPayload;
                }
            }
            else
            {
                // Initialize application state
                this.originalStartDate = Date.now();                
            }
        }

        convert(): AppStateV2
        {
            return (AppStateV2.fromPriorAppState(this));
        }
    }

    /** Class representing upgraded application state. */
    export class AppStateV2 extends AppState
    {
        /**
         * @param restoredAppState Supplied only when loading (restoring) a checkpoint, or (for a "VNext" AppState) when upgrading from the prior AppState.\
         * **WARNING:** When loading a checkpoint, restoredAppState will be an object literal, so you must use this to reinstantiate any members that are (or contain) class references.
         */
        constructor(restoredAppState?: AppStateV2)
        {
            super(restoredAppState);
        }

        /** Factory method that creates a new AppStateV2 instance from an existing AppState instance. Called [once] during upgrade. */
        static fromPriorAppState(priorAppState: AppState): AppStateV2
        {
            // It's sufficient to simply pass priorAppState as the 'restoredAppState' because AppStateV2 has exactly the same shape as AppState
            const appStateV2: AppStateV2 = new AppStateV2(priorAppState);

            // Upgrading, so transform (as needed) the supplied old state, and - if needed - [re]initialize the new state
            appStateV2.loggingPrefix = "VNext";

            return (appStateV2);
        }
    }

    /** 
     * Only assign this using the return value of IC.start(), the return value of the upgrade() method of your AmbrosiaAppState
     * instance, and [if not using the generated checkpointConsumer()] in the 'onFinished' callback of an IncomingCheckpoint object. 
     */
    export let _appState: AppState | AppStateV2;
}

/** Logs the specified message (regardless of the configured 'outputLoggingLevel'). */
export function log(message: string)
{
    Utils.log(message, State._appState?.loggingPrefix, Utils.LoggingLevel.Minimal);
}

/** Namespace for the "server-side" published methods. */
export namespace ServerAPI
{
    /** 
     * A method whose purpose is to update _appState with each call.
     * @param rawParams A custom serialized byte array of all method parameters.
     * @ambrosia publish=true, methodID=1 
     */
    export function doWork(rawParams: Uint8Array): void
    {
        const buffer: Buffer = Buffer.from(rawParams.buffer);
        const currCallNum: number = buffer.readUInt32LE(0); // This changes with every message
        const clientNameLength: number = buffer[4];
        const clientName: string = StringEncoding.fromUTF8Bytes(buffer, 5, clientNameLength);
        if (!State._appState.lastCallNum[clientName])
        {
            State._appState.lastCallNum[clientName] = 0;
        }
        const expectedCallNum: number = State._appState.lastCallNum[clientName] + 1;

        if (currCallNum !== expectedCallNum)
        {
            log(`Error: Out of order message from '${clientName}' (expected ${expectedCallNum}, got ${currCallNum})`);
        }

        if (State._appState.verifyPayload)
        {
            verifyPayloadBytes(buffer, 4 + 1 + clientNameLength + CLIENT_START_TIMESTAMP_LENGTH);
        }

        State._appState.lastCallNum[clientName] = currCallNum;
        State._appState.bytesReceived += rawParams.length;
        State._appState.numCalls++;
        
        // const previous100MBReceived: number = Math.floor((State._appState.bytesReceived - rawParams.length) / ONE_HUNDRED_MB);
        // const current100MBReceived: number = Math.floor(State._appState.bytesReceived / ONE_HUNDRED_MB);
        // if (current100MBReceived !== previous100MBReceived)
        // {
        //     log(`Service received ${current100MBReceived * 100} MB so far`);
        // }

        const previousGBReceived: number = Math.floor((State._appState.bytesReceived - rawParams.length) / ONE_GB);
        const currentGBReceived: number = Math.floor(State._appState.bytesReceived / ONE_GB);
        if (currentGBReceived !== previousGBReceived)
        {
            log(`Service received ${currentGBReceived * 1024} MB so far`);
        }

        if (State._appState.bidirectional)
        {
            // Note: Because the doWork() messages get delivered to the server in RPCBatch messages, and because processLogPage() processes all
            //       RPC messages in a batch at once, this means we'll get implicit batching behavior for all our outgoing doWorkEcho() calls.
            //       This can be verified with: log(`DEBUG: QueueLength = ${IC.queueLength()}`);
            PublishedAPI.setDestinationInstance(clientName);
            PublishedAPI.ClientAPI.doWorkEcho_Fork(rawParams);
        }
    }

    /** 
     * A method that reports (to the console) the current application state.
     * @ambrosia publish=true, methodID=2
     */
    export function reportState(isFinalState: boolean): void
    {
        if (isFinalState)
        {
            log(`Bytes received: ${State._appState.bytesReceived}`); // This text is looked for by the test harness
            log("DONE"); // This text is looked for by the test harness
            if (State._appState.isServer && (State._appState.bytesReceived === State._appState.expectedFinalBytesTotal))
            {
                log(`SUCCESS: The expected number of bytes (${State._appState.expectedFinalBytesTotal}) have been received`); // This text is looked for by the test harness
            }
            if (State._appState.instanceRole === InstanceRoles.Combined)
            {
                if (!State._appState.bidirectional)
                {
                    // Stop the Combined instance
                    stopCombinedInstance();
                }
                else
                {
                    // Let the final doWorkEcho() [which will arrive later] stop the Combined instance
                }
            }
        }
        else
        {
            log(`numCalls: ${State._appState.numCalls}, bytesReceived: ${State._appState.bytesReceived}`);
        }
    }

    /** 
     * A method whose purpose is [mainly] to be a rapidly occurring Impulse method to test if this causes issues for recovery.\
     * Also periodically (eg. every ~5 seconds) reports that the Server is still running.
     * @ambrosia publish=true, methodID=3 
     */
    export function checkHealth(currentTime: number): void
    {
        if (++State._appState.checkHealthCallNumber % 200 === 0) // Every ~5 seconds [if checkHealth() is called every 25ms]
        {
            log(`Service healthy after ${State._appState.checkHealthCallNumber} checks at ${Utils.getTime(currentTime)}`);
        }
    }

    /** 
     * A simple post method. Returns the supplied value incremented by 1.
     * @ambrosia publish=true
     */
    export function incrementValue(value: number): number
    {
        return (value + 1);
    }
}

/** Stops the Combined instance, after waiting (but only in the case of --includePostMethod) for any queued outgoing messages. */
function stopCombinedInstance(isRetry: boolean = false): void
{
    if (State._appState.instanceRole === InstanceRoles.Combined)
    {
        if (State._appState.includePostMethod)
        {
            const queueLength: number = IC.queueLength();
            const inFlightMethodCount: number = IC.inFlightPostMethodsCount();
            if ((queueLength > 0) || (inFlightMethodCount > 0))
            {
                log(`Waiting for ${IC.queueLength()} outgoing messages and ${IC.inFlightPostMethodsCount()} in-flight post methods...`);
                setTimeout(() => stopCombinedInstance(true), 200);
                return;
            }
            else
            {
                if (isRetry)
                {
                    log(`Outgoing message queue is empty; there are no in-flight post methods`);
                }
                checkFinalPostMethod();
                IC.stop();
            }
        }
        else
        {
            IC.stop();
        }
    }
}

/** For a client, checks that the final 'incrementValue' post method result received was for the final 'doWork' message. */
function checkFinalPostMethod(): void
{
    if (State._appState.isClient && State._appState.includePostMethod)
    {
        const inFlightMethodsCount: number = IC.inFlightPostMethodsCount();
        if (inFlightMethodsCount > 0)
        {
            log(`Waiting for ${IC.inFlightPostMethodsCount()} in-flight post methods...`);
            setTimeout(checkFinalPostMethod, 200);
            return;
        }
        else
        {
            const finalCallNum: number = State._appState.callNum;
            if (State._appState.postMethodResultCount !== State._appState.callNum)
            {
                throw new Error(`The result handler for the 'incrementValue' post method was not called the expected number of times (expected ${State._appState.callNum}, but it was called ${State._appState.postMethodResultCount} times)`);
            }
            log(`SUCCESS: The result handler for the 'incrementValue' post method was called the expected number of times (${State._appState.callNum})`);
        }
    }
}

/** Throws if the byte values in buffer (starting from startPos) are not all PAYLOAD_FILL_BYTE and stops the process. */
function verifyPayloadBytes(buffer: Buffer, startPos: number): void
{
    try
    {
        for (let pos = startPos; pos < buffer.length; pos++)
        {
            if (buffer[pos] !== PAYLOAD_FILL_BYTE)
            {
                const displayBytes: string = Utils.makeDisplayBytes(buffer, pos, 64);
                throw new Error(`PTI message payload is corrupt: Byte at position ${pos} (of ${buffer.length}) is ${buffer[pos]}, not ${PAYLOAD_FILL_BYTE} as expected [corrupted fragment: ${displayBytes}]`);
            }
        }
    }
    catch (error: unknown)
    {
        Utils.log(Utils.makeError(error));
        process.exit(-1); // Hard-stop [otherwise the Immortal will keep running, but reporting many "Out of order message" errors until the round completes]
    }
}

/** Returns the supplied date as an array of bytes, with one byte for each digit pair in [YY][YY][MM][DD][HH][MM][SS]. */
function makeTimestampBytes(date: number): Uint8Array
{
    // Note: If you change the number of bytes in the timestamp, you MUST also change CLIENT_RUN_TIMESTAMP_LENGTH.
    const runDate: Date = new Date(date);
    const millennia: number = Math.floor(runDate.getFullYear() / 1000);
    const century: number = Math.floor((runDate.getFullYear() - (1000 * millennia)) / 100);
    const decade: number = Math.floor((runDate.getFullYear() - (1000 * millennia) - (100 * century)) / 10);
    const year: number = runDate.getFullYear() - (1000 * millennia) - (100 * century) - (10 * decade);
    const timestampBytes: Uint8Array = new Uint8Array([(millennia * 10) + century, (decade * 10) + year, runDate.getMonth() + 1, runDate.getDate(), runDate.getHours(), runDate.getMinutes(), runDate.getSeconds()]);
    return (timestampBytes);
}

/** Namespace for the "client-side" published methods. */
export namespace ClientAPI
{
    /** UTF-8 bytes representing the name of the client instance. These bytes will be added to the 'doWork' message payload (along with the call number). */
    export let clientNameBytes: Uint8Array; // We "cache" this for speed

    /** A byte array representing the timestamp of the original start time of the client, with one byte for each digit pair in [YY][YY][MM][DD][HH][MM][SS]. */
    export let originalStartTimestampBytes: Uint8Array; // We "cache" this for speed

    /** 
     * Builds a batch of messages (each of size 'numRPCBytes') until the batch contains at least State._appState.batchSizeCutoff bytes. The batch is then sent.\
     * This continues until a total of State._appState.bytesPerRound have been sent.\
     * With the round complete, numRPCBytes is adjusted, then the whole cycle repeats until State._appState.numRoundsLeft reaches 0.
     * @ambrosia publish=true, methodID=4
     */
    export function continueSendingMessages(numRPCBytes: number, iterationWithinRound: number, startTimeOfRound: number): void
    {
        let bytesSentInCurrentBatch: number = 0;
        let isTailOfPriorRoundACompleteBatch: boolean = false; // Note: A batch can span 2 rounds (ie. the final partial batch of round n can be flushed as part of first batch of round n+1)

        for (; State._appState.numRoundsLeft > 0; State._appState.numRoundsLeft--)
        {
            const iterations = State._appState.bytesPerRound / numRPCBytes; // Our parameter validation ensures this will always be an integer
            const buffer: Buffer = Buffer.alloc(numRPCBytes);
            buffer.fill(PAYLOAD_FILL_BYTE); // Completely fill the buffer with non-zero values (170 = 10101010)

            if (!isTailOfPriorRoundACompleteBatch)
            {
                if (iterationWithinRound === 0)
                {
                    // A new round is starting
                    log(`Starting new round (with ${State._appState.numRoundsLeft} round${State._appState.numRoundsLeft === 1 ? "" : "s"} left) of ${iterations} messages of ${numRPCBytes} bytes each (${(State._appState.bytesPerRound / ONE_MB).toFixed((State._appState.bytesPerRound < 16384) ? 6 : 2)} MB/round)`);
                    startTimeOfRound = Date.now();
                }
                else
                {
                    // log(`Continuing round (${iterationWithinRound} messages sent, ${iterations - iterationWithinRound} messages remain)...`);
                }
            }
            else
            {
                // The "tail" of the prior round was a complete batch (ie. contained batchSizeCutoff bytes [or contained a single message larger than batchSizeCutoff]).
                // In this case, the bytesSentInCurrentBatch (which were actually sent as the final batch of the prior round) will equal [or exceed] batchSizeCutoff, 
                // so the "iterationWithinRound" loop below will immediately "recurse" to start the "real" next round.
            }

            // Send message batch
            for (; iterationWithinRound < iterations; iterationWithinRound++)
            {
                if (bytesSentInCurrentBatch >= State._appState.batchSizeCutoff)
                {
                    PublishedAPI.setDestinationInstance(IC.instanceName());
                    PublishedAPI.ClientAPI.continueSendingMessages_EnqueueFork(numRPCBytes, iterationWithinRound, startTimeOfRound); // "Recurse" to continue (or complete) the round
                    // PublishedAPI.setDestinationInstance(State._appState.serverInstanceName);
                    // PublishedAPI.ServerAPI.reportState_EnqueueFork(false);
                    IC.flushQueue();

                    // Report client's progress through round [but only as we cross each 10% completion boundary]
                    const percentInterval: number = 10; // Report at every 10%
                    const currentRound: number = (State._appState.numRounds - State._appState.numRoundsLeft) + 1;
                    const roundCompletionPercentage: number = (iterationWithinRound / iterations) * 100; 
                    const isTenPercentBoundaryCrossed: boolean = Math.floor(roundCompletionPercentage / percentInterval) > Math.floor(State._appState.lastRoundCompletionPercentage / percentInterval);
                    if (isTenPercentBoundaryCrossed)
                    {
                        log(`Client is ${Math.floor(roundCompletionPercentage)}% through round #${currentRound}`);
                    }
                    State._appState.lastRoundCompletionPercentage = roundCompletionPercentage;
                    return;
                }

                buffer.writeUInt32LE(++State._appState.callNum, 0); // Set (overwrite) the first 4 bytes to the callNum
                buffer[4] = clientNameBytes.length;
                buffer.set(clientNameBytes, 5);
                buffer.set(originalStartTimestampBytes, 5 + clientNameBytes.length);
                PublishedAPI.setDestinationInstance(State._appState.serverInstanceName);
                PublishedAPI.ServerAPI.doWork_EnqueueFork(buffer);
                bytesSentInCurrentBatch += numRPCBytes;

                if (State._appState.includePostMethod)
                {
                    PublishedAPI.ServerAPI.incrementValue_Post(State._appState.callNum + 1, State._appState.callNum);
                }
            }

            // The round has ended...
            if (State._appState.numRoundsLeft === 1)
            {
                // We're at the end of the final round, so we need to flush the partial final batch.
                // Note: In the case of "(bytesPerRound % batchSizeCutoff) == 0", then the "partial final batch" will actually be a complete batch.
                IC.flushQueue();
            }
            else
            {
                // We let the first batch of the next round flush the "tail" (ie. the partial [or complete] final batch) of the current round.
                // Note: In the case of "(bytesPerRound % batchSizeCutoff) == 0", then the "partial final batch" will actually be a complete batch.
                //       Further, if batchSizeCutoff is less than the message size (numRPCBytes) then the "partial final batch" is also a complete batch (since each batch consists of a single message).
                // Note: Because a batch can span rounds, it can contain messages of 2 different sizes.
                isTailOfPriorRoundACompleteBatch = (bytesSentInCurrentBatch === State._appState.batchSizeCutoff) || (State._appState.batchSizeCutoff < numRPCBytes);
            }

            // Report throughput
            const endTimeOfRound: number = Date.now();
            const roundDurationInMs: number = Math.max(1, endTimeOfRound - startTimeOfRound); // Don't allow a duration of 0ms since this results in all xxxPerSecond values being "Infinity"
            const numberOfBytesSent: number = iterations * numRPCBytes;
            const numberOfGigabytesSent: number = numberOfBytesSent / ONE_GB;
            const gigabytesPerSecond: number = numberOfGigabytesSent / (roundDurationInMs / 1000);
            const messagesPerSecond: number = (iterations / roundDurationInMs) * 1000; // Note: This excludes the 'continueSendingMessages' message [which is an "overhead" message]

            log(`Round complete (${messagesPerSecond.toFixed(2)} messages/sec, ${(gigabytesPerSecond * 1024).toFixed(2)} MB/sec, ${gigabytesPerSecond.toFixed(8)} GB/sec)`);
            
            // Prepare for the next round
            iterationWithinRound = 0;
            State._appState.lastRoundCompletionPercentage = 0;
            if (!State._appState.useFixedMessageSize)
            {
                if (State._appState.useDescendingSize)
                {
                    // Halve the message size (but not below 64 bytes)
                    if (numRPCBytes > 64)
                    {
                        numRPCBytes >>= 1;
                    }
                }
                else
                {
                    // Use a random message size between 64 and maxMessageSize
                    const minPower2: number = 6;
                    const maxPower2: number = Math.log2(State._appState.maxMessageSize);
                    const randomPower2: number = minPower2 + Math.floor((Math.random() * 100) % ((maxPower2 - minPower2) + 1)); // 100 is just a value that will always be greater than (maxPower2 - minPower2) + 1)
                    numRPCBytes = 1 << randomPower2;
                }
            }

            if (State._appState.numRoundsLeft > 1)
            {
                PublishedAPI.setDestinationInstance(State._appState.serverInstanceName);
                PublishedAPI.ServerAPI.reportState_Fork(false);
            }
        }

        // All rounds have ended
        log(`All rounds complete (${State._appState.callNum} messages sent)`); // This text is looked for by the test harness
        if (_healthTimer)
        { 
            clearTimeout(_healthTimer)
        };
        PublishedAPI.setDestinationInstance(State._appState.serverInstanceName);
        PublishedAPI.ServerAPI.reportState_Fork(true);

        if ((State._appState.instanceRole === InstanceRoles.Client) && State._appState.includePostMethod)
        {
            checkFinalPostMethod();
        }    
    }

    let _noMoreEchoCallsExpected: boolean = false;

    /** 
     * A client method whose purpose is simply to be called [by the server] as an "echo" of the doWork() call sent to the server [by the client].
     * @param rawParams A custom serialized byte array of all method parameters.
     * @ambrosia publish=true, methodID=5 
     */
    // Note: The equivalent method in C# PTI is MAsync() in C:\src\git\AMBROSIA\InternalImmortals\PerformanceTestInterruptible\Client\Program.cs
    export function doWorkEcho(rawParams: Uint8Array): void
    {
        const buffer: Buffer = Buffer.from(rawParams.buffer);
        const currCallNum: number = buffer.readUInt32LE(0); // This changes with every message
        const expectedCallNum: number = State._appState.echoedLastCallNum + 1;

        if (currCallNum !== expectedCallNum)
        {
            log(`Error: Out of order echoed message (expected ${expectedCallNum}, got ${currCallNum})`);
        }

        if (State._appState.verifyPayload)
        {
            verifyPayloadBytes(buffer, 4 + 1 + clientNameBytes.length + CLIENT_START_TIMESTAMP_LENGTH);
        }

        if (_noMoreEchoCallsExpected)
        {
            log(`Error: doWorkEcho() called (call #${currCallNum} with ${rawParams.length} bytes) after receiving all expected echoed bytes (${State._appState.expectedEchoedBytesTotal})`);
        }

        State._appState.echoedLastCallNum = currCallNum;
        State._appState.echoedBytesReceived += rawParams.length;

        const previousGBReceived: number = Math.floor((State._appState.echoedBytesReceived - rawParams.length) / ONE_GB);
        const currentGBReceived: number = Math.floor(State._appState.echoedBytesReceived / ONE_GB);
        if (currentGBReceived !== previousGBReceived)
        {
            log(`Client received ${currentGBReceived * 1024} MB so far`);
        }

        if (State._appState.echoedBytesReceived === State._appState.expectedEchoedBytesTotal)
        {
            log(`SUCCESS: The expected number of echoed bytes (${State._appState.expectedEchoedBytesTotal}) have been received`); // This text is looked for by the test harness
            _noMoreEchoCallsExpected = true;

            if (State._appState.instanceRole === InstanceRoles.Combined)
            {
                // Stop the Combined instance
                stopCombinedInstance();
            }
        }
    }
}

/** Namespace for Ambrosia AppEvent handlers. */
export namespace EventHandlers
{
    export function onICConnected(): void
    {
        // We don't check State._appState.isClient here because it's not reliable [yet] - it could change after a checkpoint is loaded.
        // Instead, we just always set ClientAPI.clientNameBytes (it's benign to set it if we're the server).
        ClientAPI.clientNameBytes = StringEncoding.toUTF8Bytes(IC.instanceName());
    }

    export function onFirstStart(): void
    {
        log(`${IC.instanceName()} in entry point`);

        if (State._appState.isClient)
        {
            ClientAPI.originalStartTimestampBytes = makeTimestampBytes(State._appState.originalStartDate);
            log(`Client original start date: ${Utils.makeDisplayBytes(ClientAPI.originalStartTimestampBytes)}`)

            PublishedAPI.setDestinationInstance(IC.instanceName());
            PublishedAPI.ClientAPI.continueSendingMessages_Fork(State._appState.maxMessageSize, 0, Date.now()); 
        }
    }

    export function onBecomingPrimary(): void
    {
        log(`Becoming primary`);
 
        if (State._appState.isServer && !State._appState.noHealthCheck)
        {
            _healthTimer = setInterval(() => 
            {
                PublishedAPI.setDestinationInstance(State._appState.serverInstanceName);
                PublishedAPI.ServerAPI.checkHealth_Impulse(Date.now()); 
            }, 25); // Nominally ~40 per second, in reality closer to ~28 per second (ie. every ~35ms)
        }
    }

    export function onRecoveryComplete(): void
    {
        if (State._appState.callNum > 0)
        {
            log(`Recovery complete: Resuming test at call #${State._appState.callNum + 1}`);
        }
    }

    export function onCheckpointLoaded(checkpointSizeInBytes: number): void
    {
        const roleStateInfo: string = State._appState.isClient ? `Last call #${State._appState.callNum}` : `${State._appState.numCalls} calls received`;
        log(`Checkpoint loaded (${checkpointSizeInBytes} bytes): ${roleStateInfo}`);
        
        if (State._appState.isClient)
        {
            ClientAPI.originalStartTimestampBytes = makeTimestampBytes(State._appState.originalStartDate);
            log(`Client original start date: ${Utils.makeDisplayBytes(ClientAPI.originalStartTimestampBytes)}`)
        }
    }

    export function onCheckpointSaved(): void
    {
        const roleStateInfo: string = State._appState.isClient ? `Last call #${State._appState.callNum}` : `${State._appState.numCalls} calls received`;
        log(`Checkpoint saved: ${roleStateInfo}`);
    }

    export function onUpgradeState(upgradeMode: Messages.AppUpgradeMode)
    {
        State._appState = State._appState.upgrade<State.AppStateV2>(State.AppStateV2);
    }

    export function onUpgradeCode(upgradeMode: Messages.AppUpgradeMode)
    {
        IC.upgrade(Framework.messageDispatcher, Framework.checkpointProducer, Framework.checkpointConsumer); // A no-op code upgrade
    }

    export function onUpgradeComplete(): void
    {
        log(`Successfully upgraded!`); // This should be logged as "VNext: Successfully upgraded!"
    }
}
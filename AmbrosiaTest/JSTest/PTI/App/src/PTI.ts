import Ambrosia = require("ambrosia-node"); 
import IC = Ambrosia.IC;
import Messages = Ambrosia.Messages;
import Utils = Ambrosia.Utils;
import * as PublishedAPI from "./ConsumerInterface.g"; // This is a generated file
import * as Framework from "./PublisherFramework.g"; // This is a generated file [only needed to support code upgrade]

const ONE_MB: number = 1024 * 1024
const ONE_GB: number =  ONE_MB * 1024;
const ONE_HUNDRED_MB: number = ONE_MB * 100;
const EXCLUDE_FROM_COMP_PREFIX: string = "*X* "; // Lines that start with this are excluded from comparison by the test harness
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

        /** The name of the instance that's acting in the 'Client' role for the test. */
        clientInstanceName: string = "";
        /** The total number of bytes [from the doWork() message payload] received since the server started running. */
        bytesReceived: number = 0;
        /** The total number of doWork() calls received since the server started running. */
        numCalls: number = 0;
        /** The call number from the previously received doWork() call. */
        lastCallNum: number = 0;
        /** The total number of times the checkHealth() method has been called. */
        checkHealthCallNumber: number = 0;
        /** When true, disables the periodic server health check (requested via an impulse message). */
        noHealthCheck: boolean = false;
        /** When true, enables echoing the 'doWork' method call back to the client (as specified by clientInstanceName). */
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
        /** The maximum size (in bytes) of the message payload; should be a power of 2 (eg. 65536), and be at least 16. */
        maxMessageSize: number = 0;
        /** The requested number of rounds (of size bytesPerRound). */
        numRounds: number = 0;
        /** The number of rounds (of size bytesPerRound) still left to process. */
        numRoundsLeft: number = 0;
        /** How far through the current round the client is (as a percentage). */
        lastRoundCompletionPercentage: number = 0;
        /** When true, disables descending (halving) message size; instead, a random [power of 2] size between 16 and maxMessageSize will be used. */
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

        // Common state:

        /** The role of this instance in the test. */
        instanceRole: InstanceRoles = InstanceRoles.Combined;
        /** Optional 'Padding' used to simulate large checkpoints. Each member of the array will be no larger than 100MB. */
        checkpointPadding?: Array<Uint8Array>;
        /** The prefix to use when writing to the output log [this is only used to test upgrade]. */
        loggingPrefix: string = "";

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
                this.clientInstanceName = restoredAppState.clientInstanceName;
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

                // Common state:
                this.instanceRole = restoredAppState.instanceRole;
                this.checkpointPadding = restoredAppState.checkpointPadding;
                this.loggingPrefix = restoredAppState.loggingPrefix;
            }
            else
            {
                // TODO: Initialize your application state here
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
            appStateV2.loggingPrefix = "V2";

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

/** 
 * Updates the instance name that the published API targets.
 * @param roleToTarget Either 'Client' or 'Server' ('Combined' is invalid).
 */
function setDestination(roleToTarget: InstanceRoles): void
{
    switch (roleToTarget)
    {
        case InstanceRoles.Server:
            PublishedAPI.setDestinationInstance(State._appState.serverInstanceName);
            break;
        case InstanceRoles.Client:
            PublishedAPI.setDestinationInstance(State._appState.clientInstanceName);
            break;
        default:
            throw new Error(`The specified roleToTarget ('${InstanceRoles[roleToTarget]}') is not supported in this context`);
    }
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
        const expectedCallNum: number = State._appState.lastCallNum + 1;

        if (currCallNum !== expectedCallNum)
        {
            log(`Error: Out of order message (expected ${expectedCallNum}, got ${currCallNum})`);
        }
        State._appState.lastCallNum = currCallNum;
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
            setDestination(InstanceRoles.Client);
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
                log(`SUCCESS: The expected number of bytes (${State._appState.expectedFinalBytesTotal}) have been received`);
            }
            if (State._appState.instanceRole === InstanceRoles.Combined)
            {
                // Stop the Combined instance
                if (State._appState.bidirectional)
                {
                    // Allow some time for the final 'doWorkEcho()' messages to be sent/received     
                    setTimeout(() => IC.stop(), 200);
                }
                else
                {
                    IC.stop();
                }
            }
        }
        else
        {
            log(`${EXCLUDE_FROM_COMP_PREFIX}numCalls: ${State._appState.numCalls}, bytesReceived: ${State._appState.bytesReceived}`);
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
            log(`${EXCLUDE_FROM_COMP_PREFIX}Service healthy after ${State._appState.checkHealthCallNumber} checks at ${Utils.getTime(currentTime)}`);
        }
    }
}

/** Namespace for the "client-side" published methods. */
export namespace ClientAPI
{
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
            buffer.fill(170); // Completely fill the buffer with non-zero values (170 = 10101010)

            if (!isTailOfPriorRoundACompleteBatch)
            {
                if (iterationWithinRound === 0)
                {
                    // A new round is starting
                    log(`${EXCLUDE_FROM_COMP_PREFIX}Starting new round (with ${State._appState.numRoundsLeft} round${State._appState.numRoundsLeft === 1 ? "" : "s"} left) of ${iterations} messages of ${numRPCBytes} bytes each (${(State._appState.bytesPerRound / ONE_MB).toFixed((State._appState.bytesPerRound < 16384) ? 6 : 2)} MB/round)`);
                    startTimeOfRound = Date.now();
                }
                else
                {
                    // log(`Continuing round (${iterationWithinRound} messages sent, ${iterations - iterationWithinRound} messages remain)...`);
                }
            }
            else
            {
                // The "tail" of the prior round was a complete batch (ie. contained batchSizeCutoff bytes).
                // In this case, the bytesSentInCurrentBatch (which were actually sent as the final batch of the prior round) will equal
                // batchSizeCutoff, so the "iterationWithinRound" loop below will immediately "recurse" to start the "real" next round.
            }

            // Send message batch
            for (; iterationWithinRound < iterations; iterationWithinRound++)
            {
                if (bytesSentInCurrentBatch >= State._appState.batchSizeCutoff)
                {
                    setDestination(InstanceRoles.Client);
                    PublishedAPI.ClientAPI.continueSendingMessages_EnqueueFork(numRPCBytes, iterationWithinRound, startTimeOfRound); // "Recurse" to continue (or complete) the round
                    // setDestination(InstanceRoles.Server);
                    // Self.ServerAPI.reportState_EnqueueFork(false);
                    IC.flushQueue();

                    // Report client's progress through round [but only as we cross each 10% completion boundary]
                    const percentInterval: number = 10; // Report at every 10%
                    const currentRound: number = (State._appState.numRounds - State._appState.numRoundsLeft) + 1;
                    const roundCompletionPercentage: number = (iterationWithinRound / iterations) * 100; 
                    const isTenPercentBoundaryCrossed: boolean = Math.floor(roundCompletionPercentage / percentInterval) > Math.floor(State._appState.lastRoundCompletionPercentage / percentInterval);
                    if (isTenPercentBoundaryCrossed)
                    {
                        log(`${EXCLUDE_FROM_COMP_PREFIX}Client is ${Math.floor(roundCompletionPercentage)}% through round #${currentRound}`);
                    }
                    State._appState.lastRoundCompletionPercentage = roundCompletionPercentage;
                    return;
                }

                buffer.writeUInt32LE(++State._appState.callNum, 0); // Set (overwrite) the first 4 bytes to the callNum
                setDestination(InstanceRoles.Server);                
                PublishedAPI.ServerAPI.doWork_EnqueueFork(buffer);
                bytesSentInCurrentBatch += numRPCBytes;
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
                // We can let the first batch of the next round flush the "tail" (ie. the partial final batch) of the current round.
                // Note: In the case of "(bytesPerRound % batchSizeCutoff) == 0", then the "partial final batch" will actually be a complete batch.
                // Note: Because a batch can span rounds, it can contain messages of 2 different sizes.
                isTailOfPriorRoundACompleteBatch = (bytesSentInCurrentBatch === State._appState.batchSizeCutoff);
            }

            // Report throughput
            const endTimeOfRound: number = Date.now();
            const roundDurationInMs: number = endTimeOfRound - startTimeOfRound;
            const numberOfBytesSent: number = iterations * numRPCBytes;
            const numberOfGigabytesSent: number = numberOfBytesSent / ONE_GB;
            const gigabytesPerSecond: number = numberOfGigabytesSent / (roundDurationInMs / 1000);
            const messagesPerSecond: number = (iterations / roundDurationInMs) * 1000; // Note: This excludes the 'continueSendingMessages' message [which is an "overhead" message]

            log(`${EXCLUDE_FROM_COMP_PREFIX}Round complete (${messagesPerSecond.toFixed(2)} messages/sec, ${(gigabytesPerSecond * 1024).toFixed(2)} MB/sec, ${gigabytesPerSecond.toFixed(8)} GB/sec)`);
            
            // Prepare for the next round
            iterationWithinRound = 0;
            State._appState.lastRoundCompletionPercentage = 0;
            if (!State._appState.useFixedMessageSize)
            {
                if (State._appState.useDescendingSize)
                {
                    // Halve the message size (but not below 16 bytes)
                    if (numRPCBytes > 16)
                    {
                        numRPCBytes >>= 1;
                    }
                }
                else
                {
                    // Use a random message size between 16 and maxMessageSize
                    const minPower2: number = 4;
                    const maxPower2: number = Math.log2(State._appState.maxMessageSize);
                    const randomPower2: number = minPower2 + Math.floor((Math.random() * 100) % ((maxPower2 - minPower2) + 1)); // 100 is just a value that will always be greater than (maxPower2 - minPower2) + 1)
                    numRPCBytes = 1 << randomPower2;
                }
            }

            if (State._appState.numRoundsLeft > 1)
            {
                setDestination(InstanceRoles.Server);
                PublishedAPI.ServerAPI.reportState_Fork(false);
            }
        }

        // All rounds have ended
        log(`All rounds complete (${State._appState.callNum} messages sent)`);
        if (_healthTimer)
        { 
            clearTimeout(_healthTimer)
        };
        setDestination(InstanceRoles.Server);
        PublishedAPI.ServerAPI.reportState_Fork(true);
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
            log(`SUCCESS: The expected number of echoed bytes (${State._appState.expectedEchoedBytesTotal}) have been received`);
            _noMoreEchoCallsExpected = true;
        }
    }
}

/** Namespace for Ambrosia AppEvent handlers. */
export namespace EventHandlers
{
    export function onFirstStart(): void
    {
        log(`${EXCLUDE_FROM_COMP_PREFIX}${IC.instanceName()} in entry point`);

        if (State._appState.isClient)
        {
            setDestination(InstanceRoles.Client);
            PublishedAPI.ClientAPI.continueSendingMessages_Fork(State._appState.maxMessageSize, 0, Date.now()); 
        }
    }

    export function onBecomingPrimary(): void
    {
        log(`${EXCLUDE_FROM_COMP_PREFIX}Becoming primary`);
 
        if (State._appState.isServer && !State._appState.noHealthCheck)
        {
            _healthTimer = setInterval(() => 
            {
                setDestination(InstanceRoles.Server);                 
                PublishedAPI.ServerAPI.checkHealth_Impulse(Date.now()); 
            }, 25); // Nominally ~40 per second, in reality closer to ~28 per second (ie. every ~35ms)
        }
    }

    export function onRecoveryComplete(): void
    {
        if (State._appState.callNum > 0)
        {
            log(`${EXCLUDE_FROM_COMP_PREFIX}Recovery complete: Resuming test at call #${State._appState.callNum + 1}`);
        }
    }

    export function onCheckpointLoaded(checkpointSizeInBytes: number): void
    {
        const roleStateInfo: string = State._appState.isClient ? `Last call #${State._appState.callNum}` : `${State._appState.numCalls} calls received`;
        log(`${EXCLUDE_FROM_COMP_PREFIX}Checkpoint loaded (${checkpointSizeInBytes} bytes): ${roleStateInfo}`);
    }

    export function onCheckpointSaved(): void
    {
        const roleStateInfo: string = State._appState.isClient ? `Last call #${State._appState.callNum}` : `${State._appState.numCalls} calls received`;
        log(`${EXCLUDE_FROM_COMP_PREFIX}Checkpoint saved: ${roleStateInfo}`);
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
        log(`Successfully upgraded!`); // This should be logged as "V2: Successfully upgraded!"
    }
}
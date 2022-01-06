// Generated consumer-side API for the 'PTI' Ambrosia Node app/service.
// Publisher: Rich (MSR) [richardh@microsoft.com].
// Note: This file was generated on 2021/12/14 at 13:42:27.680.
// Note [to publisher]: You can edit this file, but to avoid losing your changes be sure to specify a 'mergeType' other than 'None' (the default is 'Annotate') when re-running emitTypeScriptFile[FromSource]().
import Ambrosia = require("ambrosia-node");
import { State } from "./PTI";
import IC = Ambrosia.IC;
import Utils = Ambrosia.Utils;

const _knownDestinations: string[] = []; // All previously used destination instances (the 'PTI' Ambrosia app/service can be running on multiple instances, potentially simultaneously); used by the postResultDispatcher (if any)
let _destinationInstanceName: string = ""; // The current destination instance
let _postTimeoutInMs: number = 8000; // -1 = Infinite

/** 
 * Sets the destination instance name that the API targets.\
 * Must be called at least once (with the name of a registered Ambrosia instance that implements the 'PTI' API) before any other method in the API is used.
 */
export function setDestinationInstance(instanceName: string): void
{
    _destinationInstanceName = instanceName.trim();
    if (_destinationInstanceName && (_knownDestinations.indexOf(_destinationInstanceName) === -1))
    {
        _knownDestinations.push(_destinationInstanceName);
    }
}

/** Returns the destination instance name that the API currently targets. */
export function getDestinationInstance(): string
{
    return (_destinationInstanceName);
}

/** Throws if _destinationInstanceName has not been set. */
function checkDestinationSet(): void
{
    if (!_destinationInstanceName)
    {
        throw new Error("setDestinationInstance() must be called to specify the target destination before the 'PTI' API can be used.");
    }
}

/**
 * Sets the post method timeout interval (in milliseconds), which is how long to wait for a post result from the destination instance before raising an error.\
 * All post methods will use this timeout value. Specify -1 for no timeout. 
 */
export function setPostTimeoutInMs(timeoutInMs: number): void
{
    _postTimeoutInMs = Math.max(-1, timeoutInMs);
}

/**
 * Returns the post method timeout interval (in milliseconds), which is how long to wait for a post result from the destination instance before raising an error.\
 * A value of -1 means there is no timeout.
 */
export function getPostTimeoutInMs(): number
{
    return (_postTimeoutInMs);
}

/** Namespace for the "client-side" published methods. */
export namespace ClientAPI
{
    /**
     * *Note: "_Fork" methods should **only** be called from deterministic events.*
     * 
     * Builds a batch of messages (each of size 'numRPCBytes') until the batch contains at least State._appState.batchSizeCutoff bytes. The batch is then sent.\
     * This continues until a total of State._appState.bytesPerRound have been sent.\
     * With the round complete, numRPCBytes is adjusted, then the whole cycle repeats until State._appState.numRoundsLeft reaches 0.
     */
    export function continueSendingMessages_Fork(numRPCBytes: number, iterationWithinRound: number, startTimeOfRound: number): void
    {
        checkDestinationSet();
        IC.callFork(_destinationInstanceName, 4, { numRPCBytes: numRPCBytes, iterationWithinRound: iterationWithinRound, startTimeOfRound: startTimeOfRound });
    }

    /**
     * *Note: "_Impulse" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc).*
     * 
     * Builds a batch of messages (each of size 'numRPCBytes') until the batch contains at least State._appState.batchSizeCutoff bytes. The batch is then sent.\
     * This continues until a total of State._appState.bytesPerRound have been sent.\
     * With the round complete, numRPCBytes is adjusted, then the whole cycle repeats until State._appState.numRoundsLeft reaches 0.
     */
    export function continueSendingMessages_Impulse(numRPCBytes: number, iterationWithinRound: number, startTimeOfRound: number): void
    {
        checkDestinationSet();
        IC.callImpulse(_destinationInstanceName, 4, { numRPCBytes: numRPCBytes, iterationWithinRound: iterationWithinRound, startTimeOfRound: startTimeOfRound });
    }

    /**
     * *Note: "_EnqueueFork" methods should **only** be called from deterministic events, and will not be sent until IC.flushQueue() is called.*
     * 
     * Builds a batch of messages (each of size 'numRPCBytes') until the batch contains at least State._appState.batchSizeCutoff bytes. The batch is then sent.\
     * This continues until a total of State._appState.bytesPerRound have been sent.\
     * With the round complete, numRPCBytes is adjusted, then the whole cycle repeats until State._appState.numRoundsLeft reaches 0.
     */
    export function continueSendingMessages_EnqueueFork(numRPCBytes: number, iterationWithinRound: number, startTimeOfRound: number): void
    {
        checkDestinationSet();
        IC.queueFork(_destinationInstanceName, 4, { numRPCBytes: numRPCBytes, iterationWithinRound: iterationWithinRound, startTimeOfRound: startTimeOfRound });
    }

    /**
     * *Note: "_EnqueueImpulse" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc), and will not be sent until IC.flushQueue() is called.*
     * 
     * Builds a batch of messages (each of size 'numRPCBytes') until the batch contains at least State._appState.batchSizeCutoff bytes. The batch is then sent.\
     * This continues until a total of State._appState.bytesPerRound have been sent.\
     * With the round complete, numRPCBytes is adjusted, then the whole cycle repeats until State._appState.numRoundsLeft reaches 0.
     */
    export function continueSendingMessages_EnqueueImpulse(numRPCBytes: number, iterationWithinRound: number, startTimeOfRound: number): void
    {
        checkDestinationSet();
        IC.queueImpulse(_destinationInstanceName, 4, { numRPCBytes: numRPCBytes, iterationWithinRound: iterationWithinRound, startTimeOfRound: startTimeOfRound });
    }

    /**
     * *Note: "_Fork" methods should **only** be called from deterministic events.*
     * 
     * A client method whose purpose is simply to be called [by the server] as an "echo" of the doWork() call sent to the server [by the client].
     * @param rawParams A custom serialized byte array of all method parameters.
     */
    export function doWorkEcho_Fork(rawParams: Uint8Array): void
    {
        checkDestinationSet();
        IC.callFork(_destinationInstanceName, 5, rawParams);
    }

    /**
     * *Note: "_Impulse" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc).*
     * 
     * A client method whose purpose is simply to be called [by the server] as an "echo" of the doWork() call sent to the server [by the client].
     * @param rawParams A custom serialized byte array of all method parameters.
     */
    export function doWorkEcho_Impulse(rawParams: Uint8Array): void
    {
        checkDestinationSet();
        IC.callImpulse(_destinationInstanceName, 5, rawParams);
    }

    /**
     * *Note: "_EnqueueFork" methods should **only** be called from deterministic events, and will not be sent until IC.flushQueue() is called.*
     * 
     * A client method whose purpose is simply to be called [by the server] as an "echo" of the doWork() call sent to the server [by the client].
     * @param rawParams A custom serialized byte array of all method parameters.
     */
    export function doWorkEcho_EnqueueFork(rawParams: Uint8Array): void
    {
        checkDestinationSet();
        IC.queueFork(_destinationInstanceName, 5, rawParams);
    }

    /**
     * *Note: "_EnqueueImpulse" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc), and will not be sent until IC.flushQueue() is called.*
     * 
     * A client method whose purpose is simply to be called [by the server] as an "echo" of the doWork() call sent to the server [by the client].
     * @param rawParams A custom serialized byte array of all method parameters.
     */
    export function doWorkEcho_EnqueueImpulse(rawParams: Uint8Array): void
    {
        checkDestinationSet();
        IC.queueImpulse(_destinationInstanceName, 5, rawParams);
    }
}

/** Namespace for the "server-side" published methods. */
export namespace ServerAPI
{
    /**
     * *Note: "_Fork" methods should **only** be called from deterministic events.*
     * 
     * A method whose purpose is to update _appState with each call.
     * @param rawParams A custom serialized byte array of all method parameters.
     */
    export function doWork_Fork(rawParams: Uint8Array): void
    {
        checkDestinationSet();
        IC.callFork(_destinationInstanceName, 1, rawParams);
    }

    /**
     * *Note: "_Impulse" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc).*
     * 
     * A method whose purpose is to update _appState with each call.
     * @param rawParams A custom serialized byte array of all method parameters.
     */
    export function doWork_Impulse(rawParams: Uint8Array): void
    {
        checkDestinationSet();
        IC.callImpulse(_destinationInstanceName, 1, rawParams);
    }

    /**
     * *Note: "_EnqueueFork" methods should **only** be called from deterministic events, and will not be sent until IC.flushQueue() is called.*
     * 
     * A method whose purpose is to update _appState with each call.
     * @param rawParams A custom serialized byte array of all method parameters.
     */
    export function doWork_EnqueueFork(rawParams: Uint8Array): void
    {
        checkDestinationSet();
        IC.queueFork(_destinationInstanceName, 1, rawParams);
    }

    /**
     * *Note: "_EnqueueImpulse" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc), and will not be sent until IC.flushQueue() is called.*
     * 
     * A method whose purpose is to update _appState with each call.
     * @param rawParams A custom serialized byte array of all method parameters.
     */
    export function doWork_EnqueueImpulse(rawParams: Uint8Array): void
    {
        checkDestinationSet();
        IC.queueImpulse(_destinationInstanceName, 1, rawParams);
    }

    /**
     * *Note: "_Fork" methods should **only** be called from deterministic events.*
     * 
     * A method that reports (to the console) the current application state.
     */
    export function reportState_Fork(isFinalState: boolean): void
    {
        checkDestinationSet();
        IC.callFork(_destinationInstanceName, 2, { isFinalState: isFinalState });
    }

    /**
     * *Note: "_Impulse" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc).*
     * 
     * A method that reports (to the console) the current application state.
     */
    export function reportState_Impulse(isFinalState: boolean): void
    {
        checkDestinationSet();
        IC.callImpulse(_destinationInstanceName, 2, { isFinalState: isFinalState });
    }

    /**
     * *Note: "_EnqueueFork" methods should **only** be called from deterministic events, and will not be sent until IC.flushQueue() is called.*
     * 
     * A method that reports (to the console) the current application state.
     */
    export function reportState_EnqueueFork(isFinalState: boolean): void
    {
        checkDestinationSet();
        IC.queueFork(_destinationInstanceName, 2, { isFinalState: isFinalState });
    }

    /**
     * *Note: "_EnqueueImpulse" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc), and will not be sent until IC.flushQueue() is called.*
     * 
     * A method that reports (to the console) the current application state.
     */
    export function reportState_EnqueueImpulse(isFinalState: boolean): void
    {
        checkDestinationSet();
        IC.queueImpulse(_destinationInstanceName, 2, { isFinalState: isFinalState });
    }

    /**
     * *Note: "_Fork" methods should **only** be called from deterministic events.*
     * 
     * A method whose purpose is [mainly] to be a rapidly occurring Impulse method to test if this causes issues for recovery.\
     * Also periodically (eg. every ~5 seconds) reports that the Server is still running.
     */
    export function checkHealth_Fork(currentTime: number): void
    {
        checkDestinationSet();
        IC.callFork(_destinationInstanceName, 3, { currentTime: currentTime });
    }

    /**
     * *Note: "_Impulse" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc).*
     * 
     * A method whose purpose is [mainly] to be a rapidly occurring Impulse method to test if this causes issues for recovery.\
     * Also periodically (eg. every ~5 seconds) reports that the Server is still running.
     */
    export function checkHealth_Impulse(currentTime: number): void
    {
        checkDestinationSet();
        IC.callImpulse(_destinationInstanceName, 3, { currentTime: currentTime });
    }

    /**
     * *Note: "_EnqueueFork" methods should **only** be called from deterministic events, and will not be sent until IC.flushQueue() is called.*
     * 
     * A method whose purpose is [mainly] to be a rapidly occurring Impulse method to test if this causes issues for recovery.\
     * Also periodically (eg. every ~5 seconds) reports that the Server is still running.
     */
    export function checkHealth_EnqueueFork(currentTime: number): void
    {
        checkDestinationSet();
        IC.queueFork(_destinationInstanceName, 3, { currentTime: currentTime });
    }

    /**
     * *Note: "_EnqueueImpulse" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc), and will not be sent until IC.flushQueue() is called.*
     * 
     * A method whose purpose is [mainly] to be a rapidly occurring Impulse method to test if this causes issues for recovery.\
     * Also periodically (eg. every ~5 seconds) reports that the Server is still running.
     */
    export function checkHealth_EnqueueImpulse(currentTime: number): void
    {
        checkDestinationSet();
        IC.queueImpulse(_destinationInstanceName, 3, { currentTime: currentTime });
    }

    /**
     * *Note: The result (number) produced by this post method is received via the PostResultDispatcher provided to IC.start(). Returns the post method callID.*
     * 
     * *Note: "_Post" methods should **only** be called from deterministic events.*
     * 
     * A simple post method. Returns the supplied value incremented by 1.
     */
    export function incrementValue_Post(callContextData: any, value: number): number
    {
        checkDestinationSet();
        const callID = IC.postFork(_destinationInstanceName, "incrementValue", 1, _postTimeoutInMs, callContextData, IC.arg("value", value));
        return (callID);
    }

    /**
     * *Note: The result (number) produced by this post method is received via the PostResultDispatcher provided to IC.start().*
     * 
     * *Note: "_PostByImpulse" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc).*
     * 
     * A simple post method. Returns the supplied value incremented by 1.
     */
    export function incrementValue_PostByImpulse(callContextData: any, value: number): void
    {
        checkDestinationSet();
        IC.postByImpulse(_destinationInstanceName, "incrementValue", 1, _postTimeoutInMs, callContextData, IC.arg("value", value));
    }
}

/**
 * Handler for the results of previously called post methods (in Ambrosia, only 'post' methods return values). See Messages.PostResultDispatcher.\
 * Must return true only if the result (or error) was handled.
 */
export function postResultDispatcher(senderInstanceName: string, methodName: string, methodVersion: number, callID: number, callContextData: any, result: any, errorMsg: string): boolean
{
    const sender: string = IC.isSelf(senderInstanceName) ? "local" : `'${senderInstanceName}'`;
    let handled: boolean = true;

    if (_knownDestinations.indexOf(senderInstanceName) === -1)
    {
        return (false); // Not handled: this post result is from a different instance than the destination instance currently (or previously) targeted by the 'PTI' API
    }

    if (errorMsg)
    {
        switch (methodName)
        {
            case "incrementValue":
                Utils.log(`Error: ${errorMsg}`);
                break;
            default:
                handled = false;
                break;
        }
    }
    else
    {
        switch (methodName)
        {
            case "incrementValue":
                const incrementValue_Result: number = result;
                const expectedValue: number = callContextData;
                if (incrementValue_Result !== expectedValue)
                {
                    throw new Error(`The result of post method '${methodName}' (from ${sender}) is incorrect: expected ${expectedValue} but got ${incrementValue_Result}`);
                }
                if (incrementValue_Result !== State._appState.lastPostMethodResultValue + 1)
                {
                    throw new Error(`The result of post method '${methodName}' (from ${sender}) is out of order: expected ${State._appState.lastPostMethodResultValue + 1} but got ${incrementValue_Result}`);
                }
                State._appState.postMethodResultCount++;
                State._appState.lastPostMethodResultValue = incrementValue_Result;
                break;
            default:
                handled = false;
                break;
        }
    }
    return (handled);
}
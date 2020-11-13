// Module for interacting with the Immortal Coordinator.
import Stream = require("stream");
import Process = require("process");
import File = require("fs");
import ChildProcess = require("child_process");
import Path = require("path");
import Net = require("net");
import { EventEmitter } from "events";
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as Configuration from "./Configuration";
import * as Messages from "./Messages";
import * as Meta from "./Meta";
import * as Root from "./AmbrosiaRoot";
import * as Streams from "./Streams";
import * as Utils from "./Utils/Utils-Index";

/** Type of a failed Promise. */
export type RejectedPromise = (error: Error) => void;

export const POST_METHOD_ID: number = -1;

/** 
 * Type of the handler method that's invoked when the result (or error) of a 'post' method is returned.\
 * **WARNING**: To ensure replay integrity, a PostResultHandler should only use state that comes from one or more of these sources:
 * 1) Checkpointed application state.
 * 2) Post method arguments.
 * 3) Non-deterministic values (eg. Date.now()) that have been "determinized" using replayableValue()/replayableValueAsync().
 * 4) Runtime state that is repeatably deterministic, ie. that will be identical during both real-time and replay.
 */
export type PostResultHandler<T> = (result?: T, errorMsg?: string) => void;

/** A named argument for a post method call. */
export interface PostMethodArg { argName: string, argValue: any }

/** 
 * Creates a named argument for a post method call.\
 * An optional argument is indicated by the supplied 'name' ending with '?'.\
 * Note: Returns 'undefined' if 'value' is 'undefined'.
 */
export function arg(name: string, value: any): PostMethodArg
{
    if (value === undefined) // This can happen when using the TS wrapper functions produced by Meta.emitTypeScriptFile() [when optional PostMethodArg values are omitted in the call to the wrapper]
    {
        return (undefined); // This 'PostMethodArg' will get filtered out by IC.post()/IC.postAsync()
    }
    Meta.checkName(name, "method argument");
    return ({ argName: name, argValue: value });
}

/** 
 * [Re]establishes Ambrosia's internal state from the supplied application state.\
 * **Must** be called immediately after receiving (restoring) a checkpoint. 
 */
export function initializeAmbrosiaState(appState: Root.AmbrosiaAppState)
{
    // Runtime type check
    if ((appState.__ambrosiaInternalState === undefined) || ((appState.__ambrosiaInternalState !== null) && (appState.__ambrosiaInternalState["_lastCallID"] === undefined)))
    {
        throw new Error("The supplied 'appState' does not derive from AmbrosiaAppState");
    }
    if (appState.__ambrosiaInternalState === null)
    {
        throw new Error("The supplied 'appState' has a null __ambrosiaInternalState; this can indicate possible app state corruption");
    }
    _ambrosiaInternalState = new Root.AmbrosiaInternalState(appState.__ambrosiaInternalState);
}

/** A singleton-instanced class that provides methods for tunneling RPC calls through the [built-in] 'post' method. */
class Poster extends EventEmitter
{
    public static METHOD_PARAMETER_PREFIX: string = "arg:"; // Used to distinguish actual method arguments from internal arguments (like "senderInstanceName")
    private static METHOD_RESULT_SUFFIX: string = "_Result";
    private static METHOD_RESULT_READY_SUFFIX: string = "_ResultReady";
    private static UNDEFINED_RETURN_VALUE: string = "__UNDEFINED__";
    private static _poster: Poster = null; // The singleton instance

    // Private because to create a [singleton] Poster instance the createPoster() method should be used
    private constructor()
    {
        super();
    }

    /** Returns the next 'post' call ID. */
    private nextCallID()
    {
        if (!_ambrosiaInternalState)
        {
            throw new Error("_ambrosiaInternalState not set; IC.initializeAmbrosiaState() may not have been called immediately after receiving a checkpoint");
        }
        return (_ambrosiaInternalState.getNextCallID());
    }

    /** Creates the singleton instance of the Poster class. */
    static createPoster(): Poster
    {
        if (this._poster === null)
        {
            this._poster = new Poster();
        }
        return (this._poster);
    }

    /** Returns the 'method result ready' event name for the specified method. */
    private resultReadyEventName(methodName: string, callID: number)
    {
        let baseMethodName: string = methodName.endsWith(Poster.METHOD_RESULT_SUFFIX) ? methodName.slice(0, -Poster.METHOD_RESULT_SUFFIX.length) : methodName;
        return (`${baseMethodName}_${callID}${Poster.METHOD_RESULT_READY_SUFFIX}`);
    }

    /** 
     * Creates a wrapper around the supplied dispatcher to intercept post method result RPCs, handle "built-in" post methods, check if 
     * the post method/version is published, and - optionally - check the parameters (name/type) of a [non built-in] post method call. 
     */
    wrapDispatcher(dispatcher: Messages.MessageDispatcher): Messages.MessageDispatcher
    {
        let postInterceptDispatcher = function(message: Messages.DispatchedMessage)
        {
            if (message.type === Messages.DispatchedMessageType.RPC)
            {
                let rpc: Messages.IncomingRPC = message as Messages.IncomingRPC;
                let expandTypes: boolean = false;
                let attrs: string = "";

                if (rpc.methodID === POST_METHOD_ID)
                {
                    // Handle post method results
                    if (_poster.isPostResult(rpc))
                    {
                        // isPostResult() will have invoked the result handler, so we're done
                        return;
                    }

                    // Handle "built-in" post methods (Note: These methods are NOT published)
                    switch (getPostMethodName(rpc))
                    {
                        case "_getPublishedMethods":
                            expandTypes = getPostMethodArg(rpc, "expandTypes");
                            let includePostMethodsOnly: boolean = getPostMethodArg(rpc, "includePostMethodsOnly");
                            let methodListXml: string = Meta.getPublishedMethodsXml(expandTypes, includePostMethodsOnly);
                            attrs = `fromInstance="${_config.icInstanceName}" expandedTypes="${expandTypes}"`;
                            postResult<string>(rpc, (methodListXml.length === 0) ? `<Methods ${attrs}/>` : `<Methods ${attrs}>${methodListXml}</Methods>`);
                            return;

                        case "_getPublishedTypes":
                            expandTypes = getPostMethodArg(rpc, "expandTypes");
                            let typeListXml: string = Meta.getPublishedTypesXml(expandTypes);
                            attrs = `fromInstance="${_config.icInstanceName}" expandedTypes="${expandTypes}"`;
                            postResult<string>(rpc, (typeListXml.length === 0) ? `<Types ${attrs}/>` : `<Types ${attrs}>${typeListXml}</Types>`);
                            return;

                        case "_isPublishedMethod":
                            let methodName: string = getPostMethodArg(rpc, "methodName");
                            let methodVersion: number = getPostMethodArg(rpc, "methodVersion");
                            let isPublished: boolean = (Meta.getPublishedMethod(methodName, methodVersion) !== null);
                            postResult<boolean>(rpc, isPublished);
                            return;

                        case "_echo":
                            postResult(rpc, getPostMethodArg(rpc, "payload")); // Note: The <T> value for postResult() isn't known in this case
                            return;
                    }

                    // Handle the case where the post method/version has not been published (or the supplied parameters don't match the published parameters)
                    // Note: We can't do the same for non-post methods [by checking for the methodID] because non-post methods don't have an error-return capability
                    let methodName: string = getPostMethodName(rpc);
                    let methodVersion: number = getPostMethodVersion(rpc);
                    let isPublished: boolean = Meta.isPublishedMethod(methodName);
                    let isSupportedVersion: boolean = isPublished && (Meta.getPublishedMethod(methodName, methodVersion) !== null);

                    if (isSupportedVersion)
                    {
                        let method: Meta.Method = Meta.getPublishedMethod(methodName, methodVersion);
                        let unknownArgNames: string[] = Object.keys(rpc.jsonParams)
                            .filter(key => key.startsWith(Poster.METHOD_PARAMETER_PREFIX) && (method.parameterNames.indexOf(key.replace(Poster.METHOD_PARAMETER_PREFIX, "")) === -1))
                            .map(key => key.replace(Poster.METHOD_PARAMETER_PREFIX, ""));

                        if (unknownArgNames.length > 0)
                        {
                            Utils.log(`Warning: Instance '${getPostMethodSender(rpc)}' supplied ${unknownArgNames.length} unexpected arguments (${unknownArgNames.join(", ")}) for post method '${methodName}'`);
                            postError(rpc, new Error(`${unknownArgNames.length} unexpected arguments (${unknownArgNames.join(", ")}) were supplied`));
                            return;
                        }

                        // Check that all the required published parameters have been supplied, and are of the correct type
                        for (let i = 0; i < method.parameterNames.length; i++)
                        {
                            let paramName: string = method.parameterNames[i];
                            let paramType: string = method.parameterTypes[i];
                            let incomingParamName: string = Poster.METHOD_PARAMETER_PREFIX + paramName;
                            let incomingParamValue: any = rpc.jsonParams[incomingParamName];

                            if (!paramName.endsWith("?") && (incomingParamValue === undefined))
                            {
                                Utils.log(`Warning: Instance '${getPostMethodSender(rpc)}' did not supply required argument '${paramName}' for post method '${methodName}'`);
                                postError(rpc, new Error(`Required argument '${paramName}' was not supplied (argument type: ${paramType})`));
                                return;
                            }

                            // Check that the type of the published/supplied parameters match
                            if (_config.lbOptions.typeCheckIncomingPostMethodParameters && method.isTypeChecked && (paramType !== "any"))
                            {
                                let incomingParamType: string = Meta.Type.getRuntimeType(incomingParamValue); // May return null
                                if (incomingParamType)
                                {
                                    let isComplexType: boolean = (incomingParamType[0] === "{");
                                    let failureReason: string = "";

                                    if (isComplexType)
                                    {
                                        failureReason = Meta.Type.compareComplexTypes(incomingParamType, Meta.getPublishedType(paramType)?.expandedDefinition ?? paramType);
                                    }
                                    else
                                    {
                                        // Note: Simple types can be published too (eg. Enums)
                                        let expectedType: string = Meta.getPublishedType(paramType)?.definition ?? paramType;
                                        if (incomingParamType !== expectedType)
                                        {
                                            failureReason = `expected ${expectedType}`;
                                        }
                                    }

                                    if (failureReason)
                                    {
                                        Utils.log(`Warning: Instance '${getPostMethodSender(rpc)}' sent a parameter ('${paramName}') of the wrong type (${incomingParamType}) for post method '${methodName}'; ${failureReason}`);
                                        postError(rpc, new Error(`Argument '${paramName}' is of the wrong type (${incomingParamType}); ${failureReason}`));
                                        return;
                                    }
                                }
                            }
                        }
                    }
                    if (!isPublished)
                    {
                        Utils.log(`Warning: Instance '${getPostMethodSender(rpc)}' requested a non-published post method '${methodName}'`);
                        postError(rpc, new Error(`The method is not published`));
                        return;
                    }
                    if (!isSupportedVersion)
                    {
                        Utils.log(`Warning: Instance '${getPostMethodSender(rpc)}' requested a non-published version (${methodVersion}) of post method '${methodName}'`);
                        postError(rpc, new Error(`The requested version (${methodVersion}) of the method is not published`));
                        return;
                    }
                }
            }
            // Call the app-provided dispatcher. 
            // Note: We cannot know if this is an "async" function, but even if we knew that it was we would NOT await it because a) there is no 'result' we
            //       need from it, and b) it may take arbitrarily long to complete. So waiting a [potentially] long time to then do nothing is not useful.
            // Note: The VSCode profiler may report that all the functions in the app's dispatcher are actually being called by postInterceptDispatcher(). 
            //       The current thinking for why this happens is because the V8 compiler is doing some sort of tail-call optimization.
            //       A workaround to get a more "reasonable" function name (eg. "wrappedDispatcher") recorded by the profiler is to do this:
            //           function wrappedDispatcher()
            //           {
            //               dispatcher(message);
            //           }
            //           wrappedDispatcher();
            //       However, this is a performance issue as we're putting another "needless" frame on the stack.
            //       The better workaround is for the user to modify their dispatcher method (if they so desire) to get a "full fidelity" stack when profiled,
            //       eg. by making a nested function call to the "real" dispatcher.
            dispatcher(message);
        }
        return (postInterceptDispatcher);
    }

    /** An awaitable wrapper for Poster.post(). The [optional] 'errorTemplate' uses "$[ERROR]" as the replacement token for the actual error message (if any). */
    postAsync<T>(destinationInstance: string, methodName: string, methodVersion: number, errorTemplate: string = null, resultTimeoutInMs: number = -1, ...methodArgs: PostMethodArg[]): Promise<T>
    {
        let promise: Promise<T> = new Promise<T>((resolve, reject: RejectedPromise) =>
        {
            this.post(destinationInstance, methodName, methodVersion, (result?: T, errorMsg?: string) => 
            {
                if (errorMsg)
                {
                    let formattedError: string = (errorTemplate && (errorTemplate.indexOf("$[ERROR]") !== -1)) ? errorTemplate.replace("$[ERROR]", errorMsg) : errorMsg;
                    reject(new Error(formattedError));
                }
                else
                {
                    resolve(result);
                }
            },
            resultTimeoutInMs, ...methodArgs);
        });
        return (promise);
    }

    /** 
     * Posts an RPC message. The receiver will examine the 'methodName' parameter of the IncomingRPC.jsonParams to 
     * decide which method to invoke. To get the result of the method, specify a resultHandler.\
     * **WARNING**: To ensure replay integrity, pay careful attention to the restrictions on the PostResultHandler.
     */
    post<T>(destinationInstance: string, methodName: string, methodVersion: number, resultHandler: PostResultHandler<T> = null, resultTimeoutInMs: number = -1, ...methodArgs: PostMethodArg[]): void
    {
        let message: Uint8Array = null;
        let returnResult: boolean = !!resultHandler;
        let jsonArgs: object = {};
        let callID: number = this.nextCallID(); // This will ensure that we'll associate the posted result (if any) with the correct resultHandler

        Utils.log(`Posting [Fork] method '${methodName}' (version ${methodVersion}) to ${isSelf(destinationInstance) ? "local" : `'${destinationInstance}'`} IC`);

        if (methodName.endsWith(Poster.METHOD_RESULT_SUFFIX))
        {
            throw new Error(`Invalid methodName '${methodName}': the name cannot end with '${Poster.METHOD_RESULT_SUFFIX}'`);
        }

        jsonArgs["senderInstanceName"] = _config.icInstanceName;
        jsonArgs["methodName"] = methodName;
        jsonArgs["methodVersion"] = methodVersion;
        jsonArgs["returnResult"] = returnResult; // Tells the destination to post("methodName_Result") back to us [although calling post() with a null resultHandler is effectively the same as callFork()]
        if (returnResult)
        {
            jsonArgs["callID"] = callID;
        }

        for (let i = 0; i < methodArgs.length; i++)
        {
            jsonArgs[`${Poster.METHOD_PARAMETER_PREFIX}${methodArgs[i].argName}`] = methodArgs[i].argValue; // For example: jsonArgs["arg:digits?"] = 5
        }

        message = Messages.makeRpcMessage(Messages.RPCType.Fork, destinationInstance, POST_METHOD_ID, jsonArgs);
        sendMessage(message, Messages.MessageType.RPC, destinationInstance);
        
        if (returnResult)
        {
            // Note: The event will get raised by isPostResult()
            let eventName: string = this.resultReadyEventName(methodName, callID);
            this.once(eventName, resultHandler);
            
            // Note: We keep track of all in-flight post methods, even if they have an infinite timeout, so that we can [optionally] detect unresponsive destination instances
            _inFlightPostMethods[callID] = new InFlightPostMethodDetails(callID, methodName, resultTimeoutInMs, destinationInstance, message);
            this.startPostResultTimeout(callID, _inFlightPostMethods[callID]);

            // Detect/report unresponsive destination instances
            let inFlightPostMessageCount: number = Object.keys(_inFlightPostMethods).length;
            if ((_config.lbOptions.maxInFlightPostMethods !== -1) && ((inFlightPostMessageCount % Math.max(1, _config.lbOptions.maxInFlightPostMethods)) === 0) && !Messages.isRecoveryRunning())
            {
                let destinationTotals: { [destinationInstance: string]: number } = {};
                let totalsList: string = "";

                for (const key of Object.keys(_inFlightPostMethods))
                {
                    let callID: number = parseInt(key);
                    let methodDetails: InFlightPostMethodDetails = _inFlightPostMethods[callID];
                    if (destinationTotals[methodDetails.destinationInstance] === undefined)
                    {
                        destinationTotals[methodDetails.destinationInstance] = 0;
                    }
                    destinationTotals[methodDetails.destinationInstance]++;
                }
                for (const destination of Object.keys(destinationTotals))
                {
                    totalsList += (totalsList.length === 0 ? "" : ", ") + `${destination} = ${destinationTotals[destination]}`;
                }
                Utils.log(`Warning: There are ${inFlightPostMessageCount} in-flight post methods (${totalsList})`, null, Utils.LoggingLevel.Minimal);
            }
        }
    }

    /** Starts the result timeout (if needed) for the supplied post method call. */
    startPostResultTimeout(callID: number, methodDetails: InFlightPostMethodDetails)
    {
        if ((methodDetails.resultTimeoutInMs !== -1) && _config.lbOptions.allowPostMethodTimeouts && !Messages.isRecoveryRunning())
        {
            let eventName: string = this.resultReadyEventName(methodDetails.methodName, callID);

            // Note: We don't start the timer during recovery because the outcome [for all but the in-flight posts] is already in the log.
            //       Timeouts will be started for any in-flight (ie. sent but no response yet received) post methods by onRecoveryComplete().
            setTimeout(() =>
            {
                // If we haven't yet received the result, self-post a timeout error result.
                // Note: We can't simply do "this.emit(eventName, null, timeoutError)" due to recovery issues. Namely, if the timeout occurred when run in realtime 
                //       but the actual result was received after the timeout, then during recovery the actual result will happen BEFORE the timeout due to playback
                //       time compression. But if we explicitly post the error then during playback the timeout result will correctly precede the actual result (if 
                //       the actual result ever arrives), faithfully replaying what happened in realtime.
                // Note: This approach can lead to BOTH the timeout result AND the actual result being received, since the timeout does not cancel the sending of the 
                //       actual result. Further, the test below can succeed after the actual result has been sent but before it has been received.
                if (this.listenerCount(eventName) === 1)
                {
                    let timeoutError: string = `The result for method '${methodDetails.methodName}' from the ${isSelf(methodDetails.destinationInstance) ? "local" : `'${methodDetails.destinationInstance}'`} IC did not return after ${methodDetails.resultTimeoutInMs}ms`;
                    let incomingRpc: Messages.IncomingRPC = Messages.makeIncomingRpcFromOutgoingRpc(methodDetails.outgoingRpc);
                    // Note: We post the timeout error as an Impulse because it's occurring as the result of a timer which won't run during recovery
                    postImpulseError(incomingRpc, new Error(timeoutError));
                }
            }, methodDetails.resultTimeoutInMs);
        }
    }

    /** An awaitable wrapper for Poster.postResult(). */
    postResultAsync<T>(rpc: Messages.IncomingRPC, responseRpcType: Messages.RPCType = Messages.RPCType.Fork, result?: T | Error): Promise<boolean>
    {
        let promise: Promise<boolean> = new Promise<boolean>((resolve, reject: RejectedPromise) =>
        {
            try
            {
                resolve(this.postResult<T>(rpc, responseRpcType, result));
            }
            catch (error)
            {
                reject(error);
            }
        });
        return (promise);
    }

    /** 
     * Posts the result of a method back to the sender, but only if the sender requested the result [by providing a PostResultHandler when they called post()]. 
     * If the method returned void, the 'result' can be omitted. Returns false if the RPC did not request the result/error to be returned.
     */
    postResult<T>(rpc: Messages.IncomingRPC, responseRpcType: Messages.RPCType = Messages.RPCType.Fork, result?: T | Error): boolean
    {
        let returnResult: boolean = (rpc.jsonParams["returnResult"] === true);
        
        if (!returnResult)
        {
            return (false);
        }

        let methodName: string = rpc.jsonParams["methodName"];
        let destinationInstance: string = rpc.jsonParams["senderInstanceName"];
        let message: Uint8Array = null;
        let jsonArgs: object = {};

        jsonArgs["senderInstanceName"] = _config.icInstanceName;
        jsonArgs["methodName"] = methodName + Poster.METHOD_RESULT_SUFFIX;
        jsonArgs["methodVersion"] = rpc.jsonParams["methodVersion"]; // We just echo this back to the caller
        jsonArgs["callID"] = rpc.jsonParams["callID"]; // We just echo this back to the caller
        
        if (result instanceof Error)
        {
            // Note: The Error object doesn't have a toJSON() member, so JSON.stringify() returns "{}".
            //       As a workaround we only include Error.message, which will serialize.
            jsonArgs["errorMsg"] = `Post method '${methodName}' [executed on '${_config.icInstanceName}'] failed (reason: ${result.message})`;
            if (_config.lbOptions.allowPostMethodErrorStacks)
            {
                // Note: This is expensive [in # bytes] and may pose a security risk, so whether to send it is configurable (similar to <customErrors mode="Off"/> in IIS)
                jsonArgs["originalError"] = result.stack;
            }
        }
        else
        {
            // Using UNDEFINED_RETURN_VALUE is to support the case of the caller just wanting to know if a method [that returns void] completes (or fails)
            jsonArgs["result"] = (result === undefined) ? Poster.UNDEFINED_RETURN_VALUE : result; // 'undefined' won't pass through JSON.stringify() [null is OK]
        }

        Utils.log(`Posting [Fork] ${result instanceof Error ? "error for" : "result of"} method '${methodName}' to ${isSelf(destinationInstance) ? "local" : `'${destinationInstance}'`} IC`);

        // Note: responseRpcType will only be 'Impulse' when called from the [internal-only] postImpulseError() method.
        message = Messages.makeRpcMessage(responseRpcType, destinationInstance, POST_METHOD_ID, jsonArgs);
        sendMessage(message, Messages.MessageType.RPC, destinationInstance);
        return (true);
    }

    /** 
     * Returns true if the supplied RPC is the posted return value of a posted method.\
     * If the RPC is a post method return value, also invokes the PostResultHandler passed to the originating post().
     */
    private isPostResult(rpc: Messages.IncomingRPC): boolean
    {
        if (rpc.methodID === POST_METHOD_ID)
        {
            let methodName: string = rpc.jsonParams["methodName"];

            if (methodName.endsWith(Poster.METHOD_RESULT_SUFFIX) && ((rpc.jsonParams["result"] !== undefined) || (rpc.jsonParams["errorMsg"] !== undefined)))
            {
                let result: any = rpc.jsonParams["result"];
                let errorMsg: string = rpc.jsonParams["errorMsg"];
                let originalError: string = rpc.jsonParams["originalError"]; // Note: The source instance can be configured not to send this [which is the default behavior]
                let callID: number = rpc.jsonParams["callID"];

                // Handle the void-return case [which is just notification of method completion]; receiving an 'undefined' result can also indicate a potential error in the sender's method handler code
                if (result === Poster.UNDEFINED_RETURN_VALUE) // 'undefined' won't pass through JSON.stringify()
                {
                    result = undefined;
                }

                let showParams: boolean = _config.lbOptions.verboseOutputLogging; // For debugging
                Utils.log(`Intercepted ${Messages.RPCType[rpc.rpcType]} RPC call for post method '${methodName}' [resultType: ${ errorMsg ? "error" : "normal" }]` + (showParams ? ` with params: ${rpc.makeDisplayParams()}`: ""));

                if (errorMsg && originalError)
                {
                    Utils.log(`Originating error (on publisher ['${rpc.jsonParams["senderInstanceName"]}'] of post method '${methodName.replace(Poster.METHOD_RESULT_SUFFIX, "")}'):`);
                    Utils.log(originalError);
                }

                // Invoke the PostResultHandler passed to post()
                let eventName: string = this.resultReadyEventName(methodName, callID);
                if (!this.emit(eventName, result, errorMsg)) // The order/type of parameters we pass here MUST match the signature of PostResultHandler
                {
                    // This can legitimately happen when either:
                    // a) A timeout occurred waiting for the post result to arrive [so the continuation will already have been completed by the timeout error]
                    // b) A result for both a timeout error AND the actual result were received [so the first one received will have completed the continuation]
                    Utils.log(`Warning: There is no listener for event '${eventName}'`);
                }
                delete _inFlightPostMethods[callID];
                return (true);
            }
        }
        return (false);
    }
}

let _ambrosiaInternalState: Root.AmbrosiaInternalState = null;
let _config: Configuration.AmbrosiaConfig = null;
let _icProcess: ChildProcess.ChildProcess = null;
let _icSendSocket: Net.Socket = null;
let _icReceiveSocket: Net.Socket = null;
let _outgoingMessageStream: Streams.OutgoingMessageStream = null;
export let _counters : 
{ 
    remoteSentMessageCount: number,
    sentForkMessageCount: number,
    receivedForkMessageCount: number,
    receivedMessageCount: number
};
let _knownDestinationInstanceNames: string[] = [];
let _selfConnectionCount: number = 0;
let _selfConnectionCountCheckTimer: NodeJS.Timeout = null;
let _selfConnectionCheckTimer: NodeJS.Timeout = null;
let _remoteConnectionCount: number = 0;
let _remoteConnectionCountCheckTimer: NodeJS.Timeout = null;
let _poster: Poster = Poster.createPoster();
let _remoteInstanceNames: string[] = [];
let _inFlightPostMethods: { [callID: number] : InFlightPostMethodDetails } = {}; // A dictionary of post method calls sent but with no response (result or error) yet received

/** [Internal] Initializes the set of remote IC instances that this instance communicates with. This list is used for checking IC startup integrity. */
export function setRemoteInstanceNames(remoteInstanceNames: string[])
{
    _remoteInstanceNames = remoteInstanceNames;
}

/** Class representing an in-flight (ie. sent but no response yet received) post method call. */
class InFlightPostMethodDetails
{
    callID: number;
    methodName: string;
    resultTimeoutInMs: number;
    destinationInstance: string;
    outgoingRpc: Uint8Array;

    constructor(callID: number, methodName: string, resultTimeoutInMs: number, destinationInstance: string, outgoingRpc: Uint8Array)
    {
        this.callID = callID;
        this.methodName = methodName;
        this.resultTimeoutInMs = resultTimeoutInMs;
        this.destinationInstance = destinationInstance;
        this.outgoingRpc = outgoingRpc;
    }
}

/** Performs house-keeping checks and actions needed when recovery completes. */
export function onRecoveryComplete()
{
    Utils.log(`Recovery complete (Received ${_counters.receivedMessageCount} messages [${_counters.receivedForkMessageCount} Fork messages], sent ${_counters.sentForkMessageCount} Fork messages)`);
    if (_counters.sentForkMessageCount < _counters.receivedForkMessageCount)
    {
        // If the log ONLY contained messages sent to ourself, then at this point we have an error condition, and the Immortal Coordinator process
        // will go into a CPU spin while it waits for the missing messages. However, since (incoming) replayed messages don't include the source
        // (sender) we cannot make this determination. The best we can do is to check if we sent any messages to a remote instance during recovery 
        // and then use that as a proxy for the fact that we likely received messages from instance(s) other than ourself, and - in which case - we 
        // will skip the warning that there *might* be a problem.
        if (_counters.remoteSentMessageCount === 0)
        {
            Utils.log(`Warning: If the log is known to ONLY contain self-messages, then at least ${_counters.receivedForkMessageCount} Fork messages should have ` + 
                      `been sent during replay, not ${_counters.sentForkMessageCount}; ` + 
                      `in this case, this condition indicates an app programming error (making a Fork RPC call when an Impulse RPC call should have been used)`);
        }
    }
    _counters.remoteSentMessageCount = _counters.receivedMessageCount = _counters.receivedForkMessageCount = _counters.sentForkMessageCount = 0;

    // Start result timeouts for any in-flight (ie. sent but no response yet received) post methods
    // Note: Unless the in-flight method was called right at the end of recovery, it is likely that the amount of time we'll actually end up waiting for a
    //       [unresponsive] method to complete will be longer - possibly considerably so - than the requested InFlightPostMethodDetails.resultTimeoutInMs.
    for (const key of Object.keys(_inFlightPostMethods))
    {
        let callID: number = parseInt(key);
        let methodDetails: InFlightPostMethodDetails = _inFlightPostMethods[callID];
        _poster.startPostResultTimeout(callID, methodDetails);
    }
}

/** 
 * Returns true if the IC is ready to handle self-call RPC's. 
 * If the IC is not ready, then the response to a self-call RPC (from the IC) will be delayed.
 */
export function readyForSelfCallRpc(): boolean
{
    return (_selfConnectionCount === 4);
}

/** Returns true if the specified IC instance name matches the local IC instance name. */
export function isSelf(instanceName: string): boolean
{
    return (Utils.equalIgnoringCase(_config.icInstanceName, instanceName));
}

/** 
 * Returns true if the specified destination instance name has not been sent to before [during the current lifetime of the Immortal].
 * Will always return false for the local IC instance name.
 */
export function isNewDestination(destinationInstanceName: string): boolean
{
    let isNew: boolean = true;

    if (isSelf(destinationInstanceName))
    {
        return (false);
    }
    for (let i = 0; i < _knownDestinationInstanceNames.length; i++)
    {
        if (Utils.equalIgnoringCase(destinationInstanceName, _knownDestinationInstanceNames[i]))
        {
            isNew = false;
            break;
        }
    }
    if (isNew)
    {
        _knownDestinationInstanceNames.push(destinationInstanceName);
    }
    return (isNew);
}

let _isReadyForSelfCallRpc: boolean = false;
let _selfCallRpcWarningShown: boolean = false;

function checkReadyForSelfCallRpc(destinationInstance: string): void
{
    if (!_isReadyForSelfCallRpc && isSelf(destinationInstance))
    {
        if (!_selfCallRpcWarningShown && !readyForSelfCallRpc())
        {
            Utils.log("Warning: Local IC not ready to handle self-call RPC's: The response from the IC will be delayed");
            _selfCallRpcWarningShown = true;
        }
        else
        {
            _isReadyForSelfCallRpc = true;
        }
    }
}

/**
 * Calls the specified method ID (as a Fork RPC).\
 * The message for the call will not be sent until the next tick of the event loop.
 */
export function callFork(destinationInstance: string, methodID: number, jsonOrRawArgs: object | Uint8Array): void
{
    checkReadyForSelfCallRpc(destinationInstance);
    if (Utils.canLog(Utils.LoggingLevel.Normal))
    {
        Utils.log(`Calling Fork method (ID ${methodID}) on ${isSelf(destinationInstance) ? "local" : `'${destinationInstance}'`} IC`);
    }
    sendMessage(Messages.makeRpcMessage(Messages.RPCType.Fork, destinationInstance, methodID, jsonOrRawArgs), Messages.MessageType.RPC)
}

/** 
 * Queues, but does not send, a [Fork] call of the specified method ID.\
 * Use 'await flushAsync()' to send all the queued calls.\
 * Note: A callFork() or callImpulse() will also result in the queue being flushed (at the next tick of the event loop).
 */
export function queueFork(destinationInstance: string, methodID: number, jsonOrRawArgs: object | Uint8Array): void
{
    _outgoingMessageStream.queueBytes(Messages.makeRpcMessage(Messages.RPCType.Fork, destinationInstance, methodID, jsonOrRawArgs));
}

/** 
 * Calls the specified method ID (as an Impulse RPC).\
 * The message for the call will not be sent until the next tick of the event loop.
 */
export function callImpulse(destinationInstance: string, methodID: number, jsonOrRawArgs: object | Uint8Array): void
{
    checkReadyForSelfCallRpc(destinationInstance);
    if (Utils.canLog(Utils.LoggingLevel.Normal))
    {
        Utils.log(`Calling Impulse method (ID ${methodID}) on ${isSelf(destinationInstance) ? "local" : `'${destinationInstance}'`} IC`);
    }
    sendMessage(Messages.makeRpcMessage(Messages.RPCType.Impulse, destinationInstance, methodID, jsonOrRawArgs), Messages.MessageType.RPC)
}

/** 
 * Queues, but does not send, a [Impulse] call of the specified method ID.\
 * Use 'await flushAsync()' to send all the queued calls.\
 * Note: A callFork() or callImpulse() will also result in the queue being flushed (at the next tick of the event loop). 
 */
export function queueImpulse(destinationInstance: string, methodID: number, jsonOrRawArgs: object | Uint8Array): void
{
    _outgoingMessageStream.queueBytes(Messages.makeRpcMessage(Messages.RPCType.Impulse, destinationInstance, methodID, jsonOrRawArgs));
}

/** 
 * Calls the specified method via post (as a Fork RPC).\
 * **WARNING**: To ensure replay integrity, pay careful attention to the restrictions on the PostResultHandler.
 */
export function post<T>(destinationInstance: string, methodName: string, methodVersion: number, resultHandler: PostResultHandler<T>, resultTimeoutInMs: number = -1, ...methodArgs: PostMethodArg[]): void
{
    checkReadyForSelfCallRpc(destinationInstance);
    methodArgs = methodArgs.filter(arg => arg !== undefined); // Remove any 'undefined' elements in the array [see arg()]
    _poster.post(destinationInstance, methodName, methodVersion, resultHandler, resultTimeoutInMs, ...methodArgs);
}

/** 
 * An awaitable version of post(). The [optional] 'errorTemplate' uses "$[ERROR]" as the replacement token for the actual error message (if any).\
 * **WARNING**: To ensure replay integrity, the continuation that follows this call must observe the same restrictions as the PostResultHandler of post().
 */
export async function postAsync<T>(destinationInstance: string, methodName: string, methodVersion: number, errorTemplate: string = null, resultTimeoutInMs: number = -1, ...methodArgs: PostMethodArg[]): Promise<T>
{
    checkReadyForSelfCallRpc(destinationInstance);
    methodArgs = methodArgs.filter(arg => arg !== undefined); // Remove any 'undefined' elements in the array [see arg()]
    let promise: Promise<T> = _poster.postAsync<T>(destinationInstance, methodName, methodVersion, errorTemplate, resultTimeoutInMs, ...methodArgs);
    return (promise);
}

/** 
 * Returns (via Fork) the result of a post method [contained in the supplied RPC] to the post-caller, but only if the caller requested the result. 
 * If there is no return value (ie. the method returned void) then 'result' can be omitted, but postResult() should still be called.
 * Returns false if the RPC did not request the result to the returned.
 */
export function postResult<T>(rpc: Messages.IncomingRPC, result?: T): boolean
{
    let resultPosted: boolean = _poster.postResult<T>(rpc, Messages.RPCType.Fork, result);
    return (resultPosted);
}

/** An awaitable version of postResult(). */
export async function postResultAsync<T>(rpc: Messages.IncomingRPC, result?: T): Promise<boolean>
{
    let promise: Promise<boolean> = _poster.postResultAsync<T>(rpc, Messages.RPCType.Fork, result);
    return (promise);
}

/** 
 * Returns (via Fork) an error when attempting to execute a post method [contained in the supplied RPC] to the post-caller, but only if the caller requested the result. 
 * Returns false if the RPC did not request the error to the returned.
 */
export function postError(rpc: Messages.IncomingRPC, error: Error): boolean
{
    let errorPosted: boolean = _poster.postResult<Error>(rpc, Messages.RPCType.Fork, error);
    return (errorPosted);
}

/** An awaitable version of postError(). */
export async function postErrorAsync(rpc: Messages.IncomingRPC, error: Error): Promise<boolean>
{
    let promise: Promise<boolean> = _poster.postResultAsync<Error>(rpc, Messages.RPCType.Fork, error);
    return (promise);
}

/** 
 * [Internal] The Impulse version of postError(). For internal use only.\
 * **WARNING:** Will cause an exception if called during recovery (replay).
 */
function postImpulseError(rpc: Messages.IncomingRPC, error: Error): void
{
    _poster.postResult<Error>(rpc, Messages.RPCType.Impulse, error);
}

/** 
 * Returns the value of the specified method parameter, or throws if the parameter cannot be found (unless the 
 * parameter is optional, as indicated by a trailing "?" in the argName, in which case it will return 'undefined').
 */
export function getPostMethodArg(rpc: Messages.IncomingRPC, argName: string): any
{
    checkIsPostMethod(rpc);
    let isOptionalArg: boolean = argName.trim().endsWith("?");
    let paramName: string = `${Poster.METHOD_PARAMETER_PREFIX}${argName}`;
    if (rpc.jsonParams.hasOwnProperty(paramName))
    {
        return (rpc.jsonParams[paramName]);
    }
    if (!isOptionalArg && rpc.jsonParams.hasOwnProperty(paramName + "?"))
    {
        // The caller didn't specify that the arg was optional, but an optional version of the arg was sent in the RPC, so we use this
        return (rpc.jsonParams[paramName + "?"]);
    }
    if (isOptionalArg)
    { 
        return (undefined);
    }
    throw new Error(`Expected post method parameter '${argName}' to be present, but it was not found in the jsonParams`);
}

/** Returns the name of the (post) method called by the supplied RPC. */
export function getPostMethodName(rpc: Messages.IncomingRPC): string
{
    checkIsPostMethod(rpc);
    return (rpc.jsonParams["methodName"]);
}

/** Returns the version of the (post) method called by the supplied RPC. */
export function getPostMethodVersion(rpc: Messages.IncomingRPC): number
{
    checkIsPostMethod(rpc);
    return (rpc.jsonParams["methodVersion"]);
}

/** Returns the sender (instance name) of the (post) method called by the supplied RPC. */
export function getPostMethodSender(rpc: Messages.IncomingRPC): string
{
    checkIsPostMethod(rpc);
    return (rpc.jsonParams["senderInstanceName"]);
}

/** Throws if the supplied IncomingRPC is not a post method call. */
function checkIsPostMethod(rpc: Messages.IncomingRPC): void
{
    if (rpc.methodID !== POST_METHOD_ID)
    {
        throw new Error(`The supplied RPC (methodID ${rpc.methodID}) is not a 'post' method`);
    }
}

/** A utility method for ensuring that a non-deterministic value, like Date.now() or Math.random(), can be used in a recovery-safe way. */
export function replayableValue<T>(value: T, handler: (value: T, error?: Error) => void): void
{
    post(_config.icInstanceName, "_echo", 1, (result?: T, errorMsg?: string) => 
    {
        if (errorMsg)
        {
            // Note: We use a custom error "template" otherwise the error will only refer to the "_echo" method, which is not obviously related to replayableValue()
            let error: Error = new Error(`replayableValue() failed (reason: ${errorMsg})`);
            handler(undefined, error);
        }
        else
        {
            handler(result);
        }
    }, 
    -1, arg("payload", value));
}

/** An awaitable version of replayableValue(). Ensures that a non-deterministic value, like Date.now() or Math.random(), can be used in a recovery-safe way. */
export async function replayableValueAsync<T>(value: T): Promise<T>
{
    // Note: We use a custom errorTemplate otherwise the error will only refer to the "_echo" method, which is not obviously related to replayableValueAsync()
    let promise: Promise<T> = postAsync<T>(_config.icInstanceName, "_echo", 1, "replayableValueAsync() failed (reason: $[ERROR])", -1, arg("payload", value));
    return (promise);
}

/** 
 * [Internal] Sends a complete [binary] message to an IC. The message will be queued until the next tick of the event loop, but messages will always be sent in chronological order.\
 * Note: The 'messageType' and 'destinationInstance' parameters are for logging purposes only, and do not supercede the values included in the message 'bytes'.
 */
export function sendMessage(bytes: Uint8Array, messageType: Messages.MessageType, destinationInstance: string = _config.icInstanceName, immediateFlush: boolean = false): void
{
    if (Utils.canLog(Utils.LoggingLevel.Normal))
    {
        let messageName: string = Messages.MessageType[messageType];
        let destination: string = isSelf(destinationInstance) ? "local" : `'${destinationInstance}'`;
        let showBytes: boolean = _config.lbOptions.verboseOutputLogging; // For debugging
        Utils.log(`Sending ${messageName ? `'${messageName}' ` : ""}to ${destination} IC (${bytes.length} bytes)` + (showBytes ? `: ${Utils.makeDisplayBytes(bytes)}` : ""));
    }
    _outgoingMessageStream.addBytes(bytes, immediateFlush);
}

/** 
 * [Internal] An asynchronous version of sendMessage().\
 * This can be more responsive than sendMessage() when called in a loop, because it avoids queuing messages until the function running the loop ends. This allows message I/O to interleave.\
 * Note: The 'messageType' and 'destinationInstance' parameters are for logging purposes only, and do not supercede the values included in the message 'bytes'.
 */
export async function sendMessageAsync(bytes: Uint8Array, messageType: Messages.MessageType, destinationInstance: string = _config.icInstanceName): Promise<void>
{
    let promise: Promise<void> = new Promise<void>((resolve, reject: RejectedPromise) =>
    {
        try
        {
            sendMessage(bytes, messageType, destinationInstance, true);
            // We use setImmediate() here to delay scheduling the continuation until AFTER the I/O events generated (queued) by sendMessage().
            // This allows I/O events to interleave (ie. messages can be received while we're sending messages).
            setImmediate(() => resolve());
        }
        catch (error)
        {
            reject(error);
        }
    });
    return (promise);
}

/** 
 * Sends all calls queued with queueFork() / queueImpulse().\
 * Returns the number of messages that were queued. 
 */
// Note: There is no synchronous version of this method because the yield it produces is required to allow
//       the event loop to service the outbound TCP traffic it generates, and to allow I/O to interleave.
export async function flushAsync(): Promise<number>
{
    let promise: Promise<number> = new Promise<number>((resolve, reject: RejectedPromise) =>
    {
        try
        {
            let queueLength: number = _outgoingMessageStream.flushQueue();
            // We use setImmediate() here to delay scheduling the continuation until AFTER the I/O events generated (queued on the event loop) by flushQueue().
            // This allows I/O events to interleave (ie. messages can be received while we're sending messages).
            setImmediate(() => resolve(queueLength));
        }
        catch (error)
        {
            reject(error);
        }
    });
    return (promise);
}

/** Returns the number of messages currently in the [outgoing] message queue. */
export function queueLength(): number
{
    return (_outgoingMessageStream.queueLength);
}

/** Streams [asynchronously] data to the local IC. Any subsequent messages sent via sendMessage() will queue until the stream finishes. */
export function sendStream(byteStream: Stream.Readable, streamLength: number = -1, streamName?: string, onFinished?: (error?: Error) => void): void
{
    streamName = `${streamName ? `'${streamName}' ` : ""}`;
    Utils.log(`Streaming ${streamName}to local IC...`);

    function onSendStreamFinished(error?: Error)
    {
        Utils.log(`Stream ${streamName}${error ? `failed (reason: ${error.message})` : "finished"}`);
        if (onFinished) 
        {
            onFinished(error);
        }
    }

    _outgoingMessageStream.addStreamedBytes(byteStream, streamLength, onSendStreamFinished);
}

/** An awaitable version of sendStream(). */
export async function sendStreamAsync(byteStream: Stream.Readable, streamLength: number = -1, streamName?: string): Promise<void>
{
    let promise: Promise<void> = new Promise<void>((resolve, reject: RejectedPromise) =>
    {
        try
        {
            function onSendStreamFinished(error?: Error)
            {
                error ? reject(error) : resolve();
            }
            sendStream(byteStream, streamLength, streamName, onSendStreamFinished);
        }
        catch (error)
        {
            reject(error);
        }
    });
    return (promise);
}

/** [Internal] Streams (asynchronously) the outgoing checkpoint to the IC. Any subsequent messages sent via sendMessage() will queue until outgoingCheckpoint.onFinished() is called. */
export function sendCheckpoint(outgoingCheckpoint: Streams.OutgoingCheckpoint, onSuccess?: () => void): void
{
    // To prevent app state from being changed (by the app processing incoming messages) while we stream the checkpoint,
    // we pause the inbound socket from the IC until the stream is finished. This way the app programmer doesn't have to
    // worry about preventing app state changes [due to incoming messages] during production/streaming of a checkpoint.
    _icReceiveSocket.pause();
    function onSendCheckpointFinished(error?: Error): void
    {
        _icReceiveSocket.resume();
        if (outgoingCheckpoint.onFinished)
        {
            outgoingCheckpoint.onFinished(error);
        }
        if (!error && onSuccess)
        {
            onSuccess();
        }
    }
    sendStream(outgoingCheckpoint.dataStream, outgoingCheckpoint.length, `CheckpointDataStream (${outgoingCheckpoint.length} bytes)`, onSendCheckpointFinished);
}

/** [Internal] An awaitable version of sendCheckpoint(). The outgoingCheckpoint.onFinished must be null; use the continuation instead. */
// TODO: This needs more testing.
export async function sendCheckpointAsync(outgoingCheckpoint: Streams.OutgoingCheckpoint): Promise<void>
{
    let promise: Promise<void> = new Promise<void>((resolve, reject: RejectedPromise) =>
    {
        try
        {
            if (outgoingCheckpoint.onFinished)
            {
                throw new Error("The outgoingCheckpoint.onFinished must be null when used with sendCheckpointAsync(); use the continuation instead");
            }
            outgoingCheckpoint.onFinished = function onCheckpointFinished(error?: Error) 
            { 
                error ? reject(error) : resolve(); 
            };
            sendCheckpoint(outgoingCheckpoint);
        }
        catch (error)
        {
            reject(error);
        }
    });
    return (promise);
}

/** Starts the Immortal Coordinator process. */
export function start(config: Configuration.AmbrosiaConfig, appState: Root.AmbrosiaAppState): ChildProcess.ChildProcess
{
    // This initializes _ambrosiaInternalState, but its primary purpose here is to check that 'appState' derives from AmbrosiaAppState
    initializeAmbrosiaState(appState);

    if (_icProcess !== null)
    {
        throw new Error(`The Immortal Coordinator (PID ${_icProcess.pid}) is already running`);
    }

    if (!Process.env["AZURE_STORAGE_CONN_STRING"])
    {
        throw new Error("The 'AZURE_STORAGE_CONN_STRING' environment variable is missing");
    }

    const icExecutable: string = Utils.getICExecutable(config.icBinFolder, config.useNetCore, config.isTimeTravelDebugging); // This may throw
    const dirToDelete: string =  Path.join(config.icLogFolder, `${config.icInstanceName}_${config.appVersion}`);

    if (!File.existsSync(config.icLogFolder))
    {
        throw new Error(`The specified icLogFolder (${config.icLogFolder}) does not exist`);
    }

    if (config.lbOptions.deleteLogs)
    {
        // Clear (reset) the log folder 
        // Note: It's not sufficient just to delete the files, the directory has to be deleted too, otherwise the IC will fail with "FATAL ERROR 2: Missing checkpoint 1"
        if (File.existsSync(dirToDelete))
        {
            // Safeguard against accidental root folder deletion (eg. "C:\")
            if ((dirToDelete.length < 5) || (dirToDelete.indexOf("log") === -1))
            {
                throw new Error(`The specified dirToDelete ('${dirToDelete}') is either too short or does not contain the word 'log'`);
            }

            // Delete all files (if any) from the folder
            File.readdirSync(dirToDelete).forEach((fileName: string) => 
            {
                let fullFileName: string = Path.join(dirToDelete, fileName);
                Utils.deleteFile(fullFileName);
            });
            File.rmdirSync(dirToDelete);
            Utils.log("Warning: Logs deleted - Recovery will not run");
        }
        else
        {
            // The folder could have been deleted manually, so we don't consider this case to be an error
        }
    }
    else
    {
        Utils.log(!config.isTimeTravelDebugging ? "Recovery will run" : "Warning: Recovery will run in time-travel debugging mode (the 'RecoveryComplete' event will NOT be raised)");
    }

    _config = config;
    _config.dispatcher = _poster.wrapDispatcher(_config.dispatcher);    

    // Windows-specific Notes:
    // [1] The following runs in its own cmd window, but the returned ChildProcess is for the cmd.exe that ran "start cmd..." (and then immediately ended) so it's not much use (eg. "close" fires for the wrong process).
    //     let icProcess: ChildProcess.ChildProcess = ChildProcess.spawn("start cmd /K \"%AMBROSIATOOLS%\\x64\\Release\\net461\\ImmortalCoordinator.exe --instanceName=server --port=2500\"",  { stdio: "ignore", shell: true });
    //
    // [2] The following starts the IC process in a visible console (not cmd.exe) window. The PID does not match that reported for IC.exe in Task Manager,
    //     in fact Task Manager doesn't even display the returned PID. Consequently, the PID cannot be used to kill the process (and we cannot capture stdout/stderr).
    //     However, explicitly closing the console window DOES fire the "close" event as expected.
    //     Note: The console window does not respond to CTRL+C input (SIGINT), but will to CTRL+Break (SIGBREAK). 
    //           See https://docs.microsoft.com/en-us/windows/console/ctrl-c-and-ctrl-break-signals?redirectedfrom=MSDN.
    //     let icProcess: ChildProcess.ChildProcess = ChildProcess.spawn(icExecutable, [`--instanceName=${instanceName}`, `--port=${icCraPort}`],  { stdio: "ignore", shell: true, detached: false });

    // The following starts the IC process directly (no visible console) and pipes both stdout/stderr to our stdout.
    // To aid in distinguishing the IC output from our own output we use the Utils.StandardOutputFormatter class.
    Utils.log(`Starting ${icExecutable}...`);
    emitAppEvent(Messages.AppEventType.ICStarting);

    // See https://github.com/microsoft/AMBROSIA/blob/master/Samples/HelloWorld/TimeTravel-Windows.md
    let timeTravelDebuggingArgs: string[] = ["DebugInstance", `--instanceName=${config.icInstanceName}`, `--receivePort=${config.icReceivePort}`, `--sendPort=${config.icSendPort}`, `--log=${config.icLogFolder}`, `--checkpoint=${config.debugStartCheckpoint}`];
    let normalArgs: string[] = [`--instanceName=${config.icInstanceName}`, `--port=${config.icCraPort}`];

    _icProcess = ChildProcess.spawn(config.useNetCore ? "dotnet" : icExecutable, 
        (config.useNetCore ? [icExecutable] : []).concat(config.isTimeTravelDebugging ? timeTravelDebuggingArgs : normalArgs),
        { stdio: ["ignore", "pipe", "pipe"], shell: false, detached: false });
    
    let outputFormatter: Utils.StandardOutputFormatter = new Utils.StandardOutputFormatter("[IC]", 
        Utils.ConsoleForegroundColors.Cyan,
        [/^Ready/, /Exception:/, /FATAL ERROR/, 
            /^Adding input:$/, /^Adding output:$/, /^restoring input:$/, /^restoring output:$/, // These detect self-connections
            /^Adding input:/, /^Adding output:/, /^restoring input:/, /^restoring output:/]); // These detect remote-connections
    
    let outputFormatterStream: Utils.StandardOutputFormatter = _icProcess.stdout.pipe(outputFormatter);
    let canLogToConsole: boolean = ((Configuration.loadedConfig().lbOptions.outputLogDestination & Configuration.OutputLogDestination.Console) === Configuration.OutputLogDestination.Console);
    
    if (canLogToConsole)
    {
        outputFormatterStream.pipe(Process.stdout);
        _icProcess.stderr.pipe(Process.stdout);
    }
    else
    {
        // We still need a [no-op] place for the "transformed" outputFormatterStream to be written 
        // [otherwise the Transform stream will simply endlessly buffer as it reads data from stdout]
        // Note: On Windows NUL is a device, not a file, so it must be accessed via device namespace by putting \\.\ at the beginning
        let nullOut: File.WriteStream = File.createWriteStream(Utils.isWindows() ? "\\\\.\\NUL" : "/dev/null");
        outputFormatterStream.pipe(nullOut);
        _icProcess.stderr.pipe(nullOut); // This is just to prevent the stderr output from appearing
    }

    // Add a handler to detect watched-for tokens
    outputFormatter.on("tokenFound", (token: string, line: string, multiLine: string) =>
    {
        switch (token)  
        {
            case "/^Ready/":
                onICReady();
                break;
            case "/Exception:/":
            case "/FATAL ERROR/":
                let error: Error = new Error(multiLine);
                let isFatalError: boolean = (token === "/FATAL ERROR/");
                
                // Reporting the JS LB stack trace isn't helpful (and is potentially confusing for an "external" error)
                error.stack = error.message; 
                
                // Note: Sometimes the IC will report this "expected" CRA error during start-up:
                //   System.InvalidOperationException: Nullable object must have a value.
                //     at System.ThrowHelper.ThrowInvalidOperationException(ExceptionResource resource)
                //     at CRA.ClientLibrary.CRAClientLibrary.<ConnectAsync>d__46.MoveNext()
                //   Possible reason: The connection-initiating CRA instance appears to be down or could not be found. Restart it and this connection will be completed automatically                    
                if (multiLine.indexOf("Possible reason: The connection-initiating CRA instance appears to be down") !== -1)
                {
                    // TODO: Rephrase the [only possibly real] "error" into a warning?
                    isFatalError = false;
                }

                _config.onError("Exception", error, isFatalError);
                outputFormatter.clearUnprocessedLines(); // Because we outputted 'multiLine'
                break;
            case "/^Adding input:$/":
            case "/^Adding output:$/":
            case "/^restoring input:$/":
            case "/^restoring output:$/":
                checkSelfConnectionsCount();
                break;
            case "/^Adding input:/":
            case "/^Adding output:/":
            case "/^restoring input:/":
            case "/^restoring output:/":
                _remoteConnectionCount++;
                checkRemoteConnectionsCount();
                break;
        }
    });

    _icProcess.on("close", (code: number, signal: NodeJS.Signals) =>
    {
        if (code === 4294967295) // 0xFFFFFFFF
        {
            code = -1;
        }
        // Note: On Windows, exit code 0xC000013A means that the application terminated as a result of either a CTRL+Break or closing the console window.
        //       On Windows, an explicit kill (via either Task Manager or taskkill /F) will result in a code of '1' and a signal of 'null'.
        let byUserRequest: boolean = (Process.platform === "win32") && (code == 0xC000013A);
        let exitState: string = byUserRequest ? "at user request" : (code === null ? (signal === "SIGTERM" ? "normally" : `signal: ${signal}`) : `exit code: ${code}`);
        let icExecutableName: string = Path.basename(Utils.getICExecutable(config.icBinFolder, config.useNetCore, config.isTimeTravelDebugging));
        Utils.log(`${icExecutableName} stopped (${exitState})`);
        emitAppEvent(Messages.AppEventType.ICStopped, code === null ? 0 : code);
        Utils.closeLog();
    });

    Utils.log(`\'${config.icInstanceName}\' IC started (PID ${_icProcess.pid})${config.isTimeTravelDebugging ? "" : ". Waiting for IC to report ready..."}`);

    // In TTD mode, the IC doesn't report "Ready", so we have to call onICReady() explicitly
    if (config.isTimeTravelDebugging)
    {
        onICReady();
    }
    return (_icProcess);
}

/** Stops the Immortal Coordinator process. */
export function stop()
{
    if (!_icProcess)
    {
        return;
    }

    if (_icSendSocket)
    {
        _outgoingMessageStream.close();
        _icSendSocket.destroy();
    }

    if (_icReceiveSocket)
    {
        _icReceiveSocket.destroy();
    }
    
    if (_icProcess)
    {
        _icProcess.kill();
        _icProcess = null;
        /*
        // Only needed if using approach [2] above
        if (Process.platform === "win32")
        {
            // For a 'shell:true/detached:true' child process, unref() appears to have the same effect as kill() on Windows [but ONLY if kill() is NOT also called]
            // TODO: Why is this? kill() works as expected for a 'shell:false/detached:true' process, so it must be related to 'shell:true'
            icProcess.unref(); 
        }
        else
        {
            // This will raise the 'close' event
            icProcess.kill(); 
        }
        */
    }
}

/** 
 * Once the LB connects to the IC, the IC should then create 4 connections to itself (to the Input/Output ports for 2 channels: data and control). 
 * Thus, the IC should report a total of 4 "Adding/restoring input:/output:" messages (in any combination), otherwise it indicates that the IC is 
 * not properly registered (or is not behaving as expected). This function checks for this condition.
 */
function checkSelfConnectionsCount(timeoutInMs: number = 5000): void
{
    if (++_selfConnectionCount === 4)
    {
        // The IC is now capable of handling (ie. immediately responding to) self-call RPC's
        emitAppEvent(Messages.AppEventType.ICReadyForSelfCallRpc);
    }

    _selfConnectionCountCheckTimer = Utils.restartOnceOnlyTimer(_selfConnectionCountCheckTimer, timeoutInMs, () =>
    {
        // The CRA will make connections [for a given instance] one-at-a-time in the order they appear in the CRAConnectionsTable [ie. sorted ascending by RowKey, which
        // includes the instance name]. If a remote instance is down, CRA will wait indefinitely for the vertex to connect. Consequently, the CRA may get "stuck" before 
        // it makes it to either the 'control' and/or 'data' connections for the local instance, resulting in either 0 or 2 self-connection messages instead of 4.
        if (_selfConnectionCount !== 4)
        {
            Utils.log(`Warning: The IC has reported ${_selfConnectionCount} self-connection messages when 4 were expected; ` +
                      `${(_remoteInstanceNames.length > 0) ? `one-or-more of the expected IC instances ('${_remoteInstanceNames.join("', '")}') may be down, or ` : ""}` + 
                      `the '${_config.icInstanceName}' IC may need to be re-registered`);
        }
        else
        {
            // This handles the case where we don't receive ANY "Adding/restoring input/output:(remoteInstanceName)" messages in the output, 
            // so checkRemoteConnectionsCount() will never have been called
            checkRemoteConnectionsCount();
        }
    });
}

/**
 * The LB should make 4 connections (to the Input/Output ports for 2 channels: data and control) to each instance (vertex) in the CRAConnectionTable.
 * This function checks if the required number of connections has been made (within the specified timeoutInMs).
 */
function checkRemoteConnectionsCount(timeoutInMs: number = _remoteInstanceNames.length * 4 * 1000): void
{
    if (_remoteInstanceNames.length === 0)
    {
        return;
    }

    _remoteConnectionCountCheckTimer = Utils.restartOnceOnlyTimer(_remoteConnectionCountCheckTimer, timeoutInMs, () =>
    {
        let expectedRemoteConnectionCount: number = _remoteInstanceNames.length * 4;
        if (_remoteConnectionCount !== expectedRemoteConnectionCount)
        {
            Utils.log(`Warning: The IC has reported ${_remoteConnectionCount} remote connection messages when ${expectedRemoteConnectionCount} were expected; ` +
                      `one-or-more of the remote IC instances ('${_remoteInstanceNames.join("', '")}') may be down`);
        }
    });
}

/** 
 * Once the LB connects to the IC, the IC should then create 4 connections to itself (to the Input/Output ports for 2 channels: data and control). 
 * This function checks if ANY connection has been made (within, by default, 10 seconds).
 */
export function checkSelfConnection(timeoutInMs: number = 10 * 1000)
{
    _selfConnectionCheckTimer = Utils.restartOnceOnlyTimer(_selfConnectionCheckTimer, timeoutInMs, () =>
    {
        if (_selfConnectionCount === 0)
        {
            Utils.log(`Warning: The IC has not yet reported making any connections to itself (required for self-call RPC support); ` +
                        `${(_remoteInstanceNames.length > 0) ? `one-or-more of the expected IC instances ('${_remoteInstanceNames.join("', '")}') may be down, or ` : ""}` + 
                        `the '${_config.icInstanceName}' IC may need to be re-registered`);
        }
    });
}

/** Called when the LB and IC are connected (on both ports). Sets up the data handler for the receive socket. */
function onICConnected(): void
{
    let receivingCheckpoint: boolean = false;
    let checkpointBytesTotal: number = 0;
    let checkpointBytesRemaining: number = 0;
    let incomingCheckpoint: Streams.IncomingCheckpoint = null;
    let checkpointStream: Stream.Writable = null; // This is where we will write received checkpoint data
    let pageReceiveBuffer: Buffer = Buffer.alloc(0); // This is where we will accumulate bytes until we have [at least 1] complete log page
    let checkpointRestoreStartTime: number = 0;

    // Called when we've received the last 'chunk' of checkpoint data from the IC
    function onCheckpointEnd(finalCheckpointChunk: Uint8Array)
    {
        receivingCheckpoint = false;

        // Wait for checkpointStream to finish
        // Note: To be sure that we don't start reading/processing log pages until the checkpoint has been fully received
        //       [restored], we pause the inbound socket from the IC until the checkpointStream raises it's 'finish' event
        _icReceiveSocket.pause();
        checkpointStream.on("finish", (error?: Error) => 
        {
            incomingCheckpoint.onFinished(error);
            Utils.log(`Checkpoint (${checkpointBytesTotal} bytes) ${error ? `restore failed (reason: ${error.message})` : `restored (in ${Date.now() - checkpointRestoreStartTime}ms)`}`);
            _icReceiveSocket.resume();
        });
        checkpointStream.end(finalCheckpointChunk);
    }

    // Read the log pages (and checkpoint data) received from the IC. Checkpoint data can be large, and is
    // sent without a header, so we read it in a different "mode" to the way we read regular log pages.
    _icReceiveSocket.on("data", (data: Buffer) =>
    {
        // If we're in the process of receiving checkpoint data, we follow a different path than the "normal" sequence-of-log-pages path
        if (receivingCheckpoint)
        {
            if (checkpointBytesRemaining > data.length)
            {
                // The data is all checkpoint data
                checkpointStream.write(data); // If this returns false, it will just buffer
                checkpointBytesRemaining -= data.length;
                return; // Wait for more data
            }
            else
            {
                // The data includes the tail of the checkpoint (and possibly part, or all, of the next log page)
                let checkpointTailChunk: Uint8Array = new Uint8Array(checkpointBytesRemaining);
                data.copy(checkpointTailChunk, 0, 0, checkpointBytesRemaining);
                onCheckpointEnd(checkpointTailChunk);
                
                if (checkpointBytesRemaining < data.length)
                {
                    data = data.slice(checkpointBytesRemaining, data.length); 
                    // data will now contain part (or all) of the next log page
                }
                else
                {
                    // checkpointBytesRemaining == data.length, so we've processed all the checkpoint data
                    return; // Wait from the next log page
                }
            }
        }

        // TESTING (2 log pages, the first with 2 messages, the second with one message)
        // let logRec1: Buffer = Buffer.from([0x96, 0x61, 0xd1, 0xdc, 0x1c, 0x00, 0x00, 0x00, 0x02, 0x0b, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x02, 0x0b, 0x02, 0x09]); 
        // let logRec2: Buffer = Buffer.from([0x96, 0x61, 0xd1, 0xdc, 0x1a, 0x00, 0x00, 0x00, 0x02, 0x0b, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x02, 0x0b]); 
        // data = Buffer.alloc(logRec1.length + logRec2.length, Buffer.concat([logRec1, logRec2]));

        // Note: There is no way to know [from the message header] the instance name of the IC that sent the data
        let showBytes: boolean = _config.lbOptions.verboseOutputLogging; // For debugging
        Utils.log(`Received data from IC (${data.length} bytes)` + (showBytes ? `: ${Utils.makeDisplayBytes(data)}` : ""));

        pageReceiveBuffer = Buffer.concat([pageReceiveBuffer, data], pageReceiveBuffer.length + data.length);
        let logPageLength: number = -1;

        while ((logPageLength = Messages.getLogPageLength(pageReceiveBuffer)) !== -1)
        {
            // Dispatch all messages in the log page
            checkpointBytesTotal = Messages.processLogPage(pageReceiveBuffer, _config);
            
            if (checkpointBytesTotal >= 0)
            {
                // We just read a 'Checkpoint' message (which will be the only message in the log page)
                // so we need to switch "modes" (receivingCheckpoint = true) to read the checkpoint data
                receivingCheckpoint = true;
                checkpointBytesRemaining = checkpointBytesTotal;
                incomingCheckpoint = _config.checkpointConsumer();
                checkpointStream = incomingCheckpoint.dataStream;
                checkpointRestoreStartTime = Date.now();
                _ambrosiaInternalState = null; // The app MUST call initializeAmbrosiaState() after restoring the checkpoint

                // Rather than, say, encoding the size as the first 8 bytes of the stream, we use an AppEvent
                // (in the C# LB this is handled automatically by [DataContract], so we can't do the same)
                emitAppEvent(Messages.AppEventType.IncomingCheckpointStreamSize, checkpointBytesTotal);

                if (checkpointBytesTotal === 0)
                {
                    onCheckpointEnd(Messages.EMPTY_BYTE_ARRAY);
                }
            }
            
            if (logPageLength < pageReceiveBuffer.length)
            {
                // We have part of the next log page(s), so truncate to just that portion
                pageReceiveBuffer = pageReceiveBuffer.slice(logPageLength, pageReceiveBuffer.length);

                if (receivingCheckpoint)
                {
                    // Rather than part of the next log page, we have part (or all) of the checkpoint data (and maybe some of the next log page after that)
                    if (checkpointBytesTotal <= pageReceiveBuffer.length)
                    {
                        // We have already read ALL of the checkpoint data
                        let checkpointChunk: Uint8Array = new Uint8Array(checkpointBytesTotal);
                        pageReceiveBuffer.copy(checkpointChunk, 0, 0, checkpointBytesTotal);
                        onCheckpointEnd(checkpointChunk);
    
                        if (checkpointBytesTotal < pageReceiveBuffer.length)
                        {
                            pageReceiveBuffer = pageReceiveBuffer.slice(checkpointBytesTotal, pageReceiveBuffer.length); 
                            // pageReceiveBuffer will now contain part (or all) of the next log page
                        }
                        else
                        {
                            // checkpointBytesTotal == pageReceiveBuffer.length, so pageReceiveBuffer is fully processed
                            pageReceiveBuffer = pageReceiveBuffer.slice(0, 0); // Empty the buffer (without reallocating it)
                        }
                    }
                    else
                    {
                        // We have only read PART of the checkpoint data: it will take additional reads to receive it all
                        checkpointStream.write(pageReceiveBuffer); // If this returns false, it will just buffer
                        checkpointBytesRemaining -= pageReceiveBuffer.length;
                        pageReceiveBuffer = pageReceiveBuffer.slice(0, 0); // Empty the buffer (without reallocating it)
                        break; // Exit from the normal log page 'while' loop
                    }
                }
            }
            else
            {
                // We have a single, complete log page
                pageReceiveBuffer = pageReceiveBuffer.slice(0, 0); // Empty the buffer (without reallocating it)
            }
        }
    });
}

/** Connects the IC's send/receive sockets and emits the 'ICStarted' AppEvent. */
function onICReady()
{ 
    // Connect to the IC's send port [this is the port we will receive on]
    _icReceiveSocket = Net.connect(_config.icSendPort, "localhost", () =>
    {
        Utils.log(`LB connected to IC send port (${_config.icSendPort})`);
    });

    _icReceiveSocket.on("error", (error: Error) =>
    {
        _config.onError("ReceiveSocket", error)
    });

    // Connect to the IC's receive port [this is the port we will send on]
    _icSendSocket = Net.connect(_config.icReceivePort, "localhost", () =>
    {
        Utils.log(`LB connected to IC receive port (${_config.icReceivePort})`);
        onICConnected();
    });

    _icSendSocket.on("error", (error: Error) =>
    {
        _config.onError("SendSocket", error)
    });

    _outgoingMessageStream = new Streams.OutgoingMessageStream(_icSendSocket, (error: Error) => _config.onError("OutgoingMessageStream", error));

    emitAppEvent(Messages.AppEventType.ICStarted);
}

/** Raises the specified event, which can be handled in the app's MessageDispatcher(). */
export function emitAppEvent(eventType: Messages.AppEventType, ...args: any[])
{
    _config.dispatcher(new Messages.AppEvent(eventType, ...args));
}
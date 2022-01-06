// Module for interacting with the Immortal Coordinator.
import Stream = require("stream");
import Process = require("process");
import File = require("fs");
import ChildProcess = require("child_process");
import Path = require("path");
import Net = require("net");
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as AmbrosiaStorage from "./Storage";
import * as Configuration from "./Configuration";
import * as Messages from "./Messages";
import * as Meta from "./Meta";
import * as Root from "./AmbrosiaRoot";
import * as Streams from "./Streams";
import * as Utils from "./Utils/Utils-Index";

// TODO: Revisit all "NonNullAssertion [for 'strictNullChecks']" comments and try to eliminate use of the non-null assertion 
//       operator ("!" suffix), since its use can hide "real" errors from the compiler. The comment makes the operators easy to find.
//       These were sometimes used to help minimize code-churn when making the initial migration to 'strictNullChecks'.

/** Type of a failed Promise. */
export type RejectedPromise = (error: Error) => void;

export const POST_METHOD_ID: number = -1;
export const POST_BY_IMPULSE_METHOD_ID: number = -2;

/** A named argument for a post method call. */
export interface PostMethodArg { argName: string, argValue: any }

type PostMethodArgs = (PostMethodArg | undefined)[]; // We need to include undefined to support optional parameters [see arg()]

/** The name of the local immortal instance. */
export function instanceName(): string
{
    return (Configuration.loadedConfig().instanceName);
}

/** 
 * Creates a named argument for a post method call.\
 * An optional argument is indicated by the supplied 'name' ending with '?'.\
 * Note: Returns _undefined_ if 'value' is _undefined_.
 */
// Note: IC.arg() must be used to provide the arguments for post methods [but not for non-post methods], because 
//       post method args are just a subset of the JSON args that are sent (we send meta-data in the JSON args too).
export function arg(name: string, value: any): PostMethodArg | undefined
{
    if (value === undefined) // This can happen when using the TS wrapper functions produced by Meta.emitTypeScriptFile() [when optional PostMethodArg values are omitted in the call to the wrapper]
    {
        return (undefined); // This 'PostMethodArg' will get filtered out by IC.postFork()
    }
    Meta.checkName(name, "method argument");
    return ({ argName: name, argValue: value });
}

/** 
 * Instantiates the application state class instance using the supplied constructor (class name). The constructor will be called using the supplied 'restoredAppState'
 * which is **required** when restoring a checkpoint (or when upgrading app state), but can be omitted otherwise. Note that when restoring a checkpoint, 'restoredAppState'
 * will be a deserialized data-only object, ie. it will have the same "shape" as a 'T' but it will not be a constructed 'T' instance.
 * 
 * Must be called **immediately** after receiving (restoring) a checkpoint, although if using simpleCheckpointConsumer() this will be done automatically.
 * 
 * **WARNING:** The returned application state instance MUST remain the same for the life of the app because Ambrosia holds a reference to it.
 */
export function initializeAmbrosiaState<T extends Root.AmbrosiaAppState>(appStateConstructor: new (restoredAppState?: T) => T, restoredAppState?: T): T
{
    Root.checkAppStateConstructor(appStateConstructor);

    // We call 'new' because we have to rehydrate the class (prototype) too - the deserialized data alone (restoredAppState) is not enough
    const appState: T = new appStateConstructor(restoredAppState);

    // Runtime type check
    if (!(appState instanceof Root.AmbrosiaAppState)) // Or: Root.AmbrosiaAppState.prototype.isPrototypeOf(appState)
    {
        throw new Error(`The instantiated 'appStateConstructor' class ('${appStateConstructor.name}') does not derive from AmbrosiaAppState`);
    }

    // Check for an empty __ambrosiaInternalState [which could arise from a user-coding mistake] since this is always an error condition
    if (!appState.__ambrosiaInternalState)
    {
        const emptyValue: string = (appState.__ambrosiaInternalState === null) ? "null" : ((appState.__ambrosiaInternalState === undefined) ? "undefined" : "(empty)");
        throw new Error(`The instantiated 'appStateConstructor' class ('${appStateConstructor.name}') has a ${emptyValue} __ambrosiaInternalState; this can indicate possible app state corruption`);
    }

    // Check that the instantiated appState didn't create a new __ambrosiaInternalState
    if (restoredAppState && !appState.__ambrosiaInternalState.isSameInstance(restoredAppState))
    {
        throw new Error(`The instantiated 'appStateConstructor' class ('${appStateConstructor.name}') did not maintain the __ambrosiaInternalState from the supplied 'restoredAppState'`);
    }

    // Check for serializability
    try
    {
        Utils.checkForCircularReferences(appState, true);
    }
    catch (error: unknown)
    {
        throw new Error(`The instantiated 'appStateConstructor' class ('${appStateConstructor.name}') is invalid (reason: ${Utils.makeError(error).message})`);
    }

    _appState = appState;
    return (appState);
}

/** 
 * Throws if the supplied 'appState' is not the same instance as the 'appState' passed to IC.initializeAmbrosiaState().\
 * MUST be called immediately before producing (saving) a checkpoint, although if using simpleCheckpointProducer() this will be done automatically.
 */
export function checkAmbrosiaState(appState: Root.AmbrosiaAppState): void
{
    if (!_appState.__ambrosiaInternalState.isSameInstance(appState))
    {
        throw new Error(`The supplied appState is not the same instance as the appState returned from IC.initializeAmbrosiaState(); this can be the result of an accidental reassignment of appState`);
    }
}

/** [Internal] Resets the flags that should become set when an upgrade is correctly performed [by user code]. */
export function clearUpgradeFlags(): void
{
    _icUpgradeCalled = false;
    _appState.__ambrosiaInternalState.upgradeCalled = false;
}

/** [Internal] Returns true if all the the flags that should become set when an upgrade is correctly performed [by user code] have been set. */
export function checkUpgradeFlags(): boolean
{
    return (_icUpgradeCalled && _appState.__ambrosiaInternalState.upgradeCalled);
}

/** A singleton-instanced class that provides methods for tunneling RPC calls through the [built-in] 'post' method. */
class Poster
{
    public static METHOD_PARAMETER_PREFIX: string = "arg:"; // Used to distinguish actual method arguments from internal arguments (like "senderInstanceName")
    private static METHOD_RESULT_SUFFIX: string = "_Result";
    private static UNDEFINED_RETURN_VALUE: string = "__UNDEFINED__";
    private static _poster: Poster; // The singleton instance

    // Private because to create a [singleton] Poster instance the createPoster() method should be used
    private constructor()
    {
    }

    /** Returns the next 'post' call ID. */
    private nextPostCallID()
    {
        if (!_appState)
        {
            throw new Error("_appState not set; IC.initializeAmbrosiaState() may not have been called immediately after receiving a checkpoint");
        }
        return (_appState.__ambrosiaInternalState.getNextPostCallID());
    }

    /** Creates the singleton instance of the Poster class. */
    static createPoster(): Poster
    {
        if (!this._poster)
        {
            this._poster = new Poster();
        }
        return (this._poster);
    }

    /** 
     * Creates a wrapper around the supplied dispatcher to intercept post method result RPCs, handle "built-in" post methods, check if 
     * the post method/version is published, and - optionally - check the parameters (name/type) of a [non built-in] post method call. 
     */
    wrapDispatcher(dispatcher: Messages.MessageDispatcher): Messages.MessageDispatcher
    {
        const postInterceptDispatcher = function(message: Messages.DispatchedMessage): void
        {
            if (message.type === Messages.DispatchedMessageType.RPC)
            {
                let rpc: Messages.IncomingRPC = message as Messages.IncomingRPC;
                let expandTypes: boolean = false;
                let attrs: string = "";

                if (rpc.methodID === POST_BY_IMPULSE_METHOD_ID)
                {
                    // Create a Post call from the Impulse self-call
                    // TODO: The downside of handling this internally (rather than via code-gen), is that the user never gets to know the callID
                    //       for the post method call [unlike when using postFork()], which may make it harder to write the postResult handler
                    const destinationInstance: string = rpc.getJsonParam("destinationInstance");
                    const methodName: string = rpc.getJsonParam("methodName");
                    const methodVersion: number = parseInt(rpc.getJsonParam("methodVersion"));
                    const resultTimeoutInMs: number = parseInt(rpc.getJsonParam("resultTimeoutInMs"));
                    const callContextData: any = rpc.getJsonParam("callContextData");
                    const methodArgs: PostMethodArg[] = rpc.getJsonParam("methodArgs");

                    Utils.log(`Intercepted [Impulse] RPC invocation for post method '${methodName}'`);
                    postFork(destinationInstance, methodName, methodVersion, resultTimeoutInMs, callContextData, ...methodArgs);
                    return;
                }

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

                        case "_ping":
                            postResult<number>(rpc, getPostMethodArg(rpc, "sentTime")); // The actual result (roundtripTimeInMs) is computed when the postResult is received
                            return;
                    }

                    // Handle the case where the post method/version has not been published (or the supplied parameters don't match the published parameters)
                    // Note: We can't do the same for non-post methods [by checking for the methodID] because non-post methods don't have an error-return capability
                    let methodName: string = getPostMethodName(rpc);
                    let methodVersion: number = getPostMethodVersion(rpc);
                    let isPublished: boolean = Meta.isPublishedMethod(methodName);
                    let method: Meta.Method | null = !isPublished ? null : Meta.getPublishedMethod(methodName, methodVersion);
                    let isSupportedVersion: boolean = (method !== null);

                    if (method !== null) // "if (isSupportedVersion)" isn't sufficient to make the compiler happy when using 'strictNullChecks'
                    {
                        let unknownArgNames: string[] = [];

                        for (const paramName of rpc.jsonParamNames)
                        {
                            if (paramName.startsWith(Poster.METHOD_PARAMETER_PREFIX))
                            {
                                // Whether the method parameter is optional but was supplied as required, or (conversely) if the method
                                // parameter is required but was supplied as optional, we'll still accept the supplied parameter
                                const argName: string = paramName.replace(Poster.METHOD_PARAMETER_PREFIX, "");
                                if (method.parameterNames.map(pn => Meta.Method.trimRest(Utils.trimTrailingChar(pn, "?"))).indexOf(Utils.trimTrailingChar(argName, "?")) === -1)
                                {
                                    unknownArgNames.push(argName);
                                }
                            }
                        }

                        if (unknownArgNames.length > 0)
                        {
                            Utils.log(`Warning: Instance '${getPostMethodSender(rpc)}' supplied ${unknownArgNames.length} unexpected arguments (${unknownArgNames.join(", ")}) for post method '${methodName}'`);
                            postError(rpc, new Error(`${unknownArgNames.length} unexpected arguments (${unknownArgNames.join(", ")}) were supplied`));
                            return;
                        }

                        // Check that all the required published parameters have been supplied, and are of the correct type
                        for (let i = 0; i < method.parameterNames.length; i++)
                        {
                            let paramName: string = Meta.Method.trimRest(method.parameterNames[i]);
                            let paramType: string = method.parameterTypes[i];
                            let expandedParamType: string = method.expandedParameterTypes[i];
                            let incomingParamName: string = Poster.METHOD_PARAMETER_PREFIX + paramName;
                            let incomingParamValue: any = rpc.getJsonParam(incomingParamName);

                            if (!paramName.endsWith("?") && (incomingParamValue === undefined))
                            {
                                Utils.log(`Warning: Instance '${getPostMethodSender(rpc)}' did not supply required argument '${paramName}' for post method '${methodName}'`);
                                postError(rpc, new Error(`Required argument '${paramName}' was not supplied (argument type: ${paramType})`));
                                return;
                            }

                            // Check that the type of the published/supplied parameters match
                            if (_config.lbOptions.typeCheckIncomingPostMethodParameters && method.isTypeChecked && (paramType !== "any") && (expandedParamType !== "any"))
                            {
                                const incomingParamType: string = Meta.Type.getRuntimeType(incomingParamValue); // May return null
                                if (incomingParamType)
                                {
                                    const failureReason: string | null = Meta.Type.compareTypes(incomingParamType, expandedParamType);
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

    /** 
     * Posts an RPC message (using Fork, which is the only RPC type supported for post). The receiver will examine the 'methodName' parameter of the IncomingRPC.jsonParams
     * to decide which method to invoke. The results of post methods will be sent to the PostResultDispatcher specified in the AmbrosiaConfig parameter of IC.start().\
     * **WARNING:** To ensure replay integrity, pay careful attention to the restrictions on the PostResultDispatcher.
     */
    post(destinationInstance: string, methodName: string, methodVersion: number, resultTimeoutInMs: number = -1, callContextData: any = null, ...methodArgs: PostMethodArg[]): number
    {
        let message: Uint8Array;
        let jsonArgs: Utils.SimpleObject = {};
        let callID: number = this.nextPostCallID(); // This will ensure that we'll associate the posted result (if any) with the correct resultHandler

        Utils.log(`Posting method '${methodName}' (version ${methodVersion}) to ${isSelf(destinationInstance) ? "local" : `'${destinationInstance}'`} IC`);

        if (methodName.endsWith(Poster.METHOD_RESULT_SUFFIX))
        {
            throw new Error(`Invalid methodName '${methodName}': the name cannot end with '${Poster.METHOD_RESULT_SUFFIX}'`);
        }

        jsonArgs["senderInstanceName"] = _config.icInstanceName;
        jsonArgs["destinationInstanceName"] = destinationInstance; // Only required in the case of a timeout [which results in a self-send of the result]
        jsonArgs["methodName"] = methodName;
        jsonArgs["methodVersion"] = methodVersion;
        jsonArgs["callID"] = callID;

        for (let i = 0; i < methodArgs.length; i++)
        {
            jsonArgs[`${Poster.METHOD_PARAMETER_PREFIX}${methodArgs[i].argName}`] = methodArgs[i].argValue; // For example: jsonArgs["arg:digits?"] = 5
        }

        // Note: The result will be dispatched to the user's PostResultDispatcher by isPostResult()
        message = Messages.makeRpcMessage(Messages.RPCType.Fork, destinationInstance, POST_METHOD_ID, jsonArgs);
        sendMessage(message, Messages.MessageType.RPC, destinationInstance);

        // Although passing the callContextData in the jsonArgs would work, it's very inefficient. The data doesn't need to leave the local instance since
        // that's the only place it's ever used. So passing it in the call would both bloat the log and slow down TCP throughput. This issue would become
        // acute for large callContextData, especially for high-frequency post methods; the situation is made even worse because the callContextData is
        // logged and sent twice (once in the outgoing post, then again in the incoming postResult). 
        // So instead we simply store it in the app state. In almost all cases, the [now locally stored] callContextData will be very short lived; as soon
        // as the postResult is received the callContextData will be purged from the app state. Only in the case where a TakeCheckpoint message arrives 
        // between a post and its postResult will the callContextData get persisted to a checkpoint. That said, if the destination instance is offline and 
        // the post() call didn't specify a timeout, then the checkpoint would get bloated with callContextData for all the pending post calls.
        //
        // Additionally, we want to keep track of all in-flight post methods so that:
        // a) We can know if the result has already been received [see isPostResult() and startPostResultTimeout()].
        // b) We can restart the timeouts [if needed] for in-flight post methods after recovery finishes [see onRecoveryComplete()].
        // c) We can [optionally] detect unresponsive destination instances [see below].
        
        // But first, since callContextData has type 'any', we need to check that it will serialize [to app state]
        if (callContextData)
        {
            Meta.checkRuntimeType(callContextData, "callContextData");
        }

        const callDetails: InFlightPostMethodDetails = new InFlightPostMethodDetails(callID, callContextData, methodName, resultTimeoutInMs, destinationInstance, message);
        _appState.__ambrosiaInternalState.pushInFlightPostCall(callID, callDetails);

        if (Messages.isRecoveryRunning())
        {
            _postCallsMadeDuringRecovery.add(callID);
        }

        this.startPostResultTimeout(callID, callDetails);

        // Detect/report unresponsive destination instances
        if ((_config.lbOptions.maxInFlightPostMethods !== -1) && !Messages.isRecoveryRunning())
        {
            const inFlightCallIDs: number[] = _appState.__ambrosiaInternalState.inFlightCallIDs();
            const inFlightPostMethodCount: number = inFlightCallIDs.length;

            if ((inFlightPostMethodCount % Math.max(1, _config.lbOptions.maxInFlightPostMethods)) === 0)
            {
                let destinationTotals: { [destinationInstance: string]: number } = {};
                let totalsList: string = "";

                for (const callID of inFlightCallIDs)
                {
                    const methodDetails: InFlightPostMethodDetails = Utils.assertDefined(_appState.__ambrosiaInternalState.getInFlightPostCall(callID));
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
                Utils.log(`Warning: There are ${inFlightPostMethodCount} in-flight post methods (${totalsList})`, null, Utils.LoggingLevel.Minimal);
            }
        }

        return (callID);
    }

    /** 
     * Starts the result timeout (if needed) for the supplied post method call.\
     * Returns true only if the timeout was started. 
     */
    startPostResultTimeout(callID: number, methodDetails: InFlightPostMethodDetails): boolean
    {
        if ((methodDetails.resultTimeoutInMs !== -1) && _config.lbOptions.allowPostMethodTimeouts && !Messages.isRecoveryRunning())
        {
            // Note: We don't start the timer during recovery because the outcome [for all but the in-flight posts] is already in the log.
            //       Timeouts will be started for any in-flight (ie. sent but no response yet received) post methods by onRecoveryComplete().
            setTimeout(() =>
            {
                // If we haven't yet received the result, self-post a timeout error result.
                // Note: We can't simply call "postResultDispatcher(timeoutError)" on a timer due to recovery issues. Namely, if the timeout occurred when run in realtime 
                //       but the actual result was [eventually] received after the timeout, then during recovery the actual result will happen BEFORE the timeout due to playback
                //       time compression. But if we explicitly post the error then during playback the timeout result will correctly precede the actual result (if the actual
                //       result ever arrives), faithfully replaying what happened in realtime.
                // Note: This approach can lead to BOTH the timeout result AND the actual result being received, since the timeout does not cancel the sending of the actual result.
                //       Further, the test below can succeed in the time-window of after the actual result has been sent but before it has been received.
                if (_appState.__ambrosiaInternalState.getInFlightPostCall(callID) && methodDetails.outgoingRpc)
                {
                    let timeoutError: string = `Timeout: The result for method '${methodDetails.methodName}' did not return after ${methodDetails.resultTimeoutInMs}ms`;
                    let incomingRpc: Messages.IncomingRPC = Messages.makeIncomingRpcFromOutgoingRpc(methodDetails.outgoingRpc);
                    // Note: We post the timeout error as an Impulse because it's occurring as the result of a timer which won't run during recovery
                    postImpulseError(incomingRpc, new Error(timeoutError));
                }
            }, methodDetails.resultTimeoutInMs);
            return (true);
        }
        return (false);
    }

    /** 
     * Sends the result of a (post) method back to the caller, which will handle it in its PostResultDispatcher. 
     * If the method returned void, the 'result' can be omitted.
     */
    postResult<T>(rpc: Messages.IncomingRPC, responseRpcType: Messages.RPCType = Messages.RPCType.Fork, result?: T | Error): void
    {
        let methodName: string = rpc.getJsonParam("methodName");
        let destinationInstance: string = rpc.getJsonParam("senderInstanceName");
        let message: Uint8Array;
        let jsonArgs: Utils.SimpleObject = {};

        jsonArgs["senderInstanceName"] = _config.icInstanceName;
        jsonArgs["destinationInstanceName"] = destinationInstance;
        jsonArgs["methodName"] = methodName + Poster.METHOD_RESULT_SUFFIX;
        jsonArgs["methodVersion"] = rpc.getJsonParam("methodVersion"); // We just echo this back to the caller
        jsonArgs["callID"] = rpc.getJsonParam("callID"); // We just echo this back to the caller
        
        if (result instanceof Error)
        {
            // Note: The Error object doesn't have a toJSON() member, so JSON.stringify() returns "{}". As a workaround we only include Error.message, which will serialize.
            // Note: In the case of a timeout, postResult() will be a self-call, so we can't assume that _config.icInstanceName was the post destination.
            jsonArgs["errorMsg"] = `Post method '${methodName}' (callID ${jsonArgs["callID"]}) sent to '${rpc.getJsonParam("destinationInstanceName")}' failed (reason: ${result.message})`;
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

        Utils.log(`Posting [${Messages.RPCType[responseRpcType]}] ${result instanceof Error ? "error for" : "result of"} method '${methodName}' to ${isSelf(destinationInstance) ? "local" : `'${destinationInstance}'`} IC`);

        message = Messages.makeRpcMessage(responseRpcType, destinationInstance, POST_METHOD_ID, jsonArgs);
        sendMessage(message, Messages.MessageType.RPC, destinationInstance);
    }

    /** 
     * Returns true if the supplied RPC is the posted return value of a posted method.\
     * If the RPC is indeed a post method return value, also invokes the PostResultDispatcher passed to IC.start() via it's AmbrosiaConfig parameter.
     */
    private isPostResult(rpc: Messages.IncomingRPC): boolean
    {
        if (rpc.methodID === POST_METHOD_ID)
        {
            let methodName: string = rpc.getJsonParam("methodName");

            if (methodName.endsWith(Poster.METHOD_RESULT_SUFFIX) && ((rpc.getJsonParam("result") !== undefined) || (rpc.getJsonParam("errorMsg") !== undefined)))
            {
                let result: any = rpc.getJsonParam("result");
                let errorMsg: string = rpc.getJsonParam("errorMsg");
                let originalError: string = rpc.getJsonParam("originalError"); // Note: The source instance can be configured not to send this [which is the default behavior]
                let callID: number = rpc.getJsonParam("callID");
                let senderInstanceName: string = rpc.getJsonParam("senderInstanceName");
                let methodVersion: number = parseInt(rpc.getJsonParam("methodVersion"));
                let baseMethodName: string = methodName.slice(0, -Poster.METHOD_RESULT_SUFFIX.length);

                // Handle the void-return case [which is just notification of method completion]; receiving an 'undefined' result can also indicate a potential error in the sender's method handler code
                if (result === Poster.UNDEFINED_RETURN_VALUE) // 'undefined' won't pass through JSON.stringify()
                {
                    result = undefined;
                }

                let showParams: boolean = _config.lbOptions.debugOutputLogging; // For debugging
                Utils.log(`Intercepted [${Messages.RPCType[rpc.rpcType]}] RPC call for post method '${methodName}' [resultType: ${errorMsg ? "error" : "normal"}]` + (showParams ? ` with params: ${rpc.makeDisplayParams()}`: ""));

                if (errorMsg && originalError)
                {
                    Utils.log(`Originating error (on publisher ['${senderInstanceName}'] of post method '${baseMethodName}'):`);
                    Utils.log(originalError);
                }

                // Note: The method may no longer be in-flight due to a timeout error
                if (_appState.__ambrosiaInternalState.getInFlightPostCall(callID))
                {
                    // Invoke the user-provided PostResultDispatcher (result/error handler)
                    const callDetails: InFlightPostMethodDetails = _appState.__ambrosiaInternalState.popInFlightPostCall(callID);

                    // Note: active/active secondaries remain in constant recovery, so we remove the callID from _postCallsMadeDuringRecovery to stop the set from growing
                    //       monotonically on a secondary. Similarly, removing a "known received" callID on a recovering standalone instance also helps keep the set small.
                    if (Messages.isRecoveryRunning() && _postCallsMadeDuringRecovery.has(callID))
                    {
                        _postCallsMadeDuringRecovery.delete(callID);
                    }

                    if (_config.postResultDispatcher)
                    {
                        switch (baseMethodName)
                        {
                            case "_ping":
                                // Override the nominal result (sentTime) with the actual round-trip time
                                const pingSucceeded: boolean = !errorMsg;
                                const roundtripTimeInMs: number = pingSucceeded ? Date.now() - parseInt(result) : -1;
                                result = roundtripTimeInMs;
                                // Ping is expected to encounter unresponsive instances, so we don't consider a timeout to be a "real" error for ping
                                if (errorMsg.indexOf("Timeout") !== -1)
                                {
                                    errorMsg = "";
                                }
                                break;
                        }

                        if (!_config.postResultDispatcher(senderInstanceName, baseMethodName, methodVersion, callID, callDetails.callContextData, result, errorMsg))
                        {
                            Utils.log(`Warning: The result of post method '${baseMethodName}' from '${senderInstanceName}' was not handled by the provided PostResultDispatcher`);
                        }
                    }
                    else
                    {
                        Utils.log(`Warning: The result of post method '${baseMethodName}' from '${senderInstanceName}' was not handled (reason: No PostResultDispatcher was provided)`);
                    }
                }
                else
                {
                    if (Messages.isRecoveryRunning() && !_postCallsMadeDuringRecovery.has(callID))
                    {
                        // The callID isn't in the in-flight list (which can span restarts), but it's not in the "sent during recovery" list either. So we're
                        // receiving a replayed post result for a post call that we haven't re-sent. Because we didn't send it, nextPostCallID() will not
                        // have been called, so there will now be a permanent mismatch in the callID's of replayed post results and re-sent post calls. 
                        // This situation can arise due to a deterministic programming error (eg. making a post call as the direct result of user input without
                        // using an Impulse to invoke the post). Since determinism is broken, there's no point carrying on with recovery, so we throw (this will
                        // bubble up to the uncaught exception handler which will terminate the process).
                        throw new Error(`Recovery failed (reason: Deterministic programming error detected; a replayed post result (for method '${baseMethodName}' (callID ${callID}) from '${senderInstanceName}') ` +
                                        `was received without its originating post call being made; this is often caused by not using an Impulse to invoke the originating post call)`);
                    }

                    // This can legitimately happen when either:
                    // a) A timeout occurred waiting for the post result to arrive [so postResultDispatcher() will already have been called by the timeout error]
                    // b) A result for both a timeout error AND the actual result were received [so the first one received will have called postResultDispatcher()]
                    Utils.log(`Warning: The result of post method '${baseMethodName}' (callID ${callID}) from '${senderInstanceName}' has already been handled`);
                }
                return (true);
            }
        }
        return (false);
    }
}

let _appState: Root.AmbrosiaAppState; // Reference to the user-supplied application state
let _config: Configuration.AmbrosiaConfig;
let _icProcess: ChildProcess.ChildProcess | null = null;
let _lbSendSocket: Net.Socket | null = null; // Our connection to the IC's receive port
let _lbReceiveSocket: Net.Socket | null = null; // Our connection to the IC's send port
let _outgoingMessageStream: Streams.OutgoingMessageStream;
export let _counters : 
{ 
    remoteSentMessageCount: number,
    sentForkMessageCount: number,
    receivedForkMessageCount: number,
    receivedMessageCount: number
} = { remoteSentMessageCount: 0, sentForkMessageCount: 0, receivedForkMessageCount: 0, receivedMessageCount: 0 };
let _knownDestinationInstanceNames: string[] = [];
let _selfConnectionCount: number = 0;
let _selfConnectionCountCheckTimer: NodeJS.Timeout;
let _selfConnectionCheckTimer: NodeJS.Timeout;
let _remoteConnectionCount: number = 0;
let _remoteConnectionCountCheckTimer: NodeJS.Timeout;
let _poster: Poster = Poster.createPoster();
let _remoteInstanceNames: string[] = [];
let _isPrimary: boolean = false; // Whether this IC is currently the Primary
let _icStoppedSignalled: boolean = false;
let _icStoppingDueToError: boolean = false;
let _icUpgradeCalled: boolean = false;
let _postCallsMadeDuringRecovery = new Set<number>(); // The CallIDs of all post calls made while recovery is running

/** Whether the IC is currently running [due to the local LB starting it]. */
export function isRunning(): boolean
{
    return (_icProcess !== null);
}

/** Returns the process ID of the IC (if it's running), or -1 (if it's not). */
export function PID(): number
{
    return (_icProcess ? _icProcess?.pid ?? -1 : -1)
}

/** 
 * [Internal] Gets or sets whether the IC is currently the Primary.\
 * Note: Becoming the Primary can happen when the instance is running either standalone or in active/active. 
 */
export function isPrimary(value?: boolean): boolean
{
    if (value !== undefined)
    {
        if (value !== _isPrimary)
        {
            _isPrimary = value;
            if (_isPrimary)
            {
                Utils.log("Local instance is now primary");
            }
        }
    }
    return (_isPrimary);
}

/** [Internal] Returns true if the outgoing message stream (to the IC) has filled past the specified percentOfStreamMaximum (0.0 to 1.0). */
export function isOutgoingMessageStreamGettingFull(percentOfStreamMaximum: number): boolean
{
    const isGettingFull: boolean = _outgoingMessageStream && (_outgoingMessageStream.readableLength > (_outgoingMessageStream.maxQueuedBytes * Math.max(0, Math.min(1, percentOfStreamMaximum))));
    return (isGettingFull);
}

/** [Internal] Returns the number of bytes that are waiting to be sent in the outgoing message stream (to the IC). */
export function outgoingMessageStreamBacklog(): number
{
    return (_outgoingMessageStream ? _outgoingMessageStream.readableLength : 0);
}

/** [Internal] Handles the lbOptions.deleteLogs configuration setting. */
export async function deleteLogsAsync(): Promise<void>
{
    const config: Configuration.AmbrosiaConfigFile = Configuration.loadedConfig();
    const FILES: string = Configuration.LogStorageType[Configuration.LogStorageType.Files];
    const BLOBS: string = Configuration.LogStorageType[Configuration.LogStorageType.Blobs];
    const instanceLogFolder: string = Path.join(config.icLogFolder, `${config.instanceName}_${config.appVersion}`); // TODO: There may (one day) also be a "shardID" (Int64) subfolder (see: \AMBROSIA\AmbrosiaLib\Ambrosia\Program.cs)

    // We can only delete the logs if we're controlling starting/stopping of the IC
    if (!config.isIntegratedIC)
    {
        return;
    }

    if ((config.icLogStorageType === FILES) && !File.existsSync(config.icLogFolder))
    {
        File.mkdirSync(config.icLogFolder); // Note: We don't need to do the equivalent when storing logs in Azure Blobs
    }

    if (config.lbOptions.deleteLogs)
    {
        const deletedFileCount: number = await deleteInstanceLogFolderAsync(instanceLogFolder, config.icLogStorageType);
        const storageType: string = (config.icLogStorageType === BLOBS) ? "Azure" : "disk";
        Utils.log(`Warning: ${deletedFileCount} log/checkpoint files ${(deletedFileCount === 0) ? "found" : "deleted"} [on ${storageType}] - Recovery will not run`);
    }
    else
    {
        let recoveryMsg: string = "Recovery will not run (no logs found)";

        // If the config doesn't specify that the logs should be deleted but the log folder is empty (eg. due to manual deletion),
        // then we still need to explicitly remove the log folder to avoid the IC failing with "FATAL ERROR 2: Missing checkpoint 1"
        // [the same (or at least equivalent) issue happens with either LogStorageType]
        switch (config.icLogStorageType)
        {
            case FILES:
                if (File.existsSync(instanceLogFolder))
                {
                    if (File.readdirSync(instanceLogFolder).length === 0)
                    {
                        File.rmdirSync(instanceLogFolder);
                    }
                    else
                    {
                        if (File.readdirSync(instanceLogFolder).filter(fn => /serverlog[\d]+$/.test(fn)))
                        {
                            recoveryMsg = "Recovery will run";
                        }
                    }
                }
                break;

            case BLOBS:
                if (await AmbrosiaStorage.folderContainsBlobLogAsync(instanceLogFolder))
                {
                    recoveryMsg = "Recovery will run";
                }
                break;
        }
        Utils.log(!config.isTimeTravelDebugging ? recoveryMsg : "Warning: Recovery will run in time-travel debugging mode (the 'RecoveryComplete' event will NOT be raised)");
    }
}

/** [Internal] Initializes the set of remote IC instances that this instance communicates with. This list is used for checking IC startup integrity. */
export function setRemoteInstanceNames(remoteInstanceNames: string[]): void
{
    _remoteInstanceNames = remoteInstanceNames;
}

/** [Internal] Class representing an in-flight (ie. sent but no result yet received) post method call. */
export class InFlightPostMethodDetails
{
    callID: number;
    callContextData: any;
    methodName: string;
    resultTimeoutInMs: number;
    destinationInstance: string;
    outgoingRpc: Uint8Array | null;

    constructor(callID: number, callContextData: any, methodName: string, resultTimeoutInMs: number, destinationInstance: string, outgoingRpc: Uint8Array | null)
    {
        this.callID = callID;
        this.callContextData = callContextData;
        this.methodName = methodName;
        this.resultTimeoutInMs = resultTimeoutInMs;
        this.destinationInstance = destinationInstance;
        // The outgoingRpc is only needed to send a timeout error result, so if the method has no timeout then we skip storing it [to save space, both in memory and in the checkpoint]
        this.outgoingRpc = (resultTimeoutInMs === -1) ? null : outgoingRpc;
    }

    /** Factory method that instantiates a new InFlightPostMethodDetails from an existing instance. */
    static createFrom(details: InFlightPostMethodDetails): InFlightPostMethodDetails
    {
        return (new this(details.callID, details.callContextData, details.methodName, details.resultTimeoutInMs, details.destinationInstance, details.outgoingRpc));
    }
}

/** Performs house-keeping checks and actions needed when recovery completes. */
export function onRecoveryComplete()
{
    Utils.log(`Recovery complete (Received ${_counters.receivedMessageCount} messages [${_counters.receivedForkMessageCount} Fork messages], sent ${_counters.sentForkMessageCount} Fork messages)`);
    if (_counters.sentForkMessageCount < _counters.receivedForkMessageCount)
    {
        // If the log ONLY contained messages sent to ourself, then at this point we [might] have an error condition, and the Immortal Coordinator process
        // may go into a CPU spin while it waits for the missing messages. However, since (incoming) replayed messages don't include the source
        // (sender) we cannot make this determination. The best we can do is to check if we sent any messages to a remote instance during recovery 
        // and then use that as a proxy for the fact that we likely received messages from instance(s) other than ourself, and - in which case - we 
        // will skip the warning that there *might* be a problem.
        if (_counters.remoteSentMessageCount === 0)
        {
            Utils.log(`Warning: If the log is known to ONLY contain self-call messages, then at least ${_counters.receivedForkMessageCount} Fork messages should have ` + 
                      `been sent during replay, not ${_counters.sentForkMessageCount}; this condition can indicate an app programming error ` + 
                      `(eg. making a Post/Fork RPC call when an Impulse RPC call should have been used) or an app programming issue ` +
                      `(eg. making a Post/Fork RPC call using a timer that delays the send until after recovery completes)`);
        }
    }
    _counters.remoteSentMessageCount = _counters.receivedMessageCount = _counters.receivedForkMessageCount = _counters.sentForkMessageCount = 0;
    _postCallsMadeDuringRecovery.clear();

    // Start result timeouts for any in-flight (ie. sent but no response yet received) post methods.
    // Note: Unless the in-flight method was called right at the end of recovery, it is likely that the amount of time we'll actually end up waiting for a
    //       [unresponsive] method to complete will be longer - possibly considerably so - than the requested InFlightPostMethodDetails.resultTimeoutInMs.
    let restartedTimeoutCount: number = 0;
    for (const callID of _appState.__ambrosiaInternalState.inFlightCallIDs())
    {
        let methodDetails: InFlightPostMethodDetails = Utils.assertDefined(_appState.__ambrosiaInternalState.getInFlightPostCall(callID));
        if (_poster.startPostResultTimeout(callID, methodDetails))
        {
            restartedTimeoutCount++;
        }
    }
    if (restartedTimeoutCount > 0)
    {
        Utils.log(`Restarted result timeouts for ${restartedTimeoutCount} in-flight post methods`, null, Utils.LoggingLevel.Minimal);
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
    if (Utils.canLog(Utils.LoggingLevel.Verbose))
    {
        Utils.log(`Calling Fork method (ID ${methodID}) on ${isSelf(destinationInstance) ? "local" : `'${destinationInstance}'`} IC`);
    }
    const message: Uint8Array = Messages.makeRpcMessage(Messages.RPCType.Fork, destinationInstance, methodID, jsonOrRawArgs);
    sendMessage(message, Messages.MessageType.RPC, destinationInstance);
}

/** 
 * Queues, but does not send, a [Fork] call of the specified method ID.\
 * Use flushQueue() to send all the queued calls.\
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
    if (Utils.canLog(Utils.LoggingLevel.Verbose))
    {
        Utils.log(`Calling Impulse method (ID ${methodID}) on ${isSelf(destinationInstance) ? "local" : `'${destinationInstance}'`} IC`);
    }
    const message: Uint8Array = Messages.makeRpcMessage(Messages.RPCType.Impulse, destinationInstance, methodID, jsonOrRawArgs);
    sendMessage(message, Messages.MessageType.RPC, destinationInstance);
}

/** 
 * Queues, but does not send, a [Impulse] call of the specified method ID.\
 * Use flushQueue() to send all the queued calls.\
 * Note: A callFork() or callImpulse() will also result in the queue being flushed (at the next tick of the event loop). 
 */
export function queueImpulse(destinationInstance: string, methodID: number, jsonOrRawArgs: object | Uint8Array): void
{
    _outgoingMessageStream.queueBytes(Messages.makeRpcMessage(Messages.RPCType.Impulse, destinationInstance, methodID, jsonOrRawArgs));
}

/** 
 * Calls the specified post method (as a Fork RPC). Returns the unique call ID.
 * 
 * The result (or error) of the called method will be received via the PostResultDispatcher provided to IC.start() in its AmbrosiaConfig parameter.
 */
// Note: There is no postImpulse(), nor should there be. An Impulse cannot be re-sent during recovery by the LB (only received), and we need
//       a post method to ALWAYS be re-sent during recovery so that nextPostCallID() gets called, which keeps the deterministically re-created 
//       state (__ambrosiaInternalState._lastCallID) in-sync with the received (replayed) post results.
//       So instead we offer postByImpulse(), which invokes the post method indirectly via a self-call Impulse RPC. By using a 
//       well-known method ID (POST_BY_IMPULSE_METHOD_ID) the Impulse can be intercepted (in postInterceptDispatcher()) and 
//       converted into the actual post call. The downside of this approach is that the caller of postByImpulse() cannot know 
//       the callID of the post method, which may make writing the postResult handler more difficult. TODO: This could be resolved
//       by not handling POST_BY_IMPULSE_METHOD_ID internally, and instead handling it via the [editable] code-generated dispatcher. 
export function postFork(destinationInstance: string, methodName: string, methodVersion: number, resultTimeoutInMs: number = -1, callContextData: any = null, ...methodArgs: PostMethodArgs): number
{
    checkReadyForSelfCallRpc(destinationInstance);
    const filteredMethodArgs: PostMethodArg[] = methodArgs.filter(arg => arg !== undefined) as PostMethodArg[]; // Remove any 'undefined' elements in the array [see arg()]
    const callID: number = _poster.post(destinationInstance, methodName, methodVersion, resultTimeoutInMs, callContextData, ...filteredMethodArgs);
    return (callID);
}

/** 
 * Calls the specified post method (via a self-call Impulse RPC). Returns void, unlike postFork(), so the callID will not be known.
 * 
 * Note: **Do not** attempt to pass state information to your postResult handler outside of either the 'callContextData' or your application state.
 * This is because the code that calls postByImpulse() will not (must not) re-run during recovery.
 * 
 * The result (or error) of the called method will be received via the PostResultDispatcher provided to IC.start() in its AmbrosiaConfig parameter.
 */
// Note: See comments above on postFork().
 export function postByImpulse(destinationInstance: string, methodName: string, methodVersion: number, resultTimeoutInMs: number = -1, callContextData: any = null, ...methodArgs: PostMethodArgs): void
{
    const jsonArgs: Utils.SimpleObject = {};
    
    jsonArgs["destinationInstance"] = destinationInstance;
    jsonArgs["methodName"] = methodName;
    jsonArgs["methodVersion"] = methodVersion;
    jsonArgs["resultTimeoutInMs"] = resultTimeoutInMs;
    jsonArgs["callContextData"] = callContextData;
    jsonArgs["methodArgs"] = methodArgs.filter(arg => arg !== undefined); // Remove any 'undefined' elements in the array [see arg()]

    // Note: This is ALWAYS a self-call (the subsequent postFork() will use 'destinationInstance')
    callImpulse(instanceName(), POST_BY_IMPULSE_METHOD_ID, jsonArgs);
}

/** 
 * Returns (via Fork) the result of a post method [contained in the supplied RPC] to the post-caller. 
 * If there is no return value (ie. the method returned void) then 'result' can be omitted, but postResult() should always still be called.
 */
export function postResult<T>(rpc: Messages.IncomingRPC, result?: T): void
{
    _poster.postResult<T>(rpc, Messages.RPCType.Fork, result);
}

/** Returns (via Fork) an error when attempting to execute a post method [contained in the supplied RPC] to the post-caller. */
export function postError(rpc: Messages.IncomingRPC, error: Error): void
{
    _poster.postResult<Error>(rpc, Messages.RPCType.Fork, error);
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
 * Returns the value of the specified (post) method parameter, or throws if the parameter cannot be found (unless the 
 * parameter is optional, as indicated by a trailing "?" in the argName, in which case it will return 'undefined').
 */
export function getPostMethodArg(rpc: Messages.IncomingRPC, argName: string): any
{
    checkIsPostMethod(rpc);
    let isOptionalArg: boolean = argName.trim().endsWith("?");
    let paramName: string = `${Poster.METHOD_PARAMETER_PREFIX}${argName}`;
    if (rpc.hasJsonParam(paramName))
    {
        return (rpc.getJsonParam(paramName));
    }
    if (!isOptionalArg && rpc.hasJsonParam(paramName + "?"))
    {
        // The caller [of getPostMethodArg()] didn't specify that the arg was optional, but an optional version of the arg was sent in the RPC, so we use this
        return (rpc.getJsonParam(paramName + "?"));
    }
    if (isOptionalArg && rpc.hasJsonParam(Utils.trimTrailingChar(paramName, "?")))
    {
        // The caller [of getPostMethodArg()] did specify that the arg was optional, but an non-optional version of the arg was sent in the RPC, so we use this
        return (rpc.getJsonParam(Utils.trimTrailingChar(paramName, "?")));
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
    return (rpc.getJsonParam("methodName"));
}

/** Returns the version of the (post) method called by the supplied RPC. */
export function getPostMethodVersion(rpc: Messages.IncomingRPC): number
{
    checkIsPostMethod(rpc);
    return (parseInt(rpc.getJsonParam("methodVersion")));
}

/** 
 * Returns the sender (instance name) of the (post) method called by the supplied RPC.\
 * **WARNING:** This is not a strong identity assertion because it's a spoofable value. Use it only for reporting, **not** for any kind of authorization.
 */
export function getPostMethodSender(rpc: Messages.IncomingRPC): string
{
    checkIsPostMethod(rpc);
    return (rpc.getJsonParam("senderInstanceName"));
}

/** Throws if the supplied IncomingRPC is not a post method call. */
function checkIsPostMethod(rpc: Messages.IncomingRPC): void
{
    if (rpc.methodID !== POST_METHOD_ID)
    {
        throw new Error(`The supplied RPC (methodID ${rpc.methodID}) is not a 'post' method`);
    }
}

/**
 * A utility post method that simply "echos" the value back to the [local] caller.\
 * For example, an app that only made self-calls could, theoretically, be built using just this method
 * without having to publish any post methods of its own.\
 * Result: The value that was supplied. Echoing a value makes it "replayable" by forcing it to be logged.
 * Handle the result in your PostResultDispatcher() for method name '_echo'.
 * Returns a unique callID for the method. 
 */
export function echo_Post<T>(value: T, callContextData: any = null, timeoutInMs: number = -1): number
{
    const callID: number = postFork(_config.icInstanceName, "_echo", 1, timeoutInMs, callContextData, arg("payload", value));
    return (callID);
}

/** 
 * An Impulse wrapper for echo_Post(). Returns void, unlike echo_Post(). 
 * @see echo_Post
 */
export function echo_PostByImpulse<T>(value: T, callContextData: any = null, timeoutInMs: number = -1): void
{
    postByImpulse(_config.icInstanceName, "_echo", 1, timeoutInMs, callContextData, arg("payload", value));
}

/** 
 * A utility post method used to check whether a given instance is responsive.\
 * The 'timeout' defaults to 3000ms.\
 * Result: The total round-trip time in milliseconds, or -1 if the operation timed out.
 * Handle the result in your PostResultDispatcher() for method name '_ping'.
 * Returns a unique callID for the method. 
 */
export function ping_Post(destinationInstance: string, timeoutInMs: number = 3000): number
{
    const callContextData: object = { destinationInstance: destinationInstance, timeoutInMs: timeoutInMs };
    const callID: number = postFork(destinationInstance, "_ping", 1, timeoutInMs, callContextData, arg("sentTime", Date.now()));
    return (callID);
}

/** 
 * An Impulse wrapper for ping_Post(). Returns void, unlike ping_Post().
 * @see ping_Post
 */
export function ping_PostByImpulse(destinationInstance: string, timeoutInMs: number = 3000): void
{
    const callContextData: object = { destinationInstance: destinationInstance, timeoutInMs: timeoutInMs };
    postByImpulse(destinationInstance, "_ping", 1, timeoutInMs, callContextData, arg("sentTime", Date.now()));
}

/** 
 * Requests the local IC to take a checkpoint immediately.\
 * Normally, a checkpoint is only taken (by the IC) whenever the log size exceeds the 'logTriggerSize' (MB) that the IC was either registered or started with.
 */
export function requestCheckpoint(): void
{
    let checkpointMessage: Uint8Array = Messages.makeTakeCheckpointMessage();
    sendMessage(checkpointMessage, Messages.MessageType.TakeCheckpoint, _config.icInstanceName);
}

/** 
 * [Internal] Sends a complete [binary] message to an IC. The message will be queued until the next tick of the event loop, but messages will always be sent in chronological order.\
 * Note: The 'messageType' and 'destinationInstance' parameters are for logging purposes only, and MUST match the values included in the message 'bytes'.
 */
export function sendMessage(bytes: Uint8Array, messageType: Messages.MessageType, destinationInstance: string, immediateFlush: boolean = false): void
{
    if (Utils.canLog(Utils.LoggingLevel.Verbose))
    {
        let messageName: string = Messages.MessageType[messageType];
        let destination: string = isSelf(destinationInstance) ? "local" : `'${destinationInstance}'`;
        let showBytes: boolean = _config.lbOptions.debugOutputLogging; // For debugging
        Utils.log(`Sending ${messageName ? `'${messageName}' ` : ""}to ${destination} IC (${bytes.length} bytes)` + (showBytes ? `: ${Utils.makeDisplayBytes(bytes)}` : ""));
    }
    _outgoingMessageStream.addBytes(bytes, immediateFlush);
}

/** 
 * [Internal] [Experimental] An asynchronous version of sendMessage().\
 * This can be more responsive than sendMessage() when called in a loop, because it avoids queuing messages until the function running the loop ends. This allows message I/O to interleave.\
 * Note: The 'messageType' and 'destinationInstance' parameters are for logging purposes only, and do not supercede the values included in the message 'bytes'.
 */
async function sendMessageAsync(bytes: Uint8Array, messageType: Messages.MessageType, destinationInstance: string): Promise<void>
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
        catch (error: unknown)
        {
            reject(Utils.makeError(error));
        }
    });
    return (promise);
}

/** 
 * Sends (synchronously) all calls queued with queueFork() / queueImpulse().\
 * Returns the number of messages that were queued.
 * 
 * **WARNING:** When enqueuing multiple batches (eg. in a loop), this method should be called from within the same asynchronous callback
 * (eg. via setImmediate()) that also enqueued the batch. Otherwise, because this method runs synchronously, I/O with the IC will not interleave.
 */
export function flushQueue(): number
{
    let queueLength: number = _outgoingMessageStream.flushQueue();
    return (queueLength);
}

/** Returns the number of messages currently in the [outgoing] message queue. */
export function queueLength(): number
{
    return (_outgoingMessageStream.queueLength);
}

/** [Internal] Streams (asynchronously) data to the local IC. Any subsequent messages sent via sendMessage() will queue until the stream finishes. */
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

/** [Internal] [Experimental] An awaitable version of sendStream(). */
async function sendStreamAsync(byteStream: Stream.Readable, streamLength: number = -1, streamName?: string): Promise<void>
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
        catch (error: unknown)
        {
            reject(Utils.makeError(error));
        }
    });
    return (promise);
}

/** [Internal] Streams (asynchronously) the outgoing checkpoint to the IC. Any subsequent messages sent via sendMessage() will queue until the stream finishes (after which outgoingCheckpoint.onFinished() is called). */
export function sendCheckpoint(outgoingCheckpoint: Streams.OutgoingCheckpoint, onSuccess?: () => void): void
{
    // A check to make the compiler happy when using 'strictNullChecks'
    if (!_lbReceiveSocket)
    {
        throw new Error("Unable to send checkpoint (reason: The inbound socket from the IC is null, so it cannot be paused)");
    }

    // When the IC asks the LB to take a checkpoint, the IC won't send any further log pages until it receives the last
    // byte of checkpoint data from the LB. So we don't need to guard against the app state being changed (by the app
    // continuing to process incoming messages) while we stream the checkpoint [which we could do by temorarily pausing
    // the inbound socket from the IC (_lbReceiveSocket) until the stream is finished].
    function onSendCheckpointFinished(error?: Error): void
    {
        // A check to make the compiler happy when using 'strictNullChecks'
        if (!_lbReceiveSocket)
        {
            throw new Error("Unable to continue after sending checkpoint (reason: The inbound socket from the IC is null, so it cannot be resumed)");
        }

        if (outgoingCheckpoint.onFinished)
        {
            outgoingCheckpoint.onFinished(error);
        }
        if (!error)
        {
            emitAppEvent(Messages.AppEventType.CheckpointSaved);
            if (onSuccess)
            {
                onSuccess();
            }
        }
        if (error && !outgoingCheckpoint.onFinished)
        {
            throw error; // The "uncaughtException" handler will catch this
        }
    }
    sendStream(outgoingCheckpoint.dataStream, outgoingCheckpoint.length, `CheckpointDataStream (${outgoingCheckpoint.length} bytes)`, onSendCheckpointFinished);
}

/** [Internal] [Experimental] An awaitable version of sendCheckpoint(). The outgoingCheckpoint.onFinished must be null; use the continuation instead. */
// TODO: This needs more testing.
async function sendCheckpointAsync(outgoingCheckpoint: Streams.OutgoingCheckpoint): Promise<void>
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
        catch (error: unknown)
        {
            reject(Utils.makeError(error));
        }
    });
    return (promise);
}

/** 
 * Upgrades (switches) the executing code of the IC. Typically, the new handlers will reside in an "upgraded" PublisherFramework.g.ts
 * [generated] file which will be included in the app along with the current PublisherFramework.g.ts file.\
 * Should be called when AppEventType.UpgradeCode becomes signalled.
 */
export function upgrade(dispatcher: Messages.MessageDispatcher, checkpointProducer: Streams.CheckpointProducer, checkpointConsumer: Streams.CheckpointConsumer, postResultDispatcher?: Messages.PostResultDispatcher): void
{
    _config.updateHandlers(dispatcher, checkpointProducer, checkpointConsumer, postResultDispatcher);
    _config.dispatcher = _poster.wrapDispatcher(_config.dispatcher);
    _icUpgradeCalled = true;
}

/** [Internal] Deletes Ambrosia log/checkpoint files from the specified folder (on disk or in Azure), then removes the folder. Returns the number of files deleted. */
export async function deleteInstanceLogFolderAsync(instanceLogFolder: string, icLogStorageType: keyof typeof Configuration.LogStorageType): Promise<number>
{
    let deletedFileCount: number = 0;

    switch (icLogStorageType)
    {
        case Configuration.LogStorageType[Configuration.LogStorageType.Files]:
            // Clear (reset) the log folder [this will remove all logs and checkpoints for the instance]
            // Note: It's not sufficient just to delete the files, the directory has to be deleted too, otherwise the IC will fail with "FATAL ERROR 2: Missing checkpoint 1"
            if (File.existsSync(instanceLogFolder))
            {
                // Delete all log/checkpoint files (if any) from the folder.
                // Note: We filter for "*serverlog*", "*serverchkpt*", and "serverkillFile" files to provide protection
                //       in the case that the config.icLogFolder is [accidentally] set to, for example, a system folder.
                File.readdirSync(instanceLogFolder)
                    .filter(fn => /serverlog[\d]+$/.test(fn) || /serverchkpt[\d]+$/.test(fn) || (fn === "serverkillFile"))
                    .forEach((fileName: string) =>
                    {
                        let fullFileName: string = Path.join(instanceLogFolder, fileName);
                        Utils.deleteFile(fullFileName);
                        deletedFileCount++;
                    });

                File.rmdirSync(instanceLogFolder); // Note: rmdirSync() will throw if the directory is not empty
            }
            break;

        case Configuration.LogStorageType[Configuration.LogStorageType.Blobs]:
            deletedFileCount = await AmbrosiaStorage.deleteBlobLogsAsync(instanceLogFolder);
            break;
    }
    return (deletedFileCount);
}

/** 
 * Starts the Immortal Coordinator process.
 * Returns the application state instantiated using 'appStateConstructor' (the application state class name). 
 */
export function start<T extends Root.AmbrosiaAppState>(config: Configuration.AmbrosiaConfig, appStateConstructor: new (restoredAppState?: T) => T): T
{
    Root.checkAppStateConstructor(appStateConstructor);

    // This instantiates the application state, which we return to the caller; it also checks that the constructed state derives from AmbrosiaAppState
    const appState: T = initializeAmbrosiaState(appStateConstructor);

    _config = config;
    _config.dispatcher = _poster.wrapDispatcher(_config.dispatcher);    

    // If an upgrade has occurred, we simply invoke IC.upgrade() again [via the user-provided AppEventType.UpgradeCode handler] to switch _config over to using the upgraded handlers.
    // When the user is ready for the next upgrade [or anytime after the last upgrade], 'activeCode' should be reset to "VCurrent" and the handlers supplied to IC.start() should
    // be replaced by the handlers assigned via the IC.upgrade() call in the AppEventType.UpgradeCode handler.
    if (config.activeCode === Configuration.ActiveCodeType.VNext)
    {
        _icUpgradeCalled = false;
        emitAppEvent(Messages.AppEventType.UpgradeCode);
        if (!_icUpgradeCalled)
        {
            throw new Error(`The 'activeCode' setting is "VNext", but there is no VNext code to use (reason: IC.upgrade() was not called by your AppEventType.UpgradeCode handler)`);
        }
    }
    Utils.log(`Using "${Configuration.ActiveCodeType[config.activeCode]}" application code (appVersion: ${config.appVersion}, upgradeVersion: ${config.upgradeVersion})`);

    if (_config.isIntegratedIC)
    {
        const icExecutable: string = Utils.getICExecutable(config.icBinFolder, config.useNetCore, config.isTimeTravelDebugging); // This may throw
        // See https://github.com/microsoft/AMBROSIA/blob/master/Samples/HelloWorld/TimeTravel-Windows.md
        let commonArgs: string[] = [`--instanceName=${config.icInstanceName}`, `--receivePort=${config.icReceivePort}`, `--sendPort=${config.icSendPort}`, `--log=${config.icLogFolder}`];
        let timeTravelDebuggingArgs: string[] = ["DebugInstance", ...commonArgs, `--checkpoint=${config.debugStartCheckpoint}`, `--currentVersion=${config.appVersion}`];
        if (config.debugTestUpgrade)
        {
            timeTravelDebuggingArgs.push("--testingUpgrade");
        }
        let normalArgs: string[] = [`--port=${config.icCraPort}`, ...commonArgs];
        
        // TODO: This version check is brittle: It would be better for the IC to support a --version parameter which makes it simply echo its version to the console (like "node --version").
        //       Having the binary set its 'File version' attribute then reading it with https://www.npmjs.com/package/win-version-info would only work on Windows.
        const isPostSledgeHammer: boolean = (Utils.getFileLastModifiedTime(icExecutable) > new Date("10/15/2020 04:52 PM")); // 'Last modified' of the release binary + 1 minute

        const optionalNormalArgs: { optionName: string, argPair: string }[] = 
        [
            { optionName: "logTriggerSizeInMB", argPair: `--logTriggerSize=${config.logTriggerSizeInMB}` }, // If not set locally (via ambrosiaConfig.json) we can just let the IC default to the registered value [either explicitly set or the default]
            // Note: Internally [to the IC], supplying --replicaNum (even to 0) automatically sets --activeActive to true [see ParseOptions() in \ambrosia\ImmortalCoordinator\Program.cs, and bug #173]
            { optionName: "isActiveActive", argPair: (config.isActiveActive ? "--activeActive" : "") }, // If set locally, MUST match the value specified when 'AddReplica' was run (we enforce this)
            { optionName: "replicaNumber", argPair: (config.isActiveActive && (config.replicaNumber > 0) ? `--replicaNum=${config.replicaNumber}` : "") }, // If set locally, MUST match the value specified when 'AddReplica' was run (we cannot enforce this)
            { optionName: "icLogStorageType", argPair: `--logStorageType=${config.icLogStorageType}` },
            { optionName: "icIPv4Address", argPair: `--IPAddr=${config.icIPv4Address}` },
            { optionName: "secureNetworkAssemblyName", argPair: `--assemblyName=${config.secureNetworkAssemblyName}` },
            { optionName: "secureNetworkClassName", argPair: `--assemblyClass=${config.secureNetworkClassName}` }
        ];

        optionalNormalArgs.forEach(oa => 
        { 
            if (config.isConfiguredLocally(oa.optionName) && (oa.argPair !== ""))
            {
                normalArgs.push(oa.argPair); 
            }
        });

        const args: string[] = config.isTimeTravelDebugging ? timeTravelDebuggingArgs : normalArgs;
        Utils.log(`Starting ${icExecutable}...`, null, Utils.LoggingLevel.Minimal);
        Utils.log(`Args: ${args.join(" ").replace(/--/g, "")}`, null, Utils.LoggingLevel.Minimal);
        Utils.logMemoryUsage();
        emitAppEvent(Messages.AppEventType.ICStarting);

        // The following starts the IC process directly (no visible console) and pipes both stdout/stderr to our stdout.
        // To aid in distinguishing the IC output from our own output we use the Utils.StandardOutputFormatter class.
        _icProcess = ChildProcess.spawn(config.useNetCore ? "dotnet" : icExecutable, 
            (config.useNetCore ? [icExecutable] : []).concat(args),
            { stdio: ["ignore", "pipe", "pipe"], shell: false, detached: false });
        
        if (!_icProcess.stdout || !_icProcess.stderr)
        {
            throw new Error(`Unable to redirect stdout/stderr for IC executable (${icExecutable})`);
        }

        const icSource: string = "[IC]";
        const icColor: Utils.ConsoleForegroundColors = Utils.ConsoleForegroundColors.Cyan;
        const icExceptionColor: Utils.ConsoleForegroundColors = Utils.ConsoleForegroundColors.Red;
        const CRA_INSTANCE_DOWN_MSG: string = "Possible reason: The connection-initiating CRA instance appears to be down";
        let outputFormatter: Utils.StandardOutputFormatter = new Utils.StandardOutputFormatter(icSource, icColor, icExceptionColor,
            [/FATAL ERROR/, new RegExp(CRA_INSTANCE_DOWN_MSG),
            /^Adding input:$/, /^Adding output:$/, /^restoring input:$/, /^restoring output:$/, // These detect self-connections
            /^Adding input:/, /^Adding output:/, /^restoring input:/, /^restoring output:/]); // These detect remote-connections
        
        let outputFormatterStream: Utils.StandardOutputFormatter = _icProcess.stdout.pipe(outputFormatter);
        
        if (Utils.canLogToConsole())
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
        outputFormatter.on("tokenFound", (token: string, line: string) =>
        {
            switch (token)  
            {
                case `/${CRA_INSTANCE_DOWN_MSG}/`:
                    // Upon attempting to connect to an instance that is down, or upon startup of an instance that has just been registered
                    // for the first time, the IC will report this "expected" CRA error [possibly more than once, consecutively]:
                    //
                    // System.InvalidOperationException: Nullable object must have a value.
                    //   at System.ThrowHelper.ThrowInvalidOperationException(ExceptionResource resource)
                    //   at CRA.ClientLibrary.CRAClientLibrary.<ConnectAsync>d__53.MoveNext()
                    // Possible reason: The connection-initiating CRA instance appears to be down or could not be found. Restart it and this connection will be completed automatically
                    if (config.isFirstStartAfterInitialRegistration)
                    {
                        // WORKAROUND: Log that this was an "expected error". This can be removed when bug #156 is resolved.
                        Utils.log(`Warning: Because this is the first start of the instance after initial registration, the prior ${icSource} 'System.InvalidOperationException' is expected`);
                    }
                    break;

                case "/FATAL ERROR/":
                    // Note: We leave the reporting (logging) of fatal errors to StandardOutputFormatter
                    onError("FatalError", null, true);
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

        _icProcess.on("exit", (code: number, signal: NodeJS.Signals) =>
        {
            if (_icStoppingDueToError && !code)
            {
                // If the IC fails with a FATAL error, it's almost certain that the process has already terminated (so "exit" has already been raised) 
                // in which case the stop() called by onError() will not result in "exit" firing (again). But just to be safe, we handle this unlikely 
                // case here (ie. the case of onError() -> (timer) -> stop() -> _icProcess.kill() -> "exit" event [in this case 'code' will be null]).
                // Note: This case can also happen when testing onError() [by calling it directly without an actual IC failure].
                code = 1; // ERROR_EXIT_CODE
            }
            if (code === 4294967295) // 0xFFFFFFFF
            {
                code = -1;
            }
            // Note: On Windows, exit code 0xC000013A means that the application terminated as a result of either a CTRL+Break or closing the console window.
            //       On Windows, an explicit kill (via either Task Manager or taskkill /F) will result in a code of '1' and a signal of 'null'.
            let byUserRequest: boolean = (Process.platform === "win32") && (code == 0xC000013A);
            let exitState: string = byUserRequest ? "at user request" : (code === null ? (signal === "SIGTERM" ? "normally" : `abnormally [signal: ${signal}]`) : `abnormally [exit code: ${code}]`);
            let icExecutableName: string = Path.basename(Utils.getICExecutable(config.icBinFolder, config.useNetCore, config.isTimeTravelDebugging));
            
            try
            {
                Utils.log(`${icExecutableName} stopped (${exitState})`);

                // Note: The user-provided AppEventType.ICStopped handler may also call stop(), but we can't rely on that
                stop(); 

                // Note: The user-provided AppEventType.ICStopped handler may throw; the code-gen'd dispatcher() will catch this, but the user may not be using it or may have modified it
                _icStoppedSignalled = true;
                emitAppEvent(Messages.AppEventType.ICStopped, code === null ? 0 : code);
            }
            catch (error: unknown)
            {
                Utils.log(Utils.makeError(error));
            }
        });

        Utils.log(`\'${config.icInstanceName}\'${config.replicaNumber > 0 ? ` (replica #${config.replicaNumber})` : ""} IC started (PID ${_icProcess.pid})`);
        emitAppEvent(Messages.AppEventType.ICStarted);
    }
    else
    {
        const icLocation: string = !_config.icIPv4Address || Utils.isLocalIPAddress(_config.icIPv4Address) ? "locally" : `on ${_config.icIPv4Address}`;
        Utils.log(`Warning: IC must be manually started ${icLocation} (because 'icHostingMode' is "${Configuration.ICHostingMode[_config.icHostingMode]}" and a remote 'icIPv4Address' was not specified)`);
    }
    
    connectToIC();
    
    return (appState);
}

/** 
 * Stops the Immortal Coordinator process (if it's running).\
 * This method also stops Node if 'exitApp' is true (the default).\
 * **WARNING:** Setting 'exitApp' to false requires that you **must** shutdown Node in your AppEventType.ICStopped handler.
 */
export function stop(exitApp: boolean = true): void
{
    if (!_icProcess)
    {
        if ((_config.icHostingMode === Configuration.ICHostingMode.Separated) && exitApp)
        {
            Utils.logMemoryUsage();
            Utils.closeOutputLog();

            // Note: Without calling Process.exit() Node will wait for any pending timers before exiting. As an alternative, we could
            //       keep track of all [our] pending timers and then unref() them here, but this would add considerable overhead.
            Process.exit(); // Stop Node.exe
        }
        return;
    }

    if (_outgoingMessageStream && (queueLength() !== 0))
    {
        // Not sure if this condition ever arises, but keeping this here as a canary
        Utils.log(`Warning: IC stopping while there are still ${queueLength()} outgoing messages in the queue`, null, Utils.LoggingLevel.Minimal);
    }

    logInFlightPostMethodsCount();

    if (_lbSendSocket)
    {
        if (_outgoingMessageStream)
        {
            // Attempt to flush the queue before exiting. We do this to handle the case of IC.sendMessage() being called right before IC.stop().
            // In this case, the resulting pending flushQueue() [that occurs via setImmediate()] may error when it attempts to push to the
            // [now closed] stream. By preemptively flushing (synchronously) we attempt to avert the error.
            try
            {
                flushQueue();
            }
            catch (error: unknown)
            {
                Utils.log(`Error: Unable to flush ${_outgoingMessageStream.queueLength} messages from the outgoing message queue (reason: ${Utils.makeError(error).message})`);
            }

            try
            {
                if (_outgoingMessageStream.canClose())
                {
                    _outgoingMessageStream.close();
                }
            }
            catch (error: unknown)
            {
                Utils.log(`Error: Unable to close _outgoingMessageStream (reason: ${Utils.makeError(error).message})`);
            }
        }

        try
        {
            _lbSendSocket.destroy();
        }
        catch (error: unknown)
        {
            Utils.log(`Error: Unable to destroy _lbSendSocket (reason: ${Utils.makeError(error).message})`);
        }
        _lbSendSocket = null;
    }

    if (_lbReceiveSocket)
    {
        try
        {
            _lbReceiveSocket.destroy();
        }
        catch (error: unknown)
        {
            Utils.log(`Error: Unable to destroy _lbReceiveSocket (reason: ${Utils.makeError(error).message})`);
        }
        _lbReceiveSocket = null;
    }
    
    if (_icProcess)
    {
        const IC_TERMINATION_TIMEOUT_IN_MS: number = 500;
        const TIMEOUT_EXIT_CODE: number = 101; // The IC failed to exit in a "reasonable" time (IC_TERMINATION_TIMEOUT_IN_MS)
        const ERROR_EXIT_CODE: number = 1;

        _icProcess.kill(); // This will raise the "exit" event for _icProcess, whose handler will raise AppEventType.ICStopped
        _icProcess = null;

        if (exitApp)
        {
            // We wait IC_TERMINATION_TIMEOUT_IN_MS to give the IC process a chance to terminate and emit its "exit" event (which raises
            // AppEventType.ICStopped), and also allows time to capture any final "straggler" output from either the IC or LB.
            setTimeout(() => 
            {
                const appExitCode: number = _icStoppedSignalled ? (_icStoppingDueToError ? ERROR_EXIT_CODE : 0) : TIMEOUT_EXIT_CODE;
                
                if (!_icStoppedSignalled)
                {
                    const icExecutableName: string = Path.basename(Utils.getICExecutable(_config.icBinFolder, _config.useNetCore, _config.isTimeTravelDebugging));
                    Utils.log(`Warning: ${icExecutableName} did not stop after ${IC_TERMINATION_TIMEOUT_IN_MS}ms`);

                    try
                    {
                        // Because the IC took longer than IC_TERMINATION_TIMEOUT_IN_MS to stop, we need to raise 
                        // AppEventType.ICStopped "manually" so that the app can do any necessary cleanup before we exit
                        emitAppEvent(Messages.AppEventType.ICStopped, _icStoppingDueToError ? ERROR_EXIT_CODE : TIMEOUT_EXIT_CODE);
                    }
                    catch (error: unknown)
                    {
                        Utils.log(Utils.makeError(error));
                    }
                }
                Utils.logMemoryUsage();
                Utils.closeOutputLog();

                // Note: Without calling Process.exit() Node will wait for any pending timers before exiting. As an alternative, we could
                //       keep track of all [our] pending timers and then unref() them here, but this would add considerable overhead.
                Process.exit(appExitCode); // Stop Node.exe
            }, IC_TERMINATION_TIMEOUT_IN_MS); 
        }
    }
}

/** Handler called if there are I/O errors with the IC, or if the IC reports a fatal error. */
function onError(source: string, error: Error | null, isFatalError?: boolean): void
{
    if (error)
    {
        Utils.logWithColor(Utils.ConsoleForegroundColors.Red, `${error.stack}`, `[IC:${source}]`, Utils.LoggingLevel.Minimal);

        /*
        // Commented out because in a "migration on the same machine" scenario, if a second LB gets an ECONNRESET because it can't connect to the IC
        // (or its connection is initially allowed but then rejected?) then it shouldn't kill the IC because the IC is still being used by the first LB.
        if (error.message.indexOf("ECONNRESET") !== -1)
        {
            // The IC terminated unexpectedly, so we can't continue [as an Immortal, the IC and the LB must share the same lifespan].
            // We do this just as a safety net since if the IC has terminated, the _icProcess "exit" event handler will also call stop().
            isFatalError = true;
        }
        */
    }

    if (isFatalError)
    {
        _icStoppingDueToError = true;
        // Allow some time for the process to terminate "naturally", then explicitly [try to] stop it.
        // Note: If the _icProcess "exit" event fires BEFORE this timeout elapses, stop() will be a no-op.
        setTimeout(() => stop(), 250); // If _icProcess hasn't already terminated, stop() will raise AppEventType.ICStopped via the _icProcess "exit" event handler
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
    // We only monitor IC output when running the IC integrated
    if (!_config.isIntegratedIC)
    {
        return;
    }

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

let _waitingToConnectToICSendPort: boolean = true;
let _waitingToConnectToICReceivePort: boolean = true;
let _lastICSendPortConnectAttemptTime: number = 0;
let _lastICReceivePortConnectAttemptTime: number = 0;
let _minimumConnectionRetryDelayInMs: number = 3000; // Retry connections no more frequently than this

/** Connects (asynchronously) to the IC's send/receive sockets and, when both are connected, starts processing messages. */
// Note: Net.connect() and Socket.connect() can have different timeout behavior depending on the version of NodeJS, the OS, and (possibly)
//       the failure reason (eg. ECONNREFUSED vs ENOTFOUND), so we use our own retry timeout logic to get [more] consistent behavior.
function connectToIC(): void
{
    const hostName: string = (!_config.icIPv4Address || Utils.isLocalIPAddress(_config.icIPv4Address)) ? "localhost" : _config.icIPv4Address;

    /** [Local function] Connects to the IC's receive port (this is the port we will send on). */
    function connectLBSendSocket(): void
    { 
        _lastICReceivePortConnectAttemptTime = Date.now();

        if (_lbSendSocket === null)
        {
            Utils.log(`LB Connecting to IC receive port (${hostName}:${_config.icReceivePort})...`);
            
            // Note: When running in the "Integrated" icHostingMode, [at least] the first connection attempt will 
            //       typically fail with "connect ECONNREFUSED 127.0.0.1:<port>" because the IC hasn't yet created the port
            _lbSendSocket = Net.connect(_config.icReceivePort, hostName); // Note: connect() is asynchronous

            _lbSendSocket.once("connect", () =>
            {
                Utils.log(`LB connected to IC receive port (${hostName}:${_config.icReceivePort})`);
                _waitingToConnectToICReceivePort = false;
                _minimumConnectionRetryDelayInMs = 250; // Once we're connected to the IC receive port, the IC send port should be available to connect to immediately
                onConnectionMade();
            });
        
            _lbSendSocket.on("error", (error: Error) =>
            {
                if (_waitingToConnectToICReceivePort)
                {
                    const timeSinceLastConnectAttemptInMs: number = Date.now() - _lastICReceivePortConnectAttemptTime;
                    const retryDelayInMs: number = Math.max(0, _minimumConnectionRetryDelayInMs - timeSinceLastConnectAttemptInMs);
                    Utils.log(`LB retrying to connect to IC receive port (in ${retryDelayInMs}ms); last attempt failed (reason: ${error.message})...`);
                    emitAppEvent(Messages.AppEventType.WaitingToConnectToIC);
                    setTimeout(() => connectLBSendSocket(), retryDelayInMs);
                    return;
                }
                onError("LBSend->ICReceive Socket", error);
            });
        }
        else
        {
            // Typically, this will succeed
            _lbSendSocket.connect(_config.icReceivePort, hostName); // Note: connect() is asynchronous
        }
    }

    /** [Local function] Connects to the IC's send port (this is the port we will receive on). */
    function connectLBReceiveSocket(): void
    {
        _lastICSendPortConnectAttemptTime = Date.now();

        if (_lbReceiveSocket === null)
        {
            Utils.log(`LB connecting to IC send port (${hostName}:${_config.icSendPort})...`);

            // Note: When running in the "Integrated" icHostingMode, since we're already connected to the IC's receive port, 
            //       the send port connection attempt will typically always succeed on the first try
            _lbReceiveSocket = Net.connect(_config.icSendPort, hostName); // Note: connect() is asynchronous

            // Add handlers
            _lbReceiveSocket.once("connect", () =>
            {
                Utils.log(`LB connected to IC send port (${hostName}:${_config.icSendPort})`);
                _waitingToConnectToICSendPort = false;
                onConnectionMade();
            });
        
            _lbReceiveSocket.on("error", (error: Error) =>
            {
                if (_waitingToConnectToICSendPort)
                {
                    const timeSinceLastConnectAttemptInMs: number = Date.now() - _lastICSendPortConnectAttemptTime;
                    const retryDelayInMs: number = Math.max(0, _minimumConnectionRetryDelayInMs - timeSinceLastConnectAttemptInMs);
                    Utils.log(`LB retrying to connect to IC send port (in ${retryDelayInMs}ms); last attempt failed (reason: ${error.message})...`);
                    emitAppEvent(Messages.AppEventType.WaitingToConnectToIC);
                    setTimeout(() => connectLBReceiveSocket(), retryDelayInMs);
                    return;
                }
                onError("LBReceive->ICSend Socket", error);
            });
        }
        else
        {
            // Typically, this will succeed
            _lbReceiveSocket.connect(_config.icSendPort, hostName); // Note: connect() is asynchronous
        }
    }

    /** [Local function] Checks if both connections (send and receive socket) have been made, and - if so - proceeds to the next step of the LB startup. */
    function onConnectionMade(): void
    {
        if (!_waitingToConnectToICReceivePort)
        {
            if (_waitingToConnectToICSendPort)
            {
                // We've connected to the IC's receive port (but not the send port), so now we can connect to the IC's send port (our receive socket)
                connectLBReceiveSocket();
            }
            else
            {
                // Both connections have been made
                const maxQueueSizeInBytes: number = Configuration.loadedConfig().lbOptions.maxMessageQueueSizeInMB * 1024 * 1024;
                _outgoingMessageStream = new Streams.OutgoingMessageStream(_lbSendSocket!, (error: Error) => onError("OutgoingMessageStream", error), true, maxQueueSizeInBytes); // NonNullAssertion [for 'strictNullChecks']
                onICConnected();
                emitAppEvent(Messages.AppEventType.ICConnected);
            }
        }
    }

    // We must connect to the IC's ports in order: receive port first, then send port.
    // We do this to match the IC's accept() order for these sockets, and to avoid the corner case of 2 LB's connecting to one port each on the same IC (see bug #182)].
    connectLBSendSocket();
}

/** Called when the LB and IC are connected (on both ports). Sets up the data handler for the receive socket. */
function onICConnected(): void
{
    const INITIAL_PAGE_BUFFER_SIZE: number = 16 * 1024 * 1024; // 16 MB [the IC has an 8 MB limit for a log page size]
    const ONE_MB: number = 1024 * 1024;
    const MEMORY_LOGGING_THRESHOLD: number = ONE_MB * 256;
    let receivingCheckpoint: boolean = false;
    let checkpointBytesTotal: number = 0;
    let checkpointBytesRemaining: number = 0;
    let incomingCheckpoint: Streams.IncomingCheckpoint;
    let checkpointStream: Stream.Writable; // This is where we will write received checkpoint data
    let pageReceiveBuffer: Buffer = Buffer.alloc(INITIAL_PAGE_BUFFER_SIZE); // This is where we will accumulate bytes until we have [at least] 1 complete log page [the buffer will auto-grow/shrink as needed]
    let bufferOffset: number = 0; // The current write position in pageReceiveBuffer
    let bufferShrinkTimer: NodeJS.Timeout; // Timer used to auto-shrink pageReceiveBuffer after auto-grow
    let checkpointRestoreStartTime: number = 0;
    let totalICBytesReceived: number = 0;
    let nextMemoryLoggingThreshold: number = MEMORY_LOGGING_THRESHOLD;

    _lbReceiveSocket!.on("data", onICData); // NonNullAssertion [for 'strictNullChecks']

    /** [Local function] Called whenever we receive a chunk of data from the IC. The chunk will never be more than 65536 bytes. */
    function onICData(data: Buffer): void
    {
        if (Utils.isTraceFlagEnabled(Utils.TraceFlag.MemoryUsage))
        {
            if (totalICBytesReceived + data.length > Number.MAX_SAFE_INTEGER) // Unlikely (8 petabytes), but possible
            {
                totalICBytesReceived = 0;
                nextMemoryLoggingThreshold = MEMORY_LOGGING_THRESHOLD;
            }
            totalICBytesReceived += data.length;
            if (totalICBytesReceived > nextMemoryLoggingThreshold)
            {
                nextMemoryLoggingThreshold += MEMORY_LOGGING_THRESHOLD;
                Utils.logMemoryUsage();
            }
        }

        processIncomingICData(data);
    }

    /** [Local function] Called when we've received the last 'chunk' of checkpoint data from the IC. */
    function onCheckpointEnd(finalCheckpointChunk: Uint8Array, remainingBuffer?: Buffer)
    {
        receivingCheckpoint = false;
        checkpointBytesRemaining = 0;

        // Wait for checkpointStream to finish
        // Note: To be sure that we don't start reading/processing [additional] log pages until the checkpoint has been fully received [restored], we
        //       pause the inbound socket from the IC until the checkpointStream 'finish' event handler completes [which can include calling user-code]
        _lbReceiveSocket!.pause(); // NonNullAssertion [for 'strictNullChecks']
        checkpointStream.on("finish", (error?: Error) => 
        {
            incomingCheckpoint.onFinished(error);
            Utils.log(`Checkpoint (${checkpointBytesTotal} bytes) ${error ? `restore failed (reason: ${error.message})` : `restored (in ${Date.now() - checkpointRestoreStartTime}ms)`}`);
            if (!error)
            {
                logInFlightPostMethodsCount();
                emitAppEvent(Messages.AppEventType.CheckpointLoaded, checkpointBytesTotal);
            }

            // The 'finalCheckpointChunk' may have been part of a buffer that ALSO contained part (or all) of the next
            // log page in it (after the checkpoint chunk), so it's now safe to finish processing that part of the buffer
            if (remainingBuffer && (remainingBuffer.length > 0))
            {
                processIncomingICData(remainingBuffer); // Note: This will cause re-entrancy into processIncomingICData()
            }

            _lbReceiveSocket!.resume(); // NonNullAssertion [for 'strictNullChecks']
        });
        checkpointStream.end(finalCheckpointChunk); // This will make checkpointStream [synchronously] emit a 'finish' event which will [synchronously] invoke the 'finish' handler above
    }

    /** 
     * [Local function] [Re]Schedules an operation to shrink pageReceiveBuffer to its original size, but only if the buffer is empty.
     * The delayInMs defaults to 5 minutes.
     */
    function scheduleBufferAutoShrink(delayInMs: number = 5 * 60 * 1000): void
    {
        if (!bufferShrinkTimer)
        {
            bufferShrinkTimer = setTimeout(() =>
            {
                if (bufferOffset === 0)
                {
                    if (pageReceiveBuffer.length > INITIAL_PAGE_BUFFER_SIZE)
                    {
                        pageReceiveBuffer = Buffer.alloc(INITIAL_PAGE_BUFFER_SIZE);
                        Utils.log(`Log page buffer decreased to ${INITIAL_PAGE_BUFFER_SIZE} bytes (${(INITIAL_PAGE_BUFFER_SIZE / (1024 * 1024)).toFixed(2)} MB)`, null, Utils.LoggingLevel.Minimal);
                    }
                }
                else
                {
                    // We can't shrink yet (the buffer is still being processed), so try again later
                    bufferShrinkTimer.refresh();
                    // Utils.log(`DEBUG: Unable to shrink buffer (reason: buffer not empty)`);
                }
            }, delayInMs);
        }
        else
        {
            // The timer is already running, so restart it
            bufferShrinkTimer.refresh();
        }
    }

    /** 
     * [Local function] Reads the log pages (and checkpoint data) received from the IC. Checkpoint data can be large, and is
     * sent without a header, so we read it in a different "mode" to the way we read regular log pages. Note the the data (chunk)
     * received from the IC will be no larger than 65536 bytes, but it can still potentially contain hundreds of log pages.
     */
    function processIncomingICData(data: Buffer): void
    {
        // If we're in the process of receiving checkpoint data, we follow a different path than the "normal" sequence-of-log-pages path
        if (receivingCheckpoint)
        {
            if (checkpointBytesRemaining > data.length)
            {
                // The data is all checkpoint data
                checkpointStream.write(data); // If this returns false, it will just buffer
                checkpointBytesRemaining -= data.length;
                return; // Wait for more data (via the _lbReceiveSocket "data" event)
            }
            else
            {
                // The data includes the tail of the checkpoint (and possibly part, or all, of the next log page)
                const checkpointTailChunk: Uint8Array = new Uint8Array(checkpointBytesRemaining);
                data.copy(checkpointTailChunk, 0, 0, checkpointBytesRemaining);
            
                if (checkpointBytesRemaining < data.length)
                {
                    // data contains part (or all) of the next log page, so we pass that along to onCheckpointEnd() to handle when it's finished
                    const remainingBufferLength: number = data.length - checkpointBytesRemaining;
                    const remainingBuffer: Buffer = Buffer.alloc(remainingBufferLength);
                    data.copy(remainingBuffer, 0, checkpointBytesRemaining, data.length);

                    onCheckpointEnd(checkpointTailChunk, remainingBuffer);
                }
                else
                {
                    // data only contains checkpoint data (checkpointBytesRemaining == data.length), so that's all we need to process
                    onCheckpointEnd(checkpointTailChunk);
                }
                return; // Wait for the next log page (via the _lbReceiveSocket "data" event)
            }
        }

        // TESTING (2 log pages, the first with 2 messages, the second with one message)
        // let logRec1: Buffer = Buffer.from([0x96, 0x61, 0xd1, 0xdc, 0x1c, 0x00, 0x00, 0x00, 0x02, 0x0b, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x02, 0x0b, 0x02, 0x09]);
        // let logRec2: Buffer = Buffer.from([0x96, 0x61, 0xd1, 0xdc, 0x1a, 0x00, 0x00, 0x00, 0x02, 0x0b, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x02, 0x0b]);
        // data = Buffer.alloc(logRec1.length + logRec2.length, Buffer.concat([logRec1, logRec2]));

        // Note: There is no way to know [from the message header] the instance name of the IC that sent the data
        if (Utils.canLog(Utils.LoggingLevel.Verbose))
        {
            let showBytes: boolean = _config.lbOptions.debugOutputLogging; // For debugging
            Utils.log(`Received data from IC (${data.length} bytes)` + (showBytes ? `: ${Utils.makeDisplayBytes(data)}` : ""));
        }

        pageReceiveBuffer.set(data, bufferOffset); // Note: set() will throw a "RangeError: offset is out of bounds" if the operation causes buffer overflow
        bufferOffset += data.length;
        let logPageLength: number = Messages.readLogPageLength(pageReceiveBuffer, bufferOffset);

        // Limited check for pageReceiveBuffer corruption (eg. an "interior" log page data chunk being written [erroneously] to the start of pageReceiveBuffer).
        // Also, the IC is using negative log page lengths (in a persisted page) to indicate that the xxHash64 checksum was used. The IC should always be
        // sendind the LB an abs(logPageLength), but we check that to be safe.
        if (logPageLength < -1)
        {
            const headerBytes: string = Utils.makeDisplayBytes(pageReceiveBuffer, 0, 24);
            throw new Error(`The log page header has an invalid logPageLength (${logPageLength}); header bytes: [${headerBytes}], bufferOffset: ${bufferOffset}, last log page ID: ${Messages.lastLogPageSequenceID()}, last complete log page ID: ${Messages.lastCompleteLogPageSequenceID()}`);
        }

        // If needed, auto-grow pageReceiveBuffer
        if (logPageLength > pageReceiveBuffer.length)
        {
            // Note: Because Node reads TCP data in chunks (of up to 64 KB) which are not aligned with log page boundaries (ie. the IC's data
            //       chunks can include part (or all) of the next log page), we allocate space (64 KB) for an additional "full" chunk.
            //       For example, if logPageLength is 65537 bytes, it could arrive in 2 chunks each of 65536 bytes (with 65535 bytes in the
            //       second chunk belonging to the next log page). If we didn't add space for that "additional" chunk, we wouldn't have space
            //       to receive all the chunks for the first log page (or to receive the start of the next log page).
            //       Aside: "The current unit of data transfer in Node is 64 KB and is hard-coded" (source: https://github.com/libuv/libuv/issues/1217).
            //              For example: https://github.com/nodejs/node/blob/926152a38c8fbe6c0b016ea36edb0b219c0fc7fd/deps/uv/src/win/tcp.c#L1060
            const newPageReceiveBuffer = Buffer.alloc(logPageLength + (64 * 1024));
            pageReceiveBuffer.copy(newPageReceiveBuffer, 0, 0, bufferOffset);
            pageReceiveBuffer = newPageReceiveBuffer;
            Utils.log(`Log page buffer increased to ${pageReceiveBuffer.length} bytes (${(pageReceiveBuffer.length / (1024 * 1024)).toFixed(2)} MB)`, null, Utils.LoggingLevel.Minimal);
            scheduleBufferAutoShrink(); // Because the large log page [that caused auto-grow] may be atypical, we schedule an auto-shrink [to avoid hogging memory indefinitely]
        }

        // First, process any previous log page(s) we had to interrupt because it was generating too much outgoing data (so we had to halt processing of the page to let I/O with the IC interleave)
        let unprocessedPageCount: number = Messages.processInterruptedLogPages(_config);
    
        // Process all the complete log pages (if any) in pageReceiveBuffer
        while ((logPageLength = Messages.getCompleteLogPageLength(pageReceiveBuffer, bufferOffset)) !== -1)
        {
            // Dispatch all messages in the log page
            checkpointBytesTotal = Messages.processLogPage(pageReceiveBuffer, _config);

            if (checkpointBytesTotal >= 0) // An empty checkpoint is still a valid checkpoint
            {
                // We just read a 'Checkpoint' message (which will be the only message in the log page)
                // so we need to switch "modes" (receivingCheckpoint = true) to read the checkpoint data
                receivingCheckpoint = true;
                checkpointBytesRemaining = checkpointBytesTotal;
                incomingCheckpoint = _config.checkpointConsumer();
                checkpointStream = incomingCheckpoint.dataStream;
                checkpointRestoreStartTime = Date.now();
                // @ts-tactical-any-cast: Suppress error "Type 'null' is not assignable to type 'AmbrosiaAppState'. ts(2322)" [because we use 'strictNullChecks']
                _appState = null as any; // The app MUST call initializeAmbrosiaState() after restoring the checkpoint, and we want things to break if it doesn't

                // Rather than, say, encoding the size as the first 8 bytes of the stream, we use an AppEvent
                // (in the C# LB this is handled automatically by [DataContract], so we can't do the same)
                emitAppEvent(Messages.AppEventType.IncomingCheckpointStreamSize, checkpointBytesTotal);
            }
        
            if (logPageLength < bufferOffset)
            {
                // We have part of the next log page(s) in pageReceiveBuffer, so truncate to just that portion
                // [the truncated part is the log page that has already been handled by processLogPage()]
                pageReceiveBuffer.copyWithin(0, logPageLength, bufferOffset);
                bufferOffset -= logPageLength;

                if (receivingCheckpoint)
                {
                    // Rather than part of the next log page, we have part (or all) of the checkpoint data (and maybe some of the next log page after that)
                    if (checkpointBytesTotal <= bufferOffset)
                    {
                        // We have already read ALL of the checkpoint data
                        let checkpointChunk: Uint8Array = new Uint8Array(checkpointBytesTotal);
                        pageReceiveBuffer.copy(checkpointChunk, 0, 0, checkpointBytesTotal);

                        if (checkpointBytesTotal < bufferOffset)
                        {
                            pageReceiveBuffer.copyWithin(0, checkpointBytesTotal, bufferOffset);
                            bufferOffset -= checkpointBytesTotal;
                            // pageReceiveBuffer will now contain part (or all) of the next log page
                        }
                        else
                        {
                            // checkpointBytesTotal == bufferOffset, so pageReceiveBuffer only contains checkpoint data
                            bufferOffset = 0; // Empty the buffer
                        }
                        const remainingBuffer: Buffer = Buffer.alloc(bufferOffset);
                        pageReceiveBuffer.copy(remainingBuffer, 0, 0, bufferOffset);
                        bufferOffset = 0; // Empty the buffer [this is safe, since remainingBuffer now contains anything that was left in the buffer]
                        onCheckpointEnd(checkpointChunk, remainingBuffer);
                        break; // Exit from the normal log page 'while' loop [any remaining data in pageReceiveBuffer will be processed by onCheckpointEnd() when it's finished]
                    }
                    else
                    {
                        // We have only read PART of the checkpoint data: it will take additional reads to receive it all
                        checkpointStream.write(pageReceiveBuffer.slice(0, bufferOffset)); // If this returns false, it will just buffer
                        checkpointBytesRemaining -= bufferOffset;
                        bufferOffset = 0; // Empty the buffer
                        break; // Exit from the normal log page 'while' loop [received checkpoint data will continue to be processed via the 'receivingCheckpoint = true' path]
                    }
                }
            }
            else
            {
                // We have a single, complete log page
                bufferOffset = 0; // Empty the buffer [this will terminate the log page 'while' loop]
            }
        }

        // If we've fallen behind on processing log pages, then pause the incoming message stream [the incoming data will still be buffered - just by Node, not us].
        // Doing this gives the outgoing message stream an opportunity to empty (especially during recovery, when we are rapidly inundated with log pages from the IC).
        unprocessedPageCount = Messages.interruptedLogPageBacklogCount();
        if (unprocessedPageCount > 0)
        {
            if (_lbReceiveSocket && !_lbReceiveSocket.isPaused())
            {
                // Note: According to https://www.derpturkey.com/node-js-socket-backpressure-in-paused-mode-2/, when a socket is paused
                //       it should exert backpressure on the the sender (the IC) [via TCP flow control] to slow the sender down, so the
                //       IC should NOT cause the _lbReceiveSocket stream to buffer indefinitely (potentially leading to an OOM condition).
                _lbReceiveSocket.pause();
                Utils.traceLog(Utils.TraceFlag.LogPageInterruption, `Incoming message stream paused (${unprocessedPageCount} log pages are backlogged)`);
                setImmediate(() => resumeReadingFromIC()); // TODO: Rather than polling, it's probably more efficient to use: _outgoingMessageStream.once("drain", resumeReadingFromIC);
            }
        }

        /** [Local function] Checks if the outgoing message stream is now empty, and if it is, resumes the incoming message stream. Otherwise, re-schedules the check. */
        function resumeReadingFromIC(): void
        {
            if (_lbReceiveSocket && _lbReceiveSocket.isPaused())
            {
                const isOutgoingMessageStreamEmpty: boolean = !isOutgoingMessageStreamGettingFull(0);
                if (isOutgoingMessageStreamEmpty)
                {
                    _lbReceiveSocket.resume();
                    Utils.traceLog(Utils.TraceFlag.LogPageInterruption, `Incoming message stream resumed (outgoing message stream is now empty)`);
                }
                else
                {
                    // Allow more time for the outgoing stream to empty, then check again
                    setImmediate(() => resumeReadingFromIC());
                }
            }
        }
    }
}

/** Raises the specified event, which can be handled in the app's MessageDispatcher(). */
export function emitAppEvent(eventType: Messages.AppEventType, ...args: any[])
{
    if (eventType === Messages.AppEventType.BecomingPrimary)
    {
        // The 'isFirstStartAfterInitialRegistration' condition only applies during the startup phase of the newly registered instance
        Configuration.loadedConfig().isFirstStartAfterInitialRegistration = false;
    }
    _config.dispatcher(new Messages.AppEvent(eventType, ...args));
}

/** Returns the number of post methods that are currently in-flight (ie. sent but no response yet received). */
export function inFlightPostMethodsCount(): number
{
    const inFlightPostMethodsCount: number = _appState.__ambrosiaInternalState.inFlightCallIDs().length;
    return (inFlightPostMethodsCount);
}

/** Logs the count of currently in-flight (ie. sent but no response yet received) post methods, if there are any. */
function logInFlightPostMethodsCount(): void
{
    const methodCount: number = inFlightPostMethodsCount();
    if (methodCount > 0)
    {
        Utils.log(`There are ${methodCount} in-flight post methods`, null, Utils.LoggingLevel.Minimal);
    }
}
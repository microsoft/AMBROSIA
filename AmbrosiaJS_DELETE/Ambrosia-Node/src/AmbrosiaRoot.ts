// Module for objects [other than "namespaces"] that live at the root of the 'Ambrosia' "namespace".
import Process = require("process");
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as AmbrosiaStorage from "./Storage";
import * as Configuration from "./Configuration"; 
import * as IC from "./ICProcess";
import * as Utils from "./Utils/Utils-Index";

/** Base class that an application state class must derive from. */
export class AmbrosiaAppState
{
    /** [Internal] **Warning:** For internal use only. Do not modify this member. */
    __ambrosiaInternalState: AmbrosiaInternalState = new AmbrosiaInternalState();
}

/** [Internal] Class representing internal Ambrosia state that is part of the serialized (checkpointed) application state [via the AmbrosiaAppState base class]. */
export class AmbrosiaInternalState
{
    private _lastCallID: number = 0;

    constructor(existingState?: AmbrosiaInternalState)
    {
        if (existingState)
        {
            this._lastCallID = existingState._lastCallID;
        }
    }

    /** Returns the next [unique] method call ID (starting at 1). */
    getNextCallID(): number
    {
        return (++this._lastCallID);
    }
}

/** The version of the JavaScript language binding (matches the ambrosia-node package version). */
export function languageBindingVersion(): string
{
    return ("0.0.73"); // Be careful when editing this because build.ps1 looks for "(X.X.X);" [in this file], where 'X' can be any integer
}

/** The modes of operation that the language binding (LB) can be initalized for. */
export enum LBInitMode
{
    /** The language binding will be initialized for running an Immortal instance. This is the default mode. */
    Normal,
    /** The language binding will be initialized for running code generation. */
    CodeGen
}

/** 
 * Loads either 'ambrosiaConfig.json' (the default), or the alternate config file specified via the "ambrosiaConfigFile=xxx" command-line parameter.\
 * Also does some asynchronous initialization tasks.\
 * If any failure is encountered, the error will be logged and the process will exit.
 */
export async function initializeAsync(initMode?: LBInitMode): Promise<void>;
/** [Internal] This overload is for internal use only. */
export async function initializeAsync(initMode: LBInitMode, completionWrapper: Utils.AsyncCompleteWrapper): Promise<void>;
export async function initializeAsync(initMode: LBInitMode = LBInitMode.Normal, completionWrapper?: Utils.AsyncCompleteWrapper): Promise<void>
{
    let ambrosiaConfigFileName: string = null;
    let configFile: Configuration.AmbrosiaConfigFile = null;
    let initializationError: Error = null;

    try
    {
        ambrosiaConfigFileName = Utils.getCommandLineArg("ambrosiaConfigFile", Configuration.AmbrosiaConfigFile.DEFAULT_FILENAME);
        configFile = new Configuration.AmbrosiaConfigFile(ambrosiaConfigFileName);

        if (configFile.lbOptions.enableTypeScriptStackTraces)
        {
            // Enables an Error stack trace to refer to TS files/locations, not JS files/locations (see https://www.npmjs.com/package/source-map-support)
            require("source-map-support").install(); 
        }

        Utils.log(`Ambrosia configuration loaded from '${ambrosiaConfigFileName}'`);

        await configFile.initializeAsync();

        if (initMode === LBInitMode.Normal)
        {
            IC.setRemoteInstanceNames(await AmbrosiaStorage.getRemoteInstancesAsync(configFile.instanceName, configFile.lbOptions.deleteRemoteCRAConnections));
            // Alternative: Running awaited methods in parallel (for performance)
            // let voidResult: void;
            // [voidResult, IC._remoteInstanceNames] = await Promise.all([configFile.initializeAsync(), AmbrosiaStorage.getRemoteInstancesAsync(configFile.instanceName, configFile.lbOptions.deleteRemoteCRAConnections)]);
        }
    }
    catch (error)
    {
        initializationError = error as Error;
        Utils.tryLog(`Unable to load configuration file '${ambrosiaConfigFileName}' (reason: ${initializationError.message})`, "Ambrosia initialization error");
        Process.exit(-1);
    }
    finally
    {
        // If needed, signal that initialization is complete
        completionWrapper?.complete(initializationError);
    }
}

/** A callback-style wrapper for initializeAsync(). */
export function initialize(onComplete: (error?: Error) => void, initMode: LBInitMode = LBInitMode.Normal)
{
    // Start - but don't wait for - initializeAsync() [which consequently MUST have its own try/catch to handle errors]
    initializeAsync(initMode, new Utils.AsyncCompleteWrapper(onComplete));
}
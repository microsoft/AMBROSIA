// Module for objects [other than "namespaces"] that live at the root of the 'Ambrosia' "namespace".
import Process = require("process");
import OS = require("os");
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as AmbrosiaStorage from "./Storage";
import * as Configuration from "./Configuration"; 
import * as IC from "./ICProcess";
import * as Messages from "./Messages";
import * as Utils from "./Utils/Utils-Index";

/** Throws if the specified 'appStateConstructor' does not take exactly 1 parameter. */
export function checkAppStateConstructor<T extends AmbrosiaAppState>(appStateConstructor: new (restoredAppState?: T) => T): void
{
    const constructorParams: string = appStateConstructor.toString().split(/constructor\s*[^\(]*\(\s*([^\)]*)\)/m)[1];
    const constructorParamCount: number = constructorParams.replace(/\s+/g, "").replace(/\/\*.*?\*\//g, "")?.split(",").filter(p => p.length > 0).length; // Remove "/*...*/" comments
    
    if (constructorParamCount !== 1)
    {
        throw new Error(`The supplied 'appStateConstructor' takes ${constructorParamCount} parameters when 1 was expected; add a single parameter ('restoredAppState') of the same type as your app-state class ('${appStateConstructor.name}')`);
    }
}

/** Base class that an application state class must derive from. */
export class AmbrosiaAppState
{
    /** 
     * [Internal] **WARNING:** For internal use only.\
     * Do not modify this member or call its methods. 
     */
    // Note: Technically, all of AmbrosiaInternalState's properties and methods could live directly in AmbrosiaAppState,
    //       but this would result in many more "internal-only" members being visible on the user's appState [which derives
    //       from AmbrosiaAppState]. So instead we minimize the visible members to just this one.
    __ambrosiaInternalState: AmbrosiaInternalState;

    /** The 'restoredAppState' parameter can be null (or undefined), but only when the class is **not** being constructed as part of either restoring a checkpoint or upgrading app state. */
    // Note: We don't make the 'restoredAppState' parameter optional because it helps to inform the signature of the constructor of a class that derives from AmbrosiaAppState.
    constructor(restoredAppState: AmbrosiaAppState | undefined)
    {
        this.__ambrosiaInternalState = new AmbrosiaInternalState(restoredAppState?.__ambrosiaInternalState);
    }

    /** 
     * [Virtual] Called by upgrade() to convert the current app state into the upgraded app state. Returns the new app state. Must be overidden in the derived class.\
     * Typically, all the overidden method will do is call a static 'factory' method on the new app state class that takes an old app state instance and creates a new app state instance,
     * eg. "static fromPriorAppState(oldState: AppState): NewAppState".
     */
    convert(): AmbrosiaAppState
    {
        throw new Error("The derived class must override the convert() method");
    }

    /** 
     * [Sealed] Upgrades an app state derived from AmbrosiaAppState by calling its convert() method, which **must** be overridden in the derived class.\
     * Should be called when AppEventType.UpgradeState becomes signalled.\
     * **WARNING:** Do not override this method.
     */
    upgrade<TNewState extends AmbrosiaAppState>(appStateConstructor: new (restoredAppState?: TNewState) => TNewState): TNewState
    {
        const upgradedAppState: TNewState = this.convert() as TNewState;

        this.__ambrosiaInternalState.upgradeCalled = true;

        // Depending on how convert() instantiates TNewState, it may not maintain the existing __ambrosiaInternalState.
        // So to be safe, we simply copy it over directly.
        upgradedAppState.__ambrosiaInternalState = this.__ambrosiaInternalState;

        // Note: Even though convert() will [likely] return a new TNewState, we still have to call IC.initializeAmbrosiaState()
        //       [which will instantiate another TNewState instance from upgradedAppState]. We do this for 2 reasons:
        //       1) To do runtime type checks of the instantiated TNewState.
        //       2) So that the IC can update its own _appState reference.
        const newState: TNewState = IC.initializeAmbrosiaState<TNewState>(appStateConstructor, upgradedAppState);
        return (newState);
    }
}

/** 
 * Type of a list that tracks details about all in-flight (ie. sent but no result yet received) post methods.\
 * Note: We use a Map because it performs better than Object for frequent additions/removals.
 */
type InFlightPostMethodsList = Map<number, IC.InFlightPostMethodDetails>; // Key = callID

/** [Internal] Class representing internal Ambrosia state that is part of the serialized (checkpointed) application state [via the AmbrosiaAppState base class]. */
export class AmbrosiaInternalState
{
    private _stateGUID: string; // Used to check if the user accidentally reassigns their "_appState" reference. Initialized in constructor.
    private _lastPostCallID: number = 0;
    private _inFlightPostMethods: InFlightPostMethodsList = new Map<number, IC.InFlightPostMethodDetails>(); // Key = callID
    private _appStateUpgradeCalled: boolean = false;

    /** Runtime flag indicating whether AmbrosiaAppState.upgrade() has been called. */
    get upgradeCalled(): boolean { return (this._appStateUpgradeCalled); }
    set upgradeCalled(value: boolean) { this._appStateUpgradeCalled = value; }

    constructor(restoredAppState?: AmbrosiaInternalState)
    {
        if (!restoredAppState)
        {
            // Assign a unique identifying ID for this AmbrosiaInternalState instance. 
            // Unless checkpoints are deleted, this ID will remain same for forever (enduring across checkpoint save/restore).
            this._stateGUID = Utils.Guid.newGuid();
        }
        else
        {
            // This is the case where the state is being rehydrated from a checkpoint (or from an upgrade of app state)
            this._stateGUID = restoredAppState._stateGUID;
            this._lastPostCallID = restoredAppState._lastPostCallID;
            this._inFlightPostMethods = new Map<number, IC.InFlightPostMethodDetails>();
            for (let [callID, details] of restoredAppState._inFlightPostMethods.entries())
            {
                this._inFlightPostMethods.set(callID, IC.InFlightPostMethodDetails.createFrom(details));
            }
            this._appStateUpgradeCalled = restoredAppState._appStateUpgradeCalled; // Not strictly needed since this is a runtime-only flag, but included for completeness
        }
    }

    /** Returns the next [unique] post method call ID (starting at 1). */
    getNextPostCallID(): number
    {
        return (++this._lastPostCallID);
    }

    /** Adds the InFlightPostMethodDetails for the specified post method callID. */
    pushInFlightPostCall(callID: number, callDetails: IC.InFlightPostMethodDetails): void
    {
        if (this._inFlightPostMethods.has(callID))
        {
            throw new Error(`Post method callID ${callID} is already in-flight`);
        }
        this._inFlightPostMethods.set(callID, callDetails);
    }

    /** Returns (and removes) the InFlightPostMethodDetails for the specified post method callID. */
    popInFlightPostCall(callID: number): IC.InFlightPostMethodDetails
    {
        if (!this._inFlightPostMethods.has(callID))
        {
            throw new Error(`Post method callID ${callID} is not in-flight`);
        }
        const callDetails: IC.InFlightPostMethodDetails = Utils.assertDefined(this._inFlightPostMethods.get(callID));
        this._inFlightPostMethods.delete(callID);
        return (callDetails);
    }

    /** Returns the InFlightPostMethodDetails for the specified post method callID. */
    getInFlightPostCall(callID: number): IC.InFlightPostMethodDetails | undefined
    {
        const callDetails: IC.InFlightPostMethodDetails | undefined = this._inFlightPostMethods.get(callID);
        return (callDetails);
    }

    /** Returns a list of callID's for all currently in-flight post methods. */
    inFlightCallIDs(): number[]
    {
        const callIDs: number[] = [...this._inFlightPostMethods.keys()];
        return (callIDs);
    }

    /** Returns true only if the AmbrosiaInternalState of the supplied appState is the same as this AmbrosiaAppState instance. */
    isSameInstance(appState: AmbrosiaAppState): boolean
    {
        return (appState && (appState.__ambrosiaInternalState._stateGUID === this._stateGUID));
    }
}

/** The version of the JavaScript language binding (matches the ambrosia-node package version). */
export function languageBindingVersion(): string
{
    return ("2.0.1"); // Be careful when editing this because build.ps1 looks for "(X.X.X);" [in this file], where 'X' can be any integer
}

/** The modes of operation that the language binding (LB) can be initalized for. */
export enum LBInitMode
{
    /** The language binding will be initialized for running an Immortal instance. This is the default mode. */
    Normal,
    /** The language binding will be initialized for running code generation. */
    CodeGen
}

let _initMode: LBInitMode = LBInitMode.Normal;

/** Returns the mode that Ambrosia was initialized in (via initialize[Async]). */
export function initializationMode(): LBInitMode
{
    return (_initMode);
}

let _initialized: boolean = false; // Whether initializeAsync() has already succeeded

/** 
 * Loads either 'ambrosiaConfig.json' (the default), or the alternate config file specified via the "ambrosiaConfigFile=xxx" command-line parameter.\
 * Also, when initMode is LBInitMode.Normal, does some asynchronous initialization tasks (like the optional auto-registration and log-deletion tasks)
 * and also handles "eraseInstance" and "eraseInstanceAndReplicas" commands.\
 * If any failure is encountered, the error will be logged and the process will exit.
 */
// Note: As a general principle, using async/await is NOT recommended when writing Ambrosia apps. This is because the continuation cannot be serialized,
//       so it can easily lead to non-deterministic program execution. However, certain actions can be safely executed asynchronously, with initialization
//       being one of those.
export async function initializeAsync(initMode?: LBInitMode): Promise<void>;
/** [Internal] This overload is for internal use only. */
export async function initializeAsync(initMode: LBInitMode, completionWrapper: Utils.AsyncCompleteWrapper): Promise<void>;
export async function initializeAsync(initMode: LBInitMode = LBInitMode.Normal, completionWrapper?: Utils.AsyncCompleteWrapper): Promise<void>
{
    let ambrosiaConfigFileName: string | null = null;
    let configFile: Configuration.AmbrosiaConfigFile | null = null;
    let initializationError: Error | undefined = undefined;

    try
    {
        if (_initialized)
        {
            throw new Error("Ambrosia has already been initialized");
        }

        _initMode = initMode;
        ambrosiaConfigFileName = Utils.getCommandLineArg("ambrosiaConfigFile", Configuration.AmbrosiaConfigFile.DEFAULT_FILENAME);
        configFile = new Configuration.AmbrosiaConfigFile(ambrosiaConfigFileName);
        const eraseInstance = Utils.hasCommandLineArg("eraseInstance");
        const eraseAllReplicas = Utils.hasCommandLineArg("eraseInstanceAndReplicas");
        const fullInstanceName: string = `'${configFile.instanceName}'${eraseAllReplicas ? " and ALL its replicas" : ((configFile.replicaNumber > 0) ? ` (replica #${configFile.replicaNumber})` : "")}`;

        if (configFile.lbOptions.enableTypeScriptStackTraces)
        {
            // Enables an Error stack trace to refer to TS files/locations, not JS files/locations (see https://www.npmjs.com/package/source-map-support)
            require("source-map-support").install(); 
        }

        // Ensure that unhandled errors get logged [these would otherwise only be written to the Debug Console window]. 
        // Note: If not using the source-map-support package (ie. lbOptions.enableTypeScriptStackTraces is false), then the 
        //       logged error will only show the JS stack, not the TS stack (as the Debug Console does). Consequently, in 
        //       this case it's usually better to just check the "Uncaught Exceptions" option under "Breakpoints" in VSCode.
        Process.on("uncaughtException", function handleUncaughtException(error: Error) 
        {
            Utils.logWithColor(Utils.ConsoleForegroundColors.Red, error.stack ?? error.toString(), "Uncaught Exception", Utils.LoggingLevel.Minimal);
            Utils.closeOutputLog();
            Process.exit(-2); // Failing fast is the best policy
        });

        Process.on("unhandledRejection", function handleUncaughtRejection<T>(error: Error | any, promise: Promise<T>) 
        {
            // Note: 'promise' can only be inspected when using the debugger
            Utils.logWithColor(Utils.ConsoleForegroundColors.Red, (error instanceof Error) ? error.stack ?? error.toString() : error.toString(), "Uncaught Promise Rejection", Utils.LoggingLevel.Minimal);
            Utils.closeOutputLog();
            Process.exit(-2); // Failing fast is the best policy
        });

        Utils.log(`Running Ambrosia Node.js language binding version ${languageBindingVersion()} on Node.js ${process.versions.node} ${process.arch} (on ${Utils.isWindows() ? "Windows" : OS.platform()})`, null, Utils.LoggingLevel.Minimal);
        Utils.log(`Ambrosia configuration loaded from '${ambrosiaConfigFileName}'`, null, Utils.LoggingLevel.Minimal);

        if (eraseInstance || eraseAllReplicas)
        {
            if (!Utils.canLogToConsole())
            {
                // This message will ONLY appear in the output log file
                Utils.log(`Error: Unable to erase (reason: the 'outputLogDestination' must include '${Configuration.OutputLogDestination[Configuration.OutputLogDestination.Console]}'))`, null, Utils.LoggingLevel.Minimal);
                Process.exit(-1);
            }
            else
            {
                Utils.log(`Warning: Are you sure you want to completely erase instance ${fullInstanceName} (y/n)?`, null, Utils.LoggingLevel.Minimal);
                const keyPressed: string = await Utils.consoleReadKeyAsync(["y", "n"]);
                if (keyPressed === "y")
                {
                    if (eraseInstance)
                    {
                        await Configuration.eraseInstanceAsync(configFile.instanceName, configFile.replicaNumber, true);
                    }
                    if (eraseAllReplicas)
                    {
                        await Configuration.eraseInstanceAndReplicasAsync(configFile.instanceName, true);
                    }
                }
                else
                {
                    Utils.log(`Operation cancelled: Instance ${fullInstanceName} NOT erased`, null, Utils.LoggingLevel.Minimal);
                }
                Process.exit(0);
            }
        }

        if (initMode === LBInitMode.CodeGen)
        {
            if (configFile.lbOptions.outputLoggingLevel < Utils.LoggingLevel.Verbose)
            {
                Utils.log(`Warning: Code-gen will not report its progress because the 'outputLoggingLevel' in ${ambrosiaConfigFileName} is set to '${Utils.LoggingLevel[configFile.lbOptions.outputLoggingLevel]}'; set it to at least '${Utils.LoggingLevel[Utils.LoggingLevel.Verbose]}' to fix this`, null, Utils.LoggingLevel.Minimal);
            }
        }

        if (initMode === LBInitMode.Normal)
        {
            let reRegistrationRequired: boolean = false;

            if (configFile.isIntegratedIC) // Only allow auto/re-registration when the IC is running integrated (because it's only in this mode that we control starting/stopping the IC) 
            {
                const doingLiveUpgrade: boolean = configFile.isLiveUpgradeRequested;
                const doingAutoRegister: boolean = !doingLiveUpgrade && configFile.isAutoRegister;
                reRegistrationRequired = doingLiveUpgrade || doingAutoRegister;

                if (doingAutoRegister)
                {
                    // If there's no existing registration, then it's necessary (and safe) to register BEFORE calling configFile.initializeAsync()
                    const isRegistered: boolean = await AmbrosiaStorage.isRegisteredAsync(configFile.instanceName, configFile.replicaNumber);
                    if (!isRegistered)
                    {
                        Utils.log(`No existing registration found for ${fullInstanceName}`);
                        await Configuration.registerInstanceAsync(true);
                        reRegistrationRequired = false; // No need to do it again
                    }
                }
            }

            // Note: Run awaited methods in parallel (for performance)
            // Aside: Node.js module loading/compiling can happen "in parallel" with async code execution. So while an operation
            //        is being awaited, additional loading/compiling may occur. This behavior can "mask" code problems, such
            //        as a module-level variable being used BEFORE it has been declared. In this specific case, the module with 
            //        the [lexical] code error gets compiled during the time it takes for the await to complete, so when the module
            //        starts executing (after the await completes) it's already fully compiled, and the lexical problem goes undetected.
            let voidResult: void;
            let remoteInstanceNames: string[];
            let tasks: Promise<any>[] = [configFile.initializeAsync(), AmbrosiaStorage.getRemoteInstancesAsync(configFile.instanceName, configFile.lbOptions.deleteRemoteCRAConnections)];
            [voidResult, remoteInstanceNames] = await Promise.all(tasks);

            Utils.log(`Local IC connects to ${remoteInstanceNames.length} remote ICs ${(remoteInstanceNames.length > 0) ? `'(${remoteInstanceNames.join("', '")}')` : ""}`);

            // If needed, [re]register the instance
            // Note: This must be done AFTER calling configFile.initializeAsync() - it cannot be done in parallel - because we need to acquire any non-locally-overridden settings from the existing registration
            if (reRegistrationRequired)
            {
                // Note: This has an external side-effect (changing the registration)
                await Configuration.registerInstanceAsync(false);
            }

            // If needed, delete the IC logs files
            // Note: This must be done AFTER calling configFile.initializeAsync() - it cannot be done in parallel - because we need to acquire any non-locally-overridden settings from the existing registration
            // Note: Ideally we'd do this in IC.start(), but we need an async function to delete logs stored in Azure
            await IC.deleteLogsAsync();
            
            IC.setRemoteInstanceNames(remoteInstanceNames);
            Messages.initializeBytePool(configFile.lbOptions.messageBytePoolSizeInMB)
        }
        _initialized = true;
    }
    catch (error: unknown)
    {
        initializationError = Utils.makeError(error);
        Utils.tryLog(`Unable to load configuration file '${ambrosiaConfigFileName}' (reason: ${initializationError.message})`, "Ambrosia initialization error", Utils.LoggingLevel.Minimal);
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
    // Note: If initializeAsync() doesn't call await, it will run to completion (synchronously) then call onComplete()
    initializeAsync(initMode, new Utils.AsyncCompleteWrapper(onComplete));
}
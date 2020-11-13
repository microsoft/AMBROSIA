// Module for configuring Ambrosia.
import File = require("fs");
import Path = require("path");
import ChildProcess = require("child_process");
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as AmbrosiaStorage from "./Storage";
import * as Messages from "./Messages"
import * as Streams from "./Streams";
import * as StringEncoding from "./StringEncoding";
import * as Utils from "./Utils/Utils-Index";
import { RejectedPromise } from "./ICProcess"; // There is no re-write issue here as this is just a type

/** The Ambrosia configuration settings loaded from ambrosiaConfig.json (or alternate config file). */
let _loadedConfigFile: AmbrosiaConfigFile = null;

/** The name of the loaded Ambrosia configuration file (eg. "ambrosiaConfig.json"). */
let _loadedConfigFileName: string = null;

/** Type of the error handler method that's invoked when ICProcess encounters an error. */
export type ICErrorHandler = (source: string, error: Error, isFatalError?: boolean) => void;

/** The locations where language-binding output can be written. */
export enum OutputLogDestination
{
    /** Log to the console. Use this during development/debugging only (it's too slow for production). */
    Console = 1,
    /** Log to a file in the 'outputLogFolder' specified in ambrosiaConfig.json. use this setting in production (for performance). */
    File = 2,
    /** Log to both the console and a file. */
    ConsoleAndFile = Console | File
}

/** Class representing IC registration (or runtime) settings, obtained from an external source (the IC or Azure). */
export class RegistrationSettings
{ 
    icLogFolder?: string;
    icSendPort?: number
    icReceivePort?: number 
};

/** 
 * Returns the settings from the loaded configuration file.\
 * Will return null if called before initialize()/initializeAsync().
 */
export function loadedConfig(): AmbrosiaConfigFile
{
    return (_loadedConfigFile);
}

/** 
 * Returns the name of the loaded configuration file.\
 * Will return null if called before initialize()/initializeAsync().
 */
export function loadedConfigFileName(): string
{
    return (_loadedConfigFileName);
}

/** Class representing the configuration settings loaded from ambrosiaConfig.json (or alternate config file). */
export class AmbrosiaConfigFile
{
    public static readonly DEFAULT_FILENAME: string = "ambrosiaConfig.json";
    private _requiredProperties: string[] = ["instanceName", "icCraPort"];
    private _allRegistrationOverridesSet: boolean = false;

    private _instanceName: string = null;
    private _useRegistrationSettings: boolean = false;
    private _icCraPort: number = -1;
    private _icReceivePort: number = -1;
    private _icSendPort: number = -1;
    private _icLogFolder: string = "";
    private _icBinFolder: string = "";
    private _useNetCore: boolean = false;
    private _debugStartCheckpoint: number = 0;
    private _appVersion: number = 0;
    private _lbOptions: LanguageBindingOptions = new LanguageBindingOptions();
    
    /** [ReadOnly][Required] The name this Ambrosia Immortal instance will be referred to by all instances (including itself). MUST match the value used during 'RegisterInstance'. */
    get instanceName(): string { return (this._instanceName); }
    /** [ReadOnly] Whether to use the registered Immortal Coordinator (IC) settings (icReceivePort, icSendPort, icLogFolder) instead of providing them explicitly in ambrosiaConfig.json. */
    get useRegistrationSettings(): boolean { return (this._useRegistrationSettings); }
    /** [ReadOnly][Required] The port number that the Common Runtime for Applications (CRA) layer uses. */
    get icCraPort(): number { return (this._icCraPort); }
    /** [ReadOnly][Required (if useRegistrationSettings is false)] The port number that the Immortal Coordinator (IC) receives on. MUST match the value used during 'RegisterInstance'. */
    get icReceivePort(): number { return (this._icReceivePort); }
    /** [ReadOnly][Required (if useRegistrationSettings is false)] The port number that the Immortal Coordinator (IC) sends on. MUST match the value used during 'RegisterInstance'. */
    get icSendPort(): number { return (this._icSendPort); }
    /** [ReadOnly][Required (if useRegistrationSettings is false)] The folder where the Immortal Coordinator (IC) will persist its logs. MUST match the value used during 'RegisterInstance'. */
    get icLogFolder(): string { return (this._icLogFolder); }
    /** [ReadOnly] The folder where the Immortal Coordinator (IC) binaries exist. If not specified, the 'AMBROSIATOOLS' environment variable will be used. */
    get icBinFolder(): string { return (this._icBinFolder); }
    /** [ReadOnly] Whether to use .NET Core (instead of .Net Framework) to run the Immortal Coordinator (IC) [this is a Windows-only option]. Defaults to false. */
    get useNetCore(): boolean { return (this._useNetCore); }
    /** [ReadOnly] The checkpoint number to start "time-travel debugging" from. */
    get debugStartCheckpoint(): number { return (this._debugStartCheckpoint); }
    /** [ReadOnly] The version (code and state) of this Immortal instance. */
    get appVersion(): number { return (this._appVersion); }
    /** [ReadOnly] Options for how the language-binding behaves. */
    get lbOptions(): LanguageBindingOptions { return (this._lbOptions); }

    constructor(configFileName: string)
    {
        if (File.existsSync(configFileName))
        {
            let fileContents: string = File.readFileSync(configFileName, { encoding: "utf8" });
            let jsonObj: object = JSON.parse(fileContents);

            _loadedConfigFileName = configFileName;
            _loadedConfigFile = this;

            for (let requiredPropName of this._requiredProperties)
            {
                if (!jsonObj.hasOwnProperty(requiredPropName))
                {
                    throw new Error(`Required setting '${requiredPropName}' is missing`)
                }
            }

            for (let propName in jsonObj)
            {
                if (this.hasOwnProperty("_" + propName))
                {
                    this["_" + propName] = (propName === "lbOptions") ? new LanguageBindingOptions(jsonObj[propName], true) : jsonObj[propName];
                }
                else
                {
                    // See https://code.visualstudio.com/docs/languages/json.
                    // for an example, see https://json.schemastore.org/tsconfig. See more examples at https://github.com/SchemaStore/schemastore/blob/master/src/api/json/catalog.json.
                    if (propName !== "$schema")
                    {
                        throw new Error(`'${propName}' is not a valid setting name`)
                    }
                }
            }

            if (this.debugStartCheckpoint !== 0)
            {
                if (this.lbOptions.deleteLogs)
                {
                    throw new Error(`A non-zero 'debugStartCheckpoint' is invalid when 'lbOptions.deleteLogs' is true`);
                }
                // Verify that the requested checkpoint file exists
                let checkpointFileName: string = Path.join(this.icLogFolder, `${this.instanceName}_${this.appVersion}`, `${this.instanceName}chkpt${this.debugStartCheckpoint}`);
                if (!File.existsSync(checkpointFileName))
                {
                    throw new Error(`'debugStartCheckpoint' (${this.debugStartCheckpoint}) does not exist (${checkpointFileName})`);
                }
            }

            if (this.instanceName.trim().length === 0)
            {
                throw new Error("'instanceName' cannot be be empty");
            }

            if (this.icLogFolder)
            {
                this._icLogFolder = Utils.ensurePathEndsWithSeparator(this.icLogFolder);
            }

            this._allRegistrationOverridesSet = (jsonObj["icReceivePort"] !== undefined) && (jsonObj["icSendPort"] !== undefined) && (jsonObj["icLogFolder"] !== undefined);
        }
        else
        {
            let howToGetFile: string = Utils.equalIgnoringCase(configFileName, AmbrosiaConfigFile.DEFAULT_FILENAME) ? 
                "; you can copy this file from .\\node_modules\\ambrosia-node\\ambrosiaConfig.json, and then edit it to match your IC registration. " +
                "If using VS2019+ or VSCode to edit the file, copy ambrosiaConfig-schema.json too." : "";
            throw new Error(`The file does not exist${howToGetFile}`);
        }
    }

    /** If needed, updates the IC registration settings (icLogFolder, icSendPort, icReceivePort) with the values asynchronously obtained from the IC (or Azure). */
    async initializeAsync(): Promise<void>
    {
        // TODO: This is just a temporary flag while we wait for required IC changes.
        //       When the change has been made we can (if desired):
        //       1) Remove AmbrosiaStorage.getRegistrationSettingsAsync().
        //       2) Remove "icReceivePort", "icSendPort" and "icLogFolder" from both ambrosiaConfig.json and ambrosiaConfig-schema.json (as they will no longer be user-settable).
        //       3) Remove the "MUST match the value used during 'RegisterInstance'" comment from these same 3 properties of the AmbrosiaConfigFile class.
        let useICForRegistrationSettings: boolean = false; 
        let registrationSettings: RegistrationSettings = null;
        
        if (useICForRegistrationSettings)
        {
            // Note: If all the local overrides for the IC registration settings have been set then there's no need
            //       to query the IC since none of the settings will be used (the local settings will take precedence)
            if (!this._allRegistrationOverridesSet)
            {
                Utils.log("Reading registration settings...");
                registrationSettings = await this.getICRuntimeSettingsAsync();
                this._icLogFolder = Utils.ensurePathEndsWithSeparator(this.icLogFolder);
            }
        }
        else
        {
            // Note: If all the local overrides for the IC registration settings have been set then there's no need
            //       to query Azure since none of the settings will be used (the local settings will take precedence)
            if (this.useRegistrationSettings && !this._allRegistrationOverridesSet)
            {
                Utils.log("Reading registration settings...");
                registrationSettings = await AmbrosiaStorage.getRegistrationSettingsAsync(this.instanceName, this.appVersion);
            }
            else
            {
                if (!this._allRegistrationOverridesSet)
                {
                    throw new Error("When 'useRegistrationSettings' is false, 'icReceivePort', 'icSendPort' and 'icLogFolder' must all be specified");
                }
            }
        }

        // Note: Local settings (if set) will take precedence over the corresponding registration settings
        if (registrationSettings)
        {
            if ((this.icLogFolder === "") && registrationSettings.icLogFolder)
            {
                this._icLogFolder = registrationSettings.icLogFolder;
            }
            if ((this.icSendPort === -1) && (registrationSettings.icSendPort !== undefined))
            {
                this._icSendPort = registrationSettings.icSendPort;
            }
            if ((this.icReceivePort === -1) && (registrationSettings.icReceivePort !== undefined))
            {
                this._icReceivePort = registrationSettings.icReceivePort;
            }
        }
    }

    /** Asynchronously reads the IC registration settings (icReceivePort, icSendPort, icLogFolder) from the icRuntimeConfig.json file emitted by the IC. */
    private async getICRuntimeSettingsAsync(): Promise<RegistrationSettings>
    {
        let promise: Promise<RegistrationSettings> = new Promise<RegistrationSettings>((resolve, reject: RejectedPromise) =>
        {
            const icExecutable: string = Utils.getICExecutable(this.icBinFolder, this.useNetCore, (this.debugStartCheckpoint > 0)); // This may throw
            let icRuntimeSettingsFile: string = Path.join(process.cwd(), "icRuntimeConfig.json");

            Utils.deleteFile(icRuntimeSettingsFile);

            let args: string[] = [`--instanceName=${this.instanceName}`, `--emitRuntimeConfigFile=${icRuntimeSettingsFile}`];
            let icProcess: ChildProcess.ChildProcess = ChildProcess.spawn(this.useNetCore ? "dotnet" : icExecutable, args);
            let memStream: Streams.MemoryStream = new Streams.MemoryStream();
            icProcess.stdout.pipe(memStream);

            icProcess.on("close", (code: number, signal: NodeJS.Signals) =>
            {
                try
                {
                    let output: string = StringEncoding.fromUTF8Bytes(memStream.readAll());
                    let syntaxError: boolean = (output.indexOf("Usage:") !== -1);

                    if (syntaxError)
                    {
                        let missingArg: string = output.substr(0, output.indexOf("."))
                        throw new Error(`IC syntax error - ${missingArg}`);
                    }

                    if (!File.existsSync(icRuntimeSettingsFile))
                    {
                        throw new Error(`IC failed to emit runtime settings file (${icRuntimeSettingsFile}); exit code ${code}`);
                    }
                    else
                    {
                        let fileContents: string = File.readFileSync(icRuntimeSettingsFile, { encoding: 'utf8' });
                        let jsonObj: object = JSON.parse(fileContents);
                        let registrationSettings: RegistrationSettings = new RegistrationSettings();
            
                        for (let propName in jsonObj)
                        {
                            switch (propName)
                            {
                                case "logFolder":
                                    registrationSettings.icLogFolder = jsonObj[propName];
                                    break;
                                case "receivePort":
                                    registrationSettings.icReceivePort = parseInt(jsonObj[propName]);
                                    break;
                                case "sendPort":
                                    registrationSettings.icSendPort = parseInt(jsonObj[propName]);
                                    break;
                            }
                        }
                        Utils.log("Registration settings read (from IC)");
                        resolve(registrationSettings);
                    }
                }
                catch (error)
                {
                    reject(new Error(`Unable to read IC registration settings (reason: ${(error as Error).message})`));
                }
            });
        });
        return (promise);
    }
}

/** The Ambrosia configuration (settings and event handlers) for the app. */
export class AmbrosiaConfig
{
    private _dispatcher: Messages.MessageDispatcher = null;
    private _checkpointProducer: Streams.CheckpointProducer = null;
    private _checkpointConsumer: Streams.CheckpointConsumer = null;
    private _onError: ICErrorHandler = null; // While this could be handled via emitAppEvent(), those handlers are all optional, so we chose to require a handler for onError
    private _configFile: AmbrosiaConfigFile = null; // The config file that was used to initialize the configuration

    /** [ReadOnly] This handler will be called each time a [dispatchable] message is received. Set via constructor. */
    get dispatcher(): Messages.MessageDispatcher { return (this._dispatcher); }
    /** Note: The property setter is for internal use only. */
    set dispatcher(value: Messages.MessageDispatcher) { this._dispatcher = value; } // Note: Settable so that the IC can wrap the user-supplied dispatcher
    /** [ReadOnly] This method will be called to generate (write) a checkpoint - a binary seralization of application state. Set via constructor. */
    get checkpointProducer(): Streams.CheckpointProducer { return (this._checkpointProducer); }
    /** [ReadOnly] This method will be called to load (read) a checkpoint - a binary seralization of application state. Set via constructor. */
    get checkpointConsumer(): Streams.CheckpointConsumer { return (this._checkpointConsumer); }
    /** [ReadOnly] This handler will be called if the IC throws an error. Set via constructor. */
    get onError(): ICErrorHandler { return (this._onError); }

    /** [ReadOnly] The folder where the Immortal Coordinator (IC) will persist its logs. */
    get icLogFolder(): string { return (this._configFile.icLogFolder); }
    /** [ReadOnly] The folder where the Immortal Coordinator (IC) binaries exist. If not specified, the 'AMBROSIATOOLS' environment variable will be used. */
    get icBinFolder(): string { return (this._configFile.icBinFolder); }
    /** [ReadOnly] The name this Ambrosia Immortal instance will be referred to by all instances (including itself). */
    get icInstanceName(): string { return (this._configFile.instanceName); }
    /** [ReadOnly] The port number that the Common Runtime for Applications (CRA) layer uses. */
    get icCraPort(): number { return (this._configFile.icCraPort); }
    /** [ReadOnly] The port number that the Immortal Coordinator (IC) receives on. */
    get icReceivePort(): number { return (this._configFile.icReceivePort); }
    /** [ReadOnly] The port number that the Immortal Coordinator (IC) sends on. */
    get icSendPort(): number { return (this._configFile.icSendPort); }
    /** [ReadOnly] Whether to use .NET Core (instead of .Net Framework) to run the Immortal Coordinator (IC) [this is Windows-only option]. Defaults to false. */
    get useNetCore(): boolean { return (this._configFile.useNetCore); }
    /** [ReadOnly] The checkpoint number to start "time-travel debugging" from. */
    get debugStartCheckpoint(): number { return (this._configFile.debugStartCheckpoint); }
    /** [ReadOnly] The version (code and state) of this Immortal instance. */
    get appVersion(): number { return (this._configFile.appVersion); }
    /** [ReadOnly] Options for how the language-binding behaves. */
    get lbOptions(): LanguageBindingOptions { return (this._configFile.lbOptions); }

    /** [ReadOnly] Whether Ambrosia is running in 'time-travel debugging' mode. */
    get isTimeTravelDebugging(): boolean { return (this.debugStartCheckpoint > 0); }

    constructor(messageDispatcher: Messages.MessageDispatcher, checkpointProducer: Streams.CheckpointProducer, checkpointConsumer: Streams.CheckpointConsumer, onICError: ICErrorHandler)
    {
        if (_loadedConfigFile === null)
        {
            throw new Error("The supplied 'configFile' is null; Ambrosia.initialize()/initializeAsync() may not have been called");
        }
        this._configFile = _loadedConfigFile;

        this._dispatcher = messageDispatcher;
        this._checkpointConsumer = checkpointConsumer;
        this._checkpointProducer = checkpointProducer;
        this._onError = onICError;
    }
}

/** Class representing options for how the language-binding behaves. */
export class LanguageBindingOptions
{
    private _deleteLogs: boolean = false; 
    private _deleteRemoteCRAConnections: boolean = false;
    private _allowCustomJSONSerialization: boolean = true;
    private _typeCheckIncomingPostMethodParameters: boolean = true;
    private _outputLoggingLevel: Utils.LoggingLevel = Utils.LoggingLevel.Normal;
    private _outputLogFolder: string = "./outputLogs";
    private _outputLogDestination: OutputLogDestination = OutputLogDestination.Console;
    private _allowDisplayOfRpcParams: boolean = false;
    private _allowPostMethodTimeouts: boolean = true;
    private _allowPostMethodErrorStacks: boolean = false;
    private _enableTypeScriptStackTraces: boolean = true;
    private _maxInFlightPostMethods: number = -1;
    
    /** [ReadOnly] Whether to clear the IC logs (all prior checkpoints and logged state changes will be permanently lost, and recovery will not run). Defaults to false. */
    get deleteLogs(): boolean { return (this._deleteLogs); } 
    /** [ReadOnly][Debug] Whether to true to delete non-local CRA connections at startup. Defaults to false. */
    get deleteRemoteCRAConnections(): boolean {return ( this._deleteRemoteCRAConnections); }
    /** [ReadOnly] Whether to disable the specialized JSON serialization of BigInt and typed-arrays (eg. Uint8Array). Defaults to true. */
    get allowCustomJSONSerialization(): boolean { return (this._allowCustomJSONSerialization); }
    /** [ReadOnly] Whether to skip type-checking the parameters of incoming post methods for correctness against published methods/types. Defaults to true.*/
    get typeCheckIncomingPostMethodParameters(): boolean { return (this._typeCheckIncomingPostMethodParameters); }
    /** [ReadOnly] The level of detail to include in the output log. Defaults to 'Normal'. */
    get outputLoggingLevel(): Utils.LoggingLevel { return (this._outputLoggingLevel); }
    /** [ReadOnly] The folder where the language-binding will write output log files (when outputLogDestination is 'File' or 'ConsoleAndFile'). Defaults to "./outputLogs". */
    get outputLogFolder(): string { return (this._outputLogFolder); }
    /** [ReadOnly] Location(s) where the language-binding will log output. Defaults to 'Console'. */
    get outputLogDestination(): OutputLogDestination { return (this._outputLogDestination); }
    /** [ReadOnly] Whether to allow incoming RPC parameters [which can contain privacy/security related content] to be displayed/logged. */
    get allowDisplayOfRpcParams(): boolean { return (this._allowDisplayOfRpcParams); }
    /** [ReadOnly] Whether to disable the timeout feature of Post methods. Defaults to true. */
    get allowPostMethodTimeouts(): boolean { return (this._allowPostMethodTimeouts); }
    /** [ReadOnly] Whether to allow sending a full stack trace in the result (as the 'originalError' parameter) if a Post method fails. Defaults to false. */
    get allowPostMethodErrorStacks(): boolean { return (this._allowPostMethodErrorStacks); }
    /** [ReadOnly] Whether Error stack trace will refer to TypeScript files/locations (when available) instead of JavaScript files/locations. Defaults to true. */
    get enableTypeScriptStackTraces(): boolean { return (this._enableTypeScriptStackTraces); }
    /** [ReadOnly] Whether to generate a warning whenever the number of in-flight post methods reaches this threshold. Defaults to -1 (no limit). */
    get maxInFlightPostMethods(): number { return (this._maxInFlightPostMethods); }
    /** [ReadOnly] Whether 'outputLoggingLevel' is currently set to 'Verbose'. */
    get verboseOutputLogging(): boolean { return ( this.outputLoggingLevel === Utils.LoggingLevel.Verbose); }

    // Note: We allow a LanguageBindingOptions instance to be initialized from a "partial" LanguageBindingOptions object, like { deleteLogs: true }
    constructor(partialOptions?: LanguageBindingOptions, throwOnUnknownOption: boolean = false)
    {
        for (let optionName in partialOptions)
        {
            if (this["_" + optionName] !== undefined)
            {
                if (typeof partialOptions[optionName] === "string")
                {
                    // Handle the cases where we have the name of the an enum (eg. "Normal") instead of the [numerical] value.
                    // This can happen when parsing ambrosiaConfig.json.
                    switch (optionName)
                    {
                        case "outputLoggingLevel":
                            let level: keyof typeof Utils.LoggingLevel = partialOptions[optionName] as any;
                            if (Utils.LoggingLevel[level] !== undefined)
                            {
                                this["_" + optionName] = Utils.LoggingLevel[level];
                            }
                            else
                            {
                                throw new Error(`Option '${optionName}' has an invalid value ('${level}')`);
                            }
                            break;
                        case "outputLogDestination":
                            let destination: keyof typeof OutputLogDestination = partialOptions[optionName] as any;
                            if (OutputLogDestination[destination] !== undefined)
                            {
                                this["_" + optionName] = OutputLogDestination[destination];
                            }
                            else
                            {
                                throw new Error(`Option '${optionName}' has an invalid value ('${destination}')`);
                            }
                            break;
                        default:
                            // A non-enum
                            this["_" + optionName] = partialOptions[optionName];
                            break;
                    }
                }
                else
                {
                    this["_" + optionName] = partialOptions[optionName];
                }
            }
            else
            {
                if (throwOnUnknownOption)
                {
                    throw new Error(`'${optionName}' is not a valid option name`);
                }
            }
        }
    }
}

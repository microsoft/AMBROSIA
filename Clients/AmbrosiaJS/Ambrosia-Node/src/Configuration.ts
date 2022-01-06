// Module for configuring Ambrosia.
import File = require("fs");
import Path = require("path");
import ChildProcess = require("child_process");
import Process = require("process");
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as AmbrosiaStorage from "./Storage";
import * as IC from "./ICProcess";
import * as Messages from "./Messages"
import * as Streams from "./Streams";
import * as StringEncoding from "./StringEncoding";
import * as Utils from "./Utils/Utils-Index";
import { RejectedPromise } from "./ICProcess"; // There is no re-write issue here as this is just a type

const LBOPTIONS_SECTION: string = "lbOptions"; // The section name for language binding options in ambrosiaConfig.json

/** The Ambrosia configuration settings loaded from ambrosiaConfig.json (or alternate config file). */
let _loadedConfigFile: AmbrosiaConfigFile;

/** The name of the loaded Ambrosia configuration file (eg. "ambrosiaConfig.json"). */
let _loadedConfigFileName: string = "";

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

/** The available hosting modes for the IC. */
export enum ICHostingMode
{
    /** 
     * The IC runs on the same machine as the LB, but without its own console window; the LB automatically starts/stops the IC,
     * and the LB output includes the IC output. This is the most commonly used mode.
     */
    Integrated,
    /** 
     * The IC runs on the machine specified by icIPv4Address (which can be the local machine) or the local machine if icIPv4Address is omitted,
     * in its own console window; the IC must be started explicitly.
     * This mode is a rarely used, and renders many options in ambrosiaConfig.json unavailable (ie. they have to be omitted).
     */
    Separated
}

/** Class representing IC registration settings, obtained from Azure, that the LB also needs to know. */
export class RegistrationSettings
{ 
    icLogFolder: string | undefined;
    icSendPort: number | undefined;
    icReceivePort: number | undefined;
    /** Note: This registered setting cannot be overridden locally, except when doing 'time-travel debugging' (TTD). */
    appVersion: number | undefined;
    /** Note: This registered setting can be overridden locally, but only to trigger an upgrade (via re-registration). */
    upgradeVersion: number | undefined;
    /** Note: This registered setting cannot be overridden locally. */
    activeActive: boolean | undefined;
};

/** 
 * Returns the settings from the loaded configuration file.\
 * Throws if called before initialize()/initializeAsync().
 */
export function loadedConfig(): AmbrosiaConfigFile
{
    if (!_loadedConfigFile)
    {
        throw new Error("Ambrosia.initialize() / initializeAsync() has not been called");
    }
    return (_loadedConfigFile);
}

/** 
 * Returns the name of the loaded configuration file, which may include a path.\
 * Will return null if called before initialize()/initializeAsync().
 */
export function loadedConfigFileName(): string | null
{
    return (_loadedConfigFileName || null);
}

/** The storage types that IC logs can be persisted in. */
export enum LogStorageType
{
    /** OS file storage. */
    Files = 0,
    /** Azure blob storage. Uses the same storage account as CRA (the AZURE_STORAGE_CONN_STRING environment variable). */
    Blobs = 1
}

/** The types of application code that can be active (in use). */
export enum ActiveCodeType
{
    /** Pre-upgrade application code. */
    VCurrent = 0,
    /** Post-upgrade application code. */
    VNext = 1
}

/** The additional types (other than true or false) of instance auto-registration that can be performed (at startup). */
export enum AutoRegisterType
{
    /** Perform auto-registration, and then immediately exit the program. */
    TrueAndExit = 0
}

/** Class representing the configuration settings loaded from ambrosiaConfig.json (or alternate config file). */
export class AmbrosiaConfigFile
{
    // Indexer (for 'noImplicitAny' compliance), although this does end up hiding ts(2551): "Property 'xxxxx' does not exist on type 'AmbrosiaConfigFile'"
    [key: string]: unknown;

    public static readonly DEFAULT_FILENAME: string = "ambrosiaConfig.json";
    private _requiredProperties: string[] = ["instanceName", "icCraPort"];
    private _propertiesOnlyForIntegratedIC: string[] = ["icLogStorageType", "icLogFolder", "icBinFolder", "useNetCore", "debugStartCheckpoint", "debugTestUpgrade", 
                                                        "logTriggerSizeInMB", "secureNetworkAssemblyName", "secureNetworkClassName", 
                                                        "appVersion", "upgradeVersion", "autoRegister", "lbOptions.deleteLogs"];
    private _inConstructor: boolean = false; // Whether the AmbrosiaConfigFile constructor is currently executing
    private _allRegistrationOverridesSet: boolean = false; // Whether all the registration settings [that the LB also needs to know about] have been set in ambrosiaConfig.json
    private _configuredProperties: string[] = []; // The list of properties that have been set in ambrosiConfig.json
    private _isFirstStartAfterInitialRegistration: boolean = false; // Computed at runtime, not a setting in ambrosiaConfig.json

    private _instanceName: string = "";
    private _icCraPort: number = -1;
    private _icReceivePort: number = -1;
    private _icSendPort: number = -1;
    private _icLogFolder: string = "";
    private _icLogStorageType: keyof typeof LogStorageType = LogStorageType[LogStorageType.Files] as keyof typeof LogStorageType;
    private _icBinFolder: string = "";
    private _icIPv4Address: string = "";
    private _icHostingMode: ICHostingMode = ICHostingMode.Integrated;
    private _useNetCore: boolean = false;
    private _debugStartCheckpoint: number = 0;
    private _debugTestUpgrade: boolean = false;
    private _logTriggerSizeInMB: number = 0;
    private _isActiveActive: boolean = false;
    private _replicaNumber: number = 0;
    private _appVersion: number = 0;
    private _upgradeVersion: number = 0;
    private _activeCode: ActiveCodeType = ActiveCodeType.VCurrent;
    private _autoRegister: boolean | AutoRegisterType = false;
    private _secureNetworkAssemblyName: string = "";
    private _secureNetworkClassName: string = "";
    private _lbOptions: LanguageBindingOptions = new LanguageBindingOptions(this); // In case the the LBOPTIONS_SECTION is omitted from ambrosiaConfig.json
    
    /** [ReadOnly] Whether this is the first run of the instance following its initial registration. Note: This is computed at runtime, and the property setter is for internal use only. */
    get isFirstStartAfterInitialRegistration(): boolean { return (this._isFirstStartAfterInitialRegistration); }
    set isFirstStartAfterInitialRegistration(value: boolean) { this._isFirstStartAfterInitialRegistration = value; }

    /** [ReadOnly][Required] The name this Ambrosia Immortal instance will be referred to by all instances (including itself). MUST match the value used during 'RegisterInstance'. */
    get instanceName(): string { return (this._instanceName); }
    /** [ReadOnly][Required] The port number that the Common Runtime for Applications (CRA) layer uses. */
    get icCraPort(): number { return (this._icCraPort); }
    /** [ReadOnly] The port number that the Immortal Coordinator (IC) receives on. If not provided, it will be read from the registration. */
    get icReceivePort(): number { return (this._icReceivePort); }
    /** [ReadOnly] The port number that the Immortal Coordinator (IC) sends on. If not provided, it will be read from the registration. */
    get icSendPort(): number { return (this._icSendPort); }
    /** [ReadOnly] The folder where the Immortal Coordinator (IC) will write its logs (or read logs from if doing "time-travel debugging"). If not provided, it will be read from the registration. */
    get icLogFolder(): string { return (this._icLogFolder); }
    /** 
     * [ReadOnly] The storage type that the Immortal Coordinator (IC) logs will be persisted in.\
     * When set to "Blobs", the logs are written to Azure storage (using AZURE_STORAGE_CONN_STRING) in the "Blob Containers\ambrosialogs" container.
     * The icLogFolder can either be set to an empty string, or (rarely) to a desired sub-path under "ambrosialogs", eg. "TestLogs/Group1".
     */
    get icLogStorageType(): keyof typeof LogStorageType { this.onlyAppliesToIntegratedICHostingMode(); return (this._icLogStorageType); }
    /** [ReadOnly] The folder where the Immortal Coordinator (IC) binaries exist. If not specified, the 'AMBROSIATOOLS' environment variable will be used. */
    get icBinFolder(): string { this.onlyAppliesToIntegratedICHostingMode(); return (this._icBinFolder); }
    /** An override IPv4 address for the Immortal Coordinator (IC) to use instead of the local IPv4 address. */
    get icIPv4Address(): string { return (this._icIPv4Address); }
    /** 
     * [ReadOnly] The hosting mode for the Immortal Coordinator (IC), which affects where and how the IC runs. Defaults to ICHostingMode.Integrated.\
     * If not explicitly set, the value will be computed based on the value provided for 'icIPv4Address'.
     */
    get icHostingMode(): ICHostingMode { return (this._icHostingMode); }
    /** [ReadOnly] Whether to use .NET Core (instead of .Net Framework) to run the Immortal Coordinator (IC) [this is a Windows-only option]. Defaults to false. */
    get useNetCore(): boolean { this.onlyAppliesToIntegratedICHostingMode(); return (Utils.isWindows() ? this._useNetCore : false); }
    /** [ReadOnly] The checkpoint number to start "time-travel debugging" from. */
    get debugStartCheckpoint(): number { this.onlyAppliesToIntegratedICHostingMode(); return (this._debugStartCheckpoint); }
    /** [ReadOnly] Whether to perform a test upgrade (for debugging/testing purposes). Defaults to false. */
    get debugTestUpgrade(): boolean { this.onlyAppliesToIntegratedICHostingMode(); return (this._debugTestUpgrade); }
    /** [ReadOnly] The size (in MB) the log must reach before the IC will take a checkpoint and start a new log. */
    get logTriggerSizeInMB(): number { this.onlyAppliesToIntegratedICHostingMode(); return (this._logTriggerSizeInMB); }
    /** [ReadOnly] Whether this [primary] instance will run in an active/active configuration. MUST be set to true when 'replicaNumber' is greater than 0, and MUST match the value used when the instance/replica was registered. */
    get isActiveActive(): boolean { return (this._isActiveActive); }
    /** [ReadOnly] The replica (secondary) ID this instance will use in an active/active configuration. MUST match the value used when the replica was registered. */
    get replicaNumber(): number { return (this._replicaNumber); }
    /** [ReadOnly] The name of the .NET assembly used to establish a secure network channel between ICs. */
    get secureNetworkAssemblyName(): string { this.onlyAppliesToIntegratedICHostingMode(); return (this._secureNetworkAssemblyName); }
    /** [ReadOnly] The name of the .NET class (that implements ISecureStreamConnectionDescriptor) in 'secureNetworkAssemblyName'. */
    get secureNetworkClassName(): string { this.onlyAppliesToIntegratedICHostingMode(); return (this._secureNetworkClassName); }
    /** 
     * [ReadOnly] The nominal version of this Immortal instance.\
     * Used to identify the log sub-folder name (ie. &lt;icInstanceName>_&lt;appVersion>) that will be logged to (or read from, if debugStartCheckpoint is specified).
     */
    // Note: Although 'appVersion' is included in the _propertiesOnlyForIntegratedIC list, we don't call onlyAppliesToIntegratedICHostingMode()
    //       in its getter because even though it won't be set locally it will still be set by reading from the registration.
    get appVersion(): number { return (this._appVersion); }
    /** [ReadOnly] The nominal version this Immortal instance should upgrade to at startup. */
    // Note: Although 'upgradeVersion' is included in the _propertiesOnlyForIntegratedIC list, we don't call onlyAppliesToIntegratedICHostingMode()
    //       in its getter because even though it won't be set locally it will still be set by reading from the registration.
    get upgradeVersion(): number { return (this._upgradeVersion); }
    /** [ReadOnly] The currently active (in use) application code: pre-upgrade is 'VCurrent', post-upgrade is 'VNext'. */
    get activeCode(): ActiveCodeType { return (this._activeCode); }
    /** [ReadOnly] How to automatically [re]register this Immortal instance at startup. See also: isAutoRegister. */
    get autoRegister(): boolean | AutoRegisterType { this.onlyAppliesToIntegratedICHostingMode(); return (this._autoRegister); }
    /** [ReadOnly] Options for how the language-binding behaves. */
    get lbOptions(): LanguageBindingOptions { return (this._lbOptions); }

    /** [Readonly] Whether a "live" upgrade has been requested (by the local configuration). This is a computed setting. */
    get isLiveUpgradeRequested(): boolean { return (this.isConfiguredProperty("appVersion") && this.isConfiguredProperty("upgradeVersion") && (this.upgradeVersion > this.appVersion)); }

    /** 
     * [ReadOnly] Whether the icHostingMode setting is 'Integrated'.\
     * Note: This computed setting is just for brevity. 
     */
    get isIntegratedIC(): boolean { return (this.icHostingMode === ICHostingMode.Integrated); }

    /** [ReadOnly] Whether Ambrosia is running in 'time-travel debugging' mode. */
    get isTimeTravelDebugging(): boolean { return (this.debugStartCheckpoint > 0); }

    /** [Readonly] Whether to automatically [re]register this Immortal instance at startup. This is a computed setting. */
    get isAutoRegister(): boolean { return ((this.autoRegister === true) || (this.autoRegister === AutoRegisterType.TrueAndExit)); }

    constructor(configFileName: string)
    {
        try
        {
            this._inConstructor = true;
            if (File.existsSync(configFileName))
            {
                let fileContents: string = File.readFileSync(configFileName, { encoding: "utf8" });
                let jsonObj: Utils.SimpleObject = JSON.parse(fileContents);

                _loadedConfigFileName = configFileName;
                _loadedConfigFile = this;

                for (let requiredPropName of this._requiredProperties)
                {
                    if (!jsonObj.hasOwnProperty(requiredPropName))
                    {
                        throw new Error(`Required setting '${requiredPropName}' is missing`);
                    }
                }

                for (let propName in jsonObj)
                {
                    if (this.hasOwnProperty("_" + propName))
                    {
                        if (propName === LBOPTIONS_SECTION)
                        {
                            this["_" + propName] = new LanguageBindingOptions(this, jsonObj[propName], true);
                            Object.keys(jsonObj[propName]).forEach(childPropName => this._configuredProperties.push(`${propName}.${childPropName}`));
                        }
                        else
                        {
                            this["_" + propName] = jsonObj[propName];
                            if (typeof jsonObj[propName] === "string")
                            {
                                switch (propName)
                                {
                                    case "icLogStorageType":
                                        // Validate the string enum value
                                        let logStorageType: keyof typeof LogStorageType = jsonObj[propName] as any;
                                        if (LogStorageType[logStorageType] === undefined)
                                        {
                                            throw new Error(`Option '${propName}' has an invalid value ('${logStorageType}'); valid values are: ${Utils.getEnumKeys("LogStorageType", LogStorageType).join(", ")}`);
                                        }
                                        break;
                                    case "icHostingMode":
                                        // Validate the string enum value
                                        let hostingMode: keyof typeof ICHostingMode = jsonObj[propName] as any;
                                        if (ICHostingMode[hostingMode] === undefined)
                                        {
                                            throw new Error(`Option '${propName}' has an invalid value ('${hostingMode}'); valid values are: ${Utils.getEnumKeys("ICHostingMode", ICHostingMode).join(", ")}`);
                                        }
                                        // Note: Unlike _icLogStorageType, we need the actual integer enum value, not the string name of the enum value
                                        this["_" + propName] = ICHostingMode[hostingMode];
                                        break;
                                    case "icIPv4Address":
                                        // Validate the IP address format
                                        let ipAddress: string = jsonObj[propName];
                                        if (!RegExp("^(?:[0-9]{1,3}\.){3}[0-9]{1,3}$").test(ipAddress))
                                        {
                                            throw new Error(`Option '${propName}' has an invalid value ('${ipAddress}')`);
                                        }
                                        break;
                                    case "activeCode":
                                        // Validate the string enum value
                                        let activeCode: keyof typeof ActiveCodeType = jsonObj[propName] as any;
                                        if (ActiveCodeType[activeCode] === undefined)
                                        {
                                            throw new Error(`Option '${propName}' has an invalid value ('${activeCode}'); valid values are: ${Utils.getEnumKeys("ActiveCodeType", ActiveCodeType).join(", ")}`);
                                        }
                                        // Note: Unlike _icLogStorageType, we need the actual integer enum value, not the string name of the enum value
                                        this["_" + propName] = ActiveCodeType[activeCode];
                                        break;
                                    case "autoRegister":
                                        let autoRegisterType: keyof typeof AutoRegisterType = jsonObj[propName] as any;
                                        if (AutoRegisterType[autoRegisterType] === undefined)
                                        {
                                            throw new Error(`Option '${propName}' has an invalid value ('${autoRegisterType}'); valid values are: true, false, "${Utils.getEnumKeys("AutoRegisterType", AutoRegisterType).join("\", \"")}"`);
                                        }
                                        // Note: Unlike _icLogStorageType, we need the actual integer enum value, not the string name of the enum value
                                        this["_" + propName] = AutoRegisterType[autoRegisterType];
                                        break;
                                }
                            }
                            this._configuredProperties.push(propName);
                        }
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

                // Initialize _icHostingMode (if not explicitly provided) 
                if (!this.isConfiguredProperty("icHostingMode"))
                {
                    if (this.isConfiguredProperty("icIPv4Address"))
                    {
                        this._icHostingMode = Utils.isLocalIPAddress(this._icIPv4Address) ? ICHostingMode.Integrated : ICHostingMode.Separated;
                    }
                    else
                    {
                        this._icHostingMode = ICHostingMode.Integrated;
                    }
                }

                if (this.isIntegratedIC && this.icIPv4Address && !Utils.isLocalIPAddress(this.icIPv4Address))
                {
                    throw new Error(`When 'icHostingMode' is set to "${ICHostingMode[ICHostingMode.Integrated]}", the configured 'icIPv4Address' must be a local address (or should be left unspecfied); valid local addresses are: ${Utils.getLocalIPAddresses().join(", ")}`);
                }

                if (!this.isIntegratedIC)
                {
                    // Disallow settings that only apply when icHostingMode is 'Integrated'.
                    // Forcing these settings out of ambrosiaConfig.json reduces confusion for the user, and helps simplify our code.
                    for (const propName of this._configuredProperties)
                    {
                        if (this._propertiesOnlyForIntegratedIC.indexOf(propName) !== -1)
                        {
                            throw new Error(`The '${propName}' setting must be omitted when 'icHostingMode' is \"${ICHostingMode[this._icHostingMode]}\"`);
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
                    let checkpointFileName: string = Path.join(this.icLogFolder, `${this.instanceName}_${this.appVersion}`, `serverchkpt${this.debugStartCheckpoint}`);
                    if (!File.existsSync(checkpointFileName))
                    {
                        throw new Error(`'debugStartCheckpoint' (${this.debugStartCheckpoint}) does not exist (${checkpointFileName})`);
                    }
                }

                if (this.debugTestUpgrade && (this.debugStartCheckpoint === 0))
                {
                    throw new Error(`When 'debugTestUpgrade' is true, a non-zero 'debugStartCheckpoint' must also be specified`);
                }

                if ((this.debugStartCheckpoint !== 0) && !this.isConfiguredProperty("appVersion"))
                {
                    throw new Error(`When a non-zero 'debugStartCheckpoint' is specified, 'appVersion' must also be specified`);
                }

                if (this.isConfiguredProperty("upgradeVersion") && (this.upgradeVersion < this.appVersion))
                {
                    // "Downgrade" is supported by including the downgraded code (and state downgrade conversion) as VNext, while still using an INCREASED upgradeVersion number
                    // in RegisterInstance to prepare for the downgrade. If we don't check for this, RegisterInstance will fail with "Current version # exceeds upgrade version #."
                    throw new Error(`'upgradeVersion' (${this.upgradeVersion}) must be greater than or equal to 'appVersion' (${this.appVersion})`);
                }

                if (this.isLiveUpgradeRequested)
                {
                    if (!this.isConfiguredProperty("autoRegister") || !this.isConfiguredProperty("appVersion") || !this.isConfiguredProperty("activeCode") || // We're going to update these properties [using updateSetting()] when the upgrade completes, so they need to exist in the config file
                        this.isAutoRegister || this.debugTestUpgrade || (this.debugStartCheckpoint !== 0) || (this.activeCode !== ActiveCodeType.VCurrent) || this.lbOptions.deleteLogs)
                    {
                        throw new Error(`When requesting a "live" upgrade ('upgradeVersion' > 'appVersion') the following settings must be configured: 'autoRegister' to false, 'debugTestUpgrade' to false, 'debugStartCheckpoint' to 0, 'activeCode' to "VCurrent", and 'lbOptions.deleteLogs' to false`);
                    }
                }

                if (this.instanceName.trim().length === 0)
                {
                    throw new Error("'instanceName' cannot be empty");
                }

                if (this.icLogFolder)
                {
                    this._icLogFolder = Utils.ensurePathEndsWithSeparator(this.icLogFolder);

                    if (this.icLogStorageType === LogStorageType[LogStorageType.Blobs])
                    {
                        if (File.existsSync(this.icLogFolder))
                        {
                            throw new Error(`When 'icLogStorageType' is "Blobs", the 'icLogFolder' should either be empty or a name of the form "name1/[name2/...]"`);
                        }
                        this._icLogFolder = this.icLogFolder.replace(/\/+|[\\]+/g, "/");
                    }
                }

                if ((this.secureNetworkAssemblyName && !this.secureNetworkClassName) || (this.secureNetworkClassName && !this.secureNetworkAssemblyName))
                {
                    throw new Error("'secureNetworkAssemblyName' and 'secureNetworkClassName' must be provided together or not at all");
                }

                if (this.secureNetworkAssemblyName && !File.existsSync(this.secureNetworkAssemblyName))
                {
                    throw new Error(`The specified 'secureNetworkAssemblyName' (${Path.resolve(this.secureNetworkAssemblyName)}) does not exist`);
                }

                this._allRegistrationOverridesSet = this.isConfiguredProperty("icReceivePort") && this.isConfiguredProperty("icSendPort") && this.isConfiguredProperty("icLogFolder");

                if (this.isAutoRegister && !this._allRegistrationOverridesSet)
                {
                    throw new Error(`When 'autoRegister' is true (or "${AutoRegisterType[AutoRegisterType.TrueAndExit]}"), the following settings must also be explicitly set: icReceivePort, icSendPort, icLogFolder`);
                }

                if ((this.replicaNumber > 0) && !this.isActiveActive)
                {
                    throw new Error(`When 'replicaNumber' (${this.replicaNumber}) is greater than 0, 'isActiveActive' must be set to true`);
                }

                // This is for convenience, and to provide command-line symmetry with "eraseInstance"
                if (Utils.hasCommandLineArg("autoRegister") || Utils.hasCommandLineArg("registerInstance"))
                {
                    if (!this.isIntegratedIC)
                    {
                        throw new Error(`The '${Utils.hasCommandLineArg("autoRegister") ? "autoRegister" : "registerInstance"}' command-line parameter can only be used when icHostingMode is '${ICHostingMode[ICHostingMode.Integrated]}'`);
                    }
                    if (Utils.hasCommandLineArg("autoRegister")) { this._autoRegister = true; }
                    if (Utils.hasCommandLineArg("registerInstance")) { this._autoRegister = AutoRegisterType.TrueAndExit; }
                }
            }
            else
            {
                let howToGetFile: string = Utils.equalIgnoringCase(configFileName, AmbrosiaConfigFile.DEFAULT_FILENAME) ?
                    "; you can copy this file from .\\node_modules\\ambrosia-node\\ambrosiaConfig.json, and then edit it to match your IC registration. " +
                    "If using VS2019+ or VSCode to edit the file, copy ambrosiaConfig-schema.json too." : "";
                throw new Error(`The file does not exist${howToGetFile}`);
            }
        }
        finally
        {
            this._inConstructor = false;
        }
    }

    /** 
     * [Internal] This method should be called by the getter for every setting in the _propertiesOnlyForIntegratedIC list.
     * It throws if the getter being called [from outside the constructor] is for a setting that only applies when icHostingMode is 'Integrated'.
     * The purpose of throwing is to detect the places [primarily] in the LB code where we need a "if (this.isIntegratedIC)" check.
     */
    onlyAppliesToIntegratedICHostingMode(sectionName?: string): void
    {
        if ((this.icHostingMode !== ICHostingMode.Integrated) && !this._inConstructor)
        {
            const settingName: string | undefined = new Error().stack?.split("\n")[2].trim().split(" ")[2]; // TODO: Is there a better way?
            // Typically, these are settings that we (the LB) need to provide to an Ambrosia binary (eg. ImmortalCoordinator.exe / Ambrosia.exe)
            // when we start it; since we're not starting the binary in 'Separated' mode, the setting doesn't apply.
            throw new Error(`The '${sectionName ? sectionName + "." : ""}${settingName || "N/A"}' setting only applies when icHostingMode is '${ICHostingMode[ICHostingMode.Integrated]}'`);
        }
    }

    /** Returns true if the specified property name (eg. "logTriggerSizeInMB" or "lbOptions.outputLoggingLevel") has been set in ambrosiaConfig.json. */
    isConfiguredProperty(propName: string): boolean
    {
        for (const configuredPropName of this._configuredProperties)
        {
            if (Utils.equalIgnoringCase(propName, configuredPropName))
            {
                return (true);
            }
        }
        return (false);
    }

    /** 
     * If needed, updates the [local] configuration settings corresponding to the settings specified at registration (icLogFolder, icSendPort, icReceivePort)
     * by asynchronously reading them from Azure, but only if they have NOT been specified locally (via ambrosiaConfig.json).
     */
    async initializeAsync(): Promise<void>
    {
        const connStr: string | undefined = Process.env["AZURE_STORAGE_CONN_STRING"];
        if (!connStr || (connStr.trim().length === 0))
        {
            throw new Error("The 'AZURE_STORAGE_CONN_STRING' environment variable is missing or empty");
        }

        // If not all of the "primary" registration settings (sendPort/receivePort/logFolder) have been overridden (configured) locally, then we need to query
        // the registration settings from Azure so that we know what values to use [so that we can connect to the IC, and to do - if needed - log deletion].
        // Additionally, if we're not doing TTD, then we need to either check the locally configured appVersion against the registration settings, or - if 
        // appVersion has not been configured locally - acquire the appVersion value from the registration settings. We also need to acquire the registered isActiveActive.
        // Note: Without checking/acquiring appVersion, we could end up [optionally] deleting from a different log folder (the configured version folder) than
        //       the one that will actually be used (the registered currentVersion folder), and then reading/writing logs from an unexpected log folder (the 
        //       registered currentVersion folder).
        // Note: If we decide to abandon AmbrosiaStorage.getRegistrationSettingsAsync(), then when _allRegistrationOverridesSet is false we'd instead have to 
        //       throw a "Not all required settings have been specified" error here, along with adding all the RegistrationSettings members (including appVersion
        //       and isActiveActive) to the "required" setting of ambrosiaConfig-schema.json. We'd also have to forego the 'appVersion' checks below, which would
        //       likely also mean abandoning the lbOptions.deleteLogs feature due to the resulting risks.
        Utils.log("Reading registration settings...");
        const registrationSettings: RegistrationSettings = await AmbrosiaStorage.getRegistrationSettingsAsync(this.instanceName, this.replicaNumber);
        const checkAppVersion: boolean = (this.isIntegratedIC && !this.isTimeTravelDebugging && this.isConfiguredProperty("appVersion")) || !this.isConfiguredProperty("appVersion");

        if (checkAppVersion)
        {
            if (this.isConfiguredProperty("appVersion"))
            {
                // Check appVersion
                // Note: The 'autoRegister' flag gets reset when the re-registration occurs, so if it's true we know that re-registration has not yet happened (but will)                
                if ((registrationSettings.appVersion !== this.appVersion) && !(this.isIntegratedIC && this.isAutoRegister))
                {
                    // Check if the version mismatch is because of an upgrade. We do this by comparing against the 'value' column of the <InstanceTable> 
                    // where PartitionKey = "(Default)" and RowKey = "CurrentVersion", which gets updated (by the IC) after an upgrade.
                    const instanceTableCurrentVersion: number = parseInt(await AmbrosiaStorage.readAzureTableColumn(this.instanceName, "value", "(Default)", "CurrentVersion") as string);
                    if (this.appVersion === instanceTableCurrentVersion)
                    {
                        // Note: Typically, this condition won't happen because we set the 'autoRegister' flag to true when the upgrade succeeds
                        throw new Error(`The instance has been upgraded to version ${instanceTableCurrentVersion}; you must re-register the instance`);
                    }
                    else
                    {
                        throw new Error(`The configured 'appVersion' (${this.appVersion}) differs from the registered version (${registrationSettings.appVersion}); change 'appVersion' to ${registrationSettings.appVersion}`);
                    }
                }
            }
            else
            {
                // Acquire appVersion [so that we can 'autoRegister' (if needed) with the correct value]
                this._appVersion = Utils.assertDefined(registrationSettings.appVersion);
            }

            if (!this.isConfiguredProperty("upgradeVersion"))
            {
                // Acquire upgradeVersion [so that we can 'autoRegister' (if needed) with the correct value]
                this._upgradeVersion = Utils.assertDefined(registrationSettings.upgradeVersion);
            }
        }

        if (this.isIntegratedIC && !this.isTimeTravelDebugging)
        {
            // We need to know if this the "first start of the instance after initial registration" because in this scenario the IC always throws 2 "informational exceptions".
            // TODO: Is there a better (cleaner/faster) way to detect this?
            this._isFirstStartAfterInitialRegistration = await AmbrosiaStorage.isFirstStartAfterInitialRegistration(this.instanceName, this.replicaNumber);
        }

        // Note: These local settings (if set) take precedence over the corresponding registration settings
        if (!this.isConfiguredProperty("icLogFolder") && (registrationSettings.icLogFolder !== undefined))
        {
            this._icLogFolder = registrationSettings.icLogFolder;
        }
        if (!this.isConfiguredProperty("icSendPort") && (registrationSettings.icSendPort !== undefined))
        {
            this._icSendPort = registrationSettings.icSendPort;
        }
        if (!this.isConfiguredProperty("icReceivePort") && (registrationSettings.icReceivePort !== undefined))
        {
            this._icReceivePort = registrationSettings.icReceivePort;
        }

        // Because the "activeActive" IC parameter works by its presence or absence (rather than being an overridable "activeActive=" parameter),
        // if we omit it (ie. when "isActiveActive: false", or when the setting is omitted) then the registered value will be used [by the IC].
        // If the registered value is true, the result (running in active/active when the local config indicates not to) would be unexpected.
        // Note that the converse, ("isActiveActive: true" locally but activeActive false in the registration) is not a problem (this override 
        // would work). But because of this asymmetrical overriding, we simplify and don't allow the configured value to differ from the registered value.
        if (registrationSettings.activeActive !== this._isActiveActive)
        {
            throw new Error(`The configured 'isActiveActive' (${this._isActiveActive}${!this.isConfiguredProperty("isActiveActive") ? " [by omission]" : ""}) does not match the registered value (${registrationSettings.activeActive}); ` +
                            `you must either change the configured value (to ${registrationSettings.activeActive}) or re-register the instance`);
        }

        // The ability to have local overrides for icSendPort and/or icReceivePort increases the likelihood of port collisions
        if ((this.icCraPort === this.icReceivePort) || (this.icCraPort === this.icSendPort) || (this.icSendPort === this.icReceivePort))
        {
            throw new Error(`The icCraPort (${this.icCraPort}), icReceivePort (${this.icReceivePort}), and icSendPort (${this.icSendPort}) must all be different`);
        }
    }

    /** 
     * [Internal] Updates the specified config file setting. The change is applied to both the in-memory setting and the on-disk file.
     * The setting MUST already exist in the config file; this method will not add a missing setting to the file.\
     * If the 'settingName' requires a path, use '.' as the separator character, eg. "lbOptions.deleteLogs".\
     * Note: Many settings are only used at startup, so changing them after calling IC.start() may have no effect until the next restart.\
     * **WARNING:** For internal use only. This method does NOT check that the type of 'value' is correct for the setting.
     */
    updateSetting(settingName: string, value: number | boolean | string): void
    {
        const parts: string[] = settingName.split(".");
        const targetSettingName: string = parts[parts.length - 1];
        let settingGroup: Utils.SimpleObject = this;

        // Navigate to the parent setting group
        for (let i = 0; i < parts.length - 1; i++)
        {
            const subGroup: Utils.SimpleObject = settingGroup["_" + parts[i]];
            if (subGroup !== undefined)
            {
                settingGroup = subGroup;
            }
            else
            {
                throw new Error(`Unknown AmbrosiaConfigFile setting group '${parts.slice(0, i + 1).join(".")}'`);
            }
        }

        // Update the setting
        if (settingGroup["_" + targetSettingName] !== undefined)
        {
            settingGroup["_" + targetSettingName] = value;

            // Update the file
            const lines: string[] = File.readFileSync(_loadedConfigFileName, { encoding: "utf8" }).split(Utils.NEW_LINE);
            let foundInFile: boolean = false;

            for (let i = 0; i < lines.length; i++)
            {
                // TODO: This assumes setting names are unique across all setting groups
                if (lines[i].indexOf(`"${targetSettingName}"`) !== -1)
                {
                    const quote: string = (typeof value === "string") ? "\"" : "";
                    const endsWithComma: boolean = lines[i].trim().endsWith(",");
                    lines[i] = lines[i].split(":")[0] + ": " + quote + value.toString() + quote + (endsWithComma ? "," : "");
                    File.writeFileSync(_loadedConfigFileName, lines.join(Utils.NEW_LINE));
                    foundInFile = true;
                    break;
                }
            }

            // Should not happen [any setting we need to update must be checked in advance that it already exists in the config file using isConfiguredProperty()]
            if (!foundInFile)
            {
                Utils.log(`Error: AmbrosiaConfigFile update failed (reason: Setting '${settingName}' could not be found in ${_loadedConfigFile})`);
            }
        }
        else
        {
            throw new Error(`Unknown AmbrosiaConfigFile setting '${settingName}'`);
        }
    } 
 }

/** The Ambrosia configuration (settings and event handlers) for the app. */
export class AmbrosiaConfig
{
    private _dispatcher: Messages.MessageDispatcher; // Initialized in constructor
    private _checkpointProducer: Streams.CheckpointProducer; // Initialized in constructor
    private _checkpointConsumer: Streams.CheckpointConsumer; // Initialized in constructor
    private _postResultDispatcher: Messages.PostResultDispatcher | null; // Initialized in constructor
    private _configFile: AmbrosiaConfigFile; // The config file that was used to initialize the configuration; Initialized in constructor

    /** [ReadOnly] This handler will be called each time a [dispatchable] message is received. Set via constructor. */
    get dispatcher(): Messages.MessageDispatcher { return (this._dispatcher); }
    /** Note: The property setter is for internal use only. */
    set dispatcher(value: Messages.MessageDispatcher) { this._dispatcher = value; } // Note: Settable so that the IC can wrap the user-supplied dispatcher
    /** [ReadOnly] This method will be called to generate (write) a checkpoint - a binary seralization of application state. Set via constructor. */
    get checkpointProducer(): Streams.CheckpointProducer { return (this._checkpointProducer); }
    /** [ReadOnly] This method will be called to load (read) a checkpoint - a binary seralization of application state. Set via constructor. */
    get checkpointConsumer(): Streams.CheckpointConsumer { return (this._checkpointConsumer); }
    /** [ReadOnly] This handler will be called each time the result (or error) of a post method is received. Set via constructor. */
    get postResultDispatcher(): Messages.PostResultDispatcher | null { return (this._postResultDispatcher); }

    /** [ReadOnly] Whether this is the first run of the instance following its initial registration. Note: This is computed at runtime. */
    get isFirstStartAfterInitialRegistration(): boolean { return (this._configFile.isFirstStartAfterInitialRegistration); }

    /** [ReadOnly] The folder where the Immortal Coordinator (IC) will write its logs (or read logs from if doing "time-travel debugging"). */
    get icLogFolder(): string { return (this._configFile.icLogFolder); }
    /** 
     * [ReadOnly] The storage type that the Immortal Coordinator (IC) logs will be persisted in.\
     * When set to "Blobs", the logs are written to Azure storage (using AZURE_STORAGE_CONN_STRING) in the "Blob Containers\ambrosialogs" container.
     * The icLogFolder can either be set to an empty string, or (rarely) to a desired sub-path under "ambrosialogs", eg. "TestLogs/Group1".
     */
     get icLogStorageType(): keyof typeof LogStorageType { return (this._configFile.icLogStorageType ); }
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
    /** An override IPv4 address for the Immortal Coordinator (IC) to use instead of the local IPv4 address. */
    get icIPv4Address(): string { return (this._configFile.icIPv4Address); }
    /** 
     * [ReadOnly] The hosting mode for the Immortal Coordinator (IC), which affects where and how the IC runs. Defaults to ICHostingMode.Integrated.\
     * If not explicitly set, the value will be computed based on the value provided for 'icIPv4Address'.
     */
     get icHostingMode(): ICHostingMode { return (this._configFile.icHostingMode); }
    /** [ReadOnly] Whether to use .NET Core (instead of .Net Framework) to run the Immortal Coordinator (IC) [this is a Windows-only option]. */
    get useNetCore(): boolean { return (this._configFile.useNetCore); }
    /** [ReadOnly] The checkpoint number to start "time-travel debugging" from. */
    get debugStartCheckpoint(): number { return (this._configFile.debugStartCheckpoint); }
    /** [ReadOnly] Whether to perform a test upgrade (for debugging/testing purposes). Causes the IC to send a MessageType.upgradeService message. */
    get debugTestUpgrade(): boolean { return (this._configFile.debugTestUpgrade); }
    /** [ReadOnly] The size (in MB) the log must reach before the IC will take a checkpoint and start a new log. */
    get logTriggerSizeInMB(): number { return (this._configFile.logTriggerSizeInMB); }
    /** [ReadOnly] Whether this [primary] instance will run in an active/active configuration. */
    get isActiveActive(): boolean { return (this._configFile.isActiveActive); }
    /** [ReadOnly] The replica (secondary) ID this instance will use in an active/active configuration. */
    get replicaNumber(): number { return (this._configFile.replicaNumber); }
    /** [ReadOnly] The name of the .NET assembly used to establish a secure network channel between ICs. */
    get secureNetworkAssemblyName() { return (this._configFile.secureNetworkAssemblyName); }
    /** [ReadOnly] The name of the .NET class (that implements ISecureStreamConnectionDescriptor) in 'secureNetworkAssemblyName'. */
    get secureNetworkClassName() { return (this._configFile.secureNetworkClassName); }
    /** 
     * [ReadOnly] The nominal version of this Immortal instance.\
     * Used to identify the log sub-folder name (ie. &lt;icInstanceName>_&lt;appVersion>) that will be logged to (or read from, if debugStartCheckpoint is specified).
     */
    get appVersion(): number { return (this._configFile.appVersion); }
    /** [ReadOnly] The nominal version this Immortal instance should upgrade to at startup. */
    get upgradeVersion(): number { return (this._configFile.upgradeVersion); }
    /** [ReadOnly] The currently active (in use) application code: pre-upgrade is 'VCurrent', post-upgrade is 'VNext'. */
    get activeCode(): ActiveCodeType { return (this._configFile.activeCode); }
    /** [ReadOnly] How to automatically [re]register this Immortal instance at startup. See also: isAutoRegister. */
    get autoRegister(): boolean | AutoRegisterType { return (this._configFile.autoRegister); }
    /** [ReadOnly] Options for how the language-binding behaves. */
    get lbOptions(): LanguageBindingOptions { return (this._configFile.lbOptions); }

    /** 
     * [ReadOnly] Whether the icHostingMode setting is 'Integrated'.\
     * Note: This computed setting is just for brevity. 
     */
    get isIntegratedIC(): boolean { return (this._configFile.isIntegratedIC); }

    /** [ReadOnly] Whether Ambrosia is running in 'time-travel debugging' mode. */
    get isTimeTravelDebugging(): boolean { return (this._configFile.isTimeTravelDebugging); }

    /** [Readonly] Whether to automatically [re]register this Immortal instance at startup. This is a computed setting. */
    get isAutoRegister(): boolean { return (this._configFile.isAutoRegister); }

    /**
     * Specifies the core Ambrosia event handlers.
     * @param messageDispatcher Function that handles incoming calls to published methods by dispatching them to their implementation. 
     *                          Also dispatches [locally generated] app events to their respective event handlers (if implemented). 
     * @param checkpointProducer Function that returns a Streams.OutgoingCheckpoint object used to serialize app state to a checkpoint.
     * @param checkpointConsumer Function that returns a Streams.IncomingCheckpoint object used to receive a checkpoint of app state.
     * @param postResultDispatcher [Optional] Function that handles the results (and errors) of all post method calls. If post methods are not used, this parameter can be omitted.
     */
    constructor(messageDispatcher: Messages.MessageDispatcher, checkpointProducer: Streams.CheckpointProducer, checkpointConsumer: Streams.CheckpointConsumer, postResultDispatcher?: Messages.PostResultDispatcher)
    {
        this._configFile = loadedConfig();
        this._dispatcher = messageDispatcher;
        this._checkpointConsumer = checkpointConsumer;
        this._checkpointProducer = checkpointProducer;
        this._postResultDispatcher = postResultDispatcher || null;
    }

    /** Returns true if the specified property name has been configured locally (in ambrosiaConfig.json). */
    isConfiguredLocally(propName: string): boolean
    {
        return (this._configFile.isConfiguredProperty(propName));
    }

    /** 
     * [Internal] For internal use only.\
     * Updates the handlers of the configuration (when performing an upgrade). 
     */
    updateHandlers(messageDispatcher: Messages.MessageDispatcher, checkpointProducer: Streams.CheckpointProducer, checkpointConsumer: Streams.CheckpointConsumer, postResultDispatcher?: Messages.PostResultDispatcher): void
    {
        this._dispatcher = messageDispatcher;
        this._checkpointConsumer = checkpointConsumer;
        this._checkpointProducer = checkpointProducer;
        this._postResultDispatcher = postResultDispatcher || null;
    }
}

/** Class representing options for how the language-binding behaves. */
export class LanguageBindingOptions
{
    // Indexer (for 'noImplicitAny' compliance), although this does end up hiding ts(2551): "Property 'xxxxx' does not exist on type 'LanguageBindingOptions'"
    [key: string]: unknown;

    private _parent: AmbrosiaConfigFile; // Initialized in constructor
    private _deleteLogs: boolean = false; 
    private _deleteRemoteCRAConnections: boolean = false;
    private _allowCustomJSONSerialization: boolean = true;
    private _typeCheckIncomingPostMethodParameters: boolean = true;
    private _outputLoggingLevel: Utils.LoggingLevel = Utils.LoggingLevel.Minimal;
    private _outputLogFolder: string = "./outputLogs";
    private _outputLogDestination: OutputLogDestination = OutputLogDestination.Console;
    private _outputLogAllowColor: boolean = true;
    private _traceFlags: string = "";
    private _allowDisplayOfRpcParams: boolean = false;
    private _allowPostMethodTimeouts: boolean = true;
    private _allowPostMethodErrorStacks: boolean = false;
    private _enableTypeScriptStackTraces: boolean = true;
    private _maxInFlightPostMethods: number = -1;
    private _messageBytePoolSizeInMB: number = 2;
    private _maxMessageQueueSizeInMB: number = 256;
    
    /** [ReadOnly] Whether to clear the IC logs (all prior checkpoints and logged state changes will be permanently lost, and recovery will not run). Defaults to false. */
    get deleteLogs(): boolean { this._parent.onlyAppliesToIntegratedICHostingMode(LBOPTIONS_SECTION); return (this._deleteLogs); } 
    /** [ReadOnly] [Debug] Whether to delete any previously created non-loopback CRA connections [from (or to) this instance] at startup. Defaults to false. */
    get deleteRemoteCRAConnections(): boolean {return ( this._deleteRemoteCRAConnections); }
    /** [ReadOnly] Whether to disable the specialized JSON serialization of BigInt and typed-arrays (eg. Uint8Array). Defaults to true. */
    get allowCustomJSONSerialization(): boolean { return (this._allowCustomJSONSerialization); }
    /** [ReadOnly] Whether to skip type-checking the parameters of incoming post methods for correctness against published methods/types. Defaults to true.*/
    get typeCheckIncomingPostMethodParameters(): boolean { return (this._typeCheckIncomingPostMethodParameters); }
    /** [ReadOnly] The level of detail to include in the output log. Defaults to 'Minimal'. */
    get outputLoggingLevel(): Utils.LoggingLevel { return (this._outputLoggingLevel); }
    /** [ReadOnly] The folder where the language-binding will write output log files (when outputLogDestination is 'File' or 'ConsoleAndFile'). Defaults to "./outputLogs". */
    get outputLogFolder(): string { return (this._outputLogFolder); }
    /** [ReadOnly] Location(s) where the language-binding will log output. Defaults to 'Console'. */
    get outputLogDestination(): OutputLogDestination { return (this._outputLogDestination); }
    /** [ReadOnly] Whether to allow the use of color when logging to the console. Defaults to true. */
    get outputLogAllowColor(): boolean { return (this._outputLogAllowColor); }
    /** [ReadOnly] A semi-colon separated list of trace flag names (case-sensitive). Defaults to "". */
    get traceFlags(): string { return (this._traceFlags); }
    /** [ReadOnly] Whether to allow incoming RPC parameters [which can contain privacy/security related content] to be displayed/logged. */
    get allowDisplayOfRpcParams(): boolean { return (this._allowDisplayOfRpcParams); }
    /** [ReadOnly] Whether to enable the timeout feature of post methods. Defaults to true. */
    get allowPostMethodTimeouts(): boolean { return (this._allowPostMethodTimeouts); }
    /** [ReadOnly] Whether to allow sending a full stack trace in the result (as the 'originalError' parameter) if a post method fails. Defaults to false. */
    get allowPostMethodErrorStacks(): boolean { return (this._allowPostMethodErrorStacks); }
    /** [ReadOnly] Whether Error stack trace will refer to TypeScript files/locations (when available) instead of JavaScript files/locations. Defaults to true. */
    get enableTypeScriptStackTraces(): boolean { return (this._enableTypeScriptStackTraces); }
    /** [ReadOnly] Whether to generate a warning whenever the number of in-flight post methods reaches this threshold. Defaults to -1 (no limit). */
    get maxInFlightPostMethods(): number { return (this._maxInFlightPostMethods); }
    /** [ReadOnly] The size (in MB) of the message byte pool used for optimizing message construction. Defaults to 2 MB. */
    get messageBytePoolSizeInMB(): number { return (this._messageBytePoolSizeInMB); }
    /** [ReadOnly] Whether 'outputLoggingLevel' is currently set to 'Debug'. */
    get debugOutputLogging(): boolean { return ( this.outputLoggingLevel === Utils.LoggingLevel.Debug); }
    /** [ReadOnly] The maximum size (in MB) of the message queue for outgoing messages. Defaults to 256 MB. */
    get maxMessageQueueSizeInMB(): number { return (this._maxMessageQueueSizeInMB); }

    // Note: We allow a LanguageBindingOptions instance to be initialized from a "partial" LanguageBindingOptions object, like { deleteLogs: true }
    constructor(parent: AmbrosiaConfigFile, partialOptions?: LanguageBindingOptions, throwOnUnknownOption: boolean = false)
    {
        this._parent = parent;
        for (let optionName in partialOptions)
        {
            if (this["_" + optionName] !== undefined)
            {
                if (optionName === "maxMessageQueueSizeInMB")
                {
                    const maxMessageQueueSizeInMB: number = partialOptions[optionName];
                    const maxMessageQueueSizeInBytes: number = maxMessageQueueSizeInMB * 1024 * 1024;
                    const heapSizeInBytes: number = Utils.getNodeLongTermHeapSizeInBytes();
                    const messageQueueLimitInBytes : number = heapSizeInBytes / 4;
                    if ((maxMessageQueueSizeInBytes < 32 * 1024 * 1024) || (maxMessageQueueSizeInBytes > messageQueueLimitInBytes))
                    {
                        throw new Error(`Option 'maxMessageQueueSizeInMB' (${maxMessageQueueSizeInMB}) must be between 32 and ${Math.floor(messageQueueLimitInBytes / ( 1024 * 1024))}; if needed, set the node.js V8 parameter '--max-old-space-size' to raise the upper limit (see https://nodejs.org/api/cli.html)`);
                    }
                }

                if (typeof partialOptions[optionName] === "string")
                {
                    // Handle the cases where we have the name of the an enum (eg. "Verbose") instead of the [numerical] value.
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
                        case "traceFlags":
                            const traceFlagNames: string[] = partialOptions[optionName].split(";").map(traceFlagName => traceFlagName.trim()).filter(traceFlagName => traceFlagName.length > 0);
                            for (let i = 0; i < traceFlagNames.length; i++)
                            {
                                const traceFlagName: keyof typeof Utils.TraceFlag = traceFlagNames[i] as keyof typeof Utils.TraceFlag;
                                if (Utils.TraceFlag[traceFlagName] === undefined)
                                {
                                    throw new Error(`Option '${optionName}' contains an invalid value ('${traceFlagName}'); valid values are one or more of "${Utils.getEnumKeys("TraceFlag", Utils.TraceFlag).join("\" or \"")}" separated by semi-colons`);
                                }
                            }
                            this["_" + optionName] = traceFlagNames.join(";");
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

/** 
 * [Internal] Asynchronously [re]registers the configured Immortal instance (for example, to prepare it for an upgrade to the configured 'upgradeVersion').\
 * Note: This can take several (3+) seconds to complete.
 */
export async function registerInstanceAsync(isNewRegistration: boolean): Promise<void>
{
    let promise: Promise<void> = new Promise<void>((resolve, reject: RejectedPromise) =>
    {
        // Run "Ambrosia.exe RegisterInstance"
        try
        {
            const config: AmbrosiaConfigFile = _loadedConfigFile;
            // Note: We want to get Ambrosia.exe/dll, so we specify 'isTimeTravelDebugging' as true 
            const registrationExecutable: string = Utils.getICExecutable(config.icBinFolder, config.useNetCore, true); 
            const memStream: Streams.MemoryStream = new Streams.MemoryStream();
            const args: string[] = [];
            const isAddReplica: boolean = config.isActiveActive && (config.replicaNumber > 0);
            const registrationTypePrefix: string = !isNewRegistration ? "Re-" : "";

            // Populate command-line args [we have to re-specify all required args, not just those related to 
            // the upgrade; if not configured locally, these values MUST come from the existing registration]
            args.push(isAddReplica ? "AddReplica" : "RegisterInstance");
            if (isAddReplica)
            {
                // Note: There is no requirement to EVER add replica 0 [see bug #175].
                args.push(`--replicaNum=${config.replicaNumber}`);
            }
            args.push(`--instanceName=${config.instanceName}`);
            args.push(`--receivePort=${config.icReceivePort}`);
            args.push(`--sendPort=${config.icSendPort}`);
            args.push(`--log=${config.icLogFolder}`); // Not a required arg for Ambrosia.exe, but we consider it to be required (see AmbrosiaConfigFile._allRegistrationOverridesSet)

            // Any arbitrary [and previously unused] --currentVersion number can be registered, and the IC will start using that version.
            // After the IC starts using the new version, it will update <instanceTable>.CurrentVersion to that version.
            // However, if the --currentVersion has been used before (as determined [by the IC] by the existence of a log folder for that version), and if the --currentVersion 
            // does not match <instanceTable>.CurrentVersion, then the IC will fail with "FATAL Error 1: Version mismatch on process start" when it starts. This behavior is 
            // designed to prevent accidental use of a previously upgraded-from version (the correct way to "downgrade" is to upgrade to an older version of the code).
            args.push(`--currentVersion=${config.appVersion}`);
            // If --upgradeVersion is not supplied, RegisterInstance will default it to the supplied --currentVersion.
            // After an upgrade completes, the IC will update <instanceTable>.CurrentVersion to --upgradeVersion, but it will not update the registered 'currentVersion' (which must be done manually via re-registration).
            const canOmitUpgradeVersion: boolean = (isNewRegistration && !config.isConfiguredProperty("upgradeVersion"));
            if (!canOmitUpgradeVersion)
            {
                args.push(`--upgradeVersion=${config.upgradeVersion}`);
            }
            
            // Include "optional" args [if these are not set, they will revert to their default values]
            // TODO: Do we need to handle these 2 RegisterInstance settings? They're for testing only.
            //       Further, because these ONLY apply to RegisterInstance (and therefore cannot be provided
            //       to the IC as "local overrides") it would be misleading to allow them in ambrosiaConfig.json.
            // --pauseAtStart
            // --noPersistLogs

            if (config.isConfiguredProperty("logTriggerSizeInMB"))
            {
                args.push(`--logTriggerSize=${config.logTriggerSizeInMB}`);
            }
            // Note: We enforce [in AmbrosiaConfigFile.initializeAsync()] that the locally configured 'isActiveActive' MUST match the
            //       registered value (to prevent it being overridable locally, even though the IC will allow this).
            //       See AmbrosiaConfigFile.initializeAsync() for details.
            if (config.isConfiguredProperty("isActiveActive") && config.isActiveActive)
            {
                args.push(`--activeActive`);
            }

            Utils.log(`${registrationTypePrefix}Registering instance '${config.instanceName}'${isAddReplica ? ` (replica #${config.replicaNumber})` : ""}...`);
            Utils.log(`Args: ${args.join(" ").replace(/--/g, "")}`);

            // Start the process (with no visible console) and pipe both stdout/stderr to memStream
            const registrationProcess: ChildProcess.ChildProcess = ChildProcess.spawn(config.useNetCore ? "dotnet" : registrationExecutable, 
                (config.useNetCore ? [registrationExecutable] : []).concat(args),
                { stdio: ["ignore", "pipe", "pipe"], shell: false, detached: false });

            if (!registrationProcess.stdout || !registrationProcess.stderr)
            {
                throw new Error(`Unable to redirect stdout/stderr for registration executable (${registrationExecutable})`);
            }
            registrationProcess.stdout.pipe(memStream);
            registrationProcess.stderr.pipe(memStream);

            registrationProcess.on("exit", (code: number, signal: NodeJS.Signals) =>
            {
                const buf: Buffer = memStream.readAll();
                // Note: In the case of a successful registration, the output will contain this [somewhat misleading] message:
                //       "The CRA instance appears to be down. Restart it and this vertex will be instantiated automatically".
                const output: string = StringEncoding.fromUTF8Bytes(buf);
                const registrationFailed: boolean = (code !== 0) || (output.indexOf("Usage:") !== -1) || (output.indexOf("Exception") !== -1);
                let suffix: string = "";

                memStream.end();

                if (registrationFailed)
                {
                    const lines: string[] = output.split("\n").filter(l => l.trim().length > 0);
                    const reason: string = (lines[0].indexOf("Usage:") === -1) ? Utils.trimTrailingChar(lines[0], ".") : "Invalid command-line syntax";
                    reject(new Error(`Unable to ${registrationTypePrefix.toLowerCase()}register instance (reason: ${reason} ('${args.join(" ")}'))`));
                }
                else
                {
                    const exitAfterRegistering: boolean = (config.autoRegister === AutoRegisterType.TrueAndExit);
                    
                    // If needed, turn off 'autoRegister'
                    if (config.isAutoRegister)
                    {
                        suffix = ` (auto-register${exitAfterRegistering ? " and exit" : ""})`;
                        config.updateSetting("autoRegister", false);
                    }
                    if (config.isLiveUpgradeRequested)
                    {
                        suffix = ` (for upgrade: v${config.appVersion} to v${config.upgradeVersion})`;
                    }
                    Utils.log(`Instance successfully ${registrationTypePrefix.toLowerCase()}registered${suffix}`);
                    
                    if (exitAfterRegistering)
                    {
                        Process.exit(0);
                    }
                    else
                    {
                        resolve(); // Success
                    }
                }
            });
        }
        catch (error: unknown)
        {
            reject(Utils.makeError(error));
        }
    });
    return (promise);
}

/** 
 * Asynchronously erases the specified Immortal instance and all of its replicas (this includes all Azure data, and all log/checkpoint files).
 * 
 * **WARNING:** Data removed by erasing is **permanently lost**.\
 * This method should **never** be called for a live/production instance.\
 * Only use it during the development and testing phases.\
 * **WARNING:** This method also deletes all log folders for the instance.\
 * **WARNING:** Do not attempt to re-register / restart the erased instance within 30 seconds of running this method, otherwise you may encounter error 409 (Conflict) from Azure.
 */
export async function eraseInstanceAndReplicasAsync(instanceName: string, verboseOutput: boolean = false): Promise<void>
{
    const replicaNumbers: number[] = await AmbrosiaStorage.getReplicaNumbersAsync(instanceName);
    if (replicaNumbers.length === 0)
    {
        Utils.log(`Error: Unable to erase (reason: no instance/replicas named '${instanceName}' found)`);
        return;
    }
    for (let i = 0; i < replicaNumbers.length; i++)
    {
        const replicaNumber: number = replicaNumbers[i];
        await eraseInstanceAsync(instanceName, replicaNumber, verboseOutput);
    }
}

/** 
 * Asynchronously erases the specified Immortal instance (this includes all Azure data, and all log/checkpoint files).
 * 
 * **WARNING:** Data removed by erasing is **permanently lost**.\
 * This method should **never** be called for a live/production instance.\
 * Only use it during the development and testing phases.\
 * **WARNING:** This method also deletes all log folders for the instance.\
 * **WARNING:** Do not attempt to re-register / restart the erased instance within 30 seconds of running this method, otherwise you may encounter error 409 (Conflict) from Azure.
 * 
 * Note: Typically, this executes in under a second, but it can take several seconds.
 */
 export async function eraseInstanceAsync(instanceName: string, replicaNumber: number = 0, verboseOutput: boolean = false): Promise<void>
 {
    const config: AmbrosiaConfigFile = loadedConfig();
    const usingFileSystemLogs: boolean = (config.icLogStorageType === LogStorageType[LogStorageType.Files]);
    const usingAzureLogs: boolean = (config.icLogStorageType === LogStorageType[LogStorageType.Blobs]);
    const fullInstanceName: string = `'${instanceName}'${replicaNumber > 0 ? ` (replica #${replicaNumber})` : ""}`;
    const replicaNumbers: number[] = await AmbrosiaStorage.getReplicaNumbersAsync(instanceName);
    const replicaCount: number = replicaNumbers.length;

    /** [Local function] Always logs the specified message. */
    function log(msg: string): void
    {
        Utils.log(msg, null, Utils.LoggingLevel.Minimal);
    }

    if (replicaNumbers.indexOf(replicaNumber) === -1)
    {
        Utils.log(`Error: Unable to erase (reason: instance ${fullInstanceName} not found)`);
        return;
    }

    log(`Erasing instance ${fullInstanceName}...`);

    if (!config.isConfiguredProperty("icLogFolder") || (usingFileSystemLogs && !config.icLogFolder.trim()))
    {
        throw new Error(`The 'icLogFolder' setting is either not specified or is empty; this setting is required for eraseInstanceAsync() when 'icLogStorageType' is \"${LogStorageType[LogStorageType.Files]}\"`);
    }

    // Delete Azure registration data
    await AmbrosiaStorage.deleteRegisteredInstanceAsync(instanceName, replicaNumber, verboseOutput);

    // Only remove log/checkpoint files when erasing the last replica
    if (replicaCount === 1)
    {
        // Delete all '<instanceName>_<version>' log folders
        // Note: To fully test this, the "deleteLogs" config setting should be set to false (otherwise AmbrosiaRoot.initializeAsync() will have already deleted the current log folder)
        const logFolder: string = config.icLogFolder;
        let instanceDirNames: string[] = [];

        if (usingFileSystemLogs)
        {
            if (!File.existsSync(logFolder))
            {
                Utils.log(`Warning: No logs or checkpoints will be deleted because the 'icLogFolder' (${logFolder}) does not exist`);
            }
            else
            {
                instanceDirNames = File.readdirSync(logFolder, { withFileTypes: true })
                    .filter(dirEntity => dirEntity.isDirectory() && dirEntity.name.startsWith(instanceName + "_")) // Eg. "SomeInstance_0"
                    .map(dirEntity => dirEntity.name);
            }
        }

        if (usingAzureLogs)
        {
            instanceDirNames = await AmbrosiaStorage.getChildLogFoldersAsync(logFolder, instanceName);
        }

        if (instanceDirNames.length > 0)
        {
            for (const dirName of instanceDirNames)
            {
                const instanceLogFolder: string = Path.join(logFolder, dirName);
                const deletedFileCount: number = await IC.deleteInstanceLogFolderAsync(instanceLogFolder, config.icLogStorageType);
                if (verboseOutput)
                {
                    log(`Removed log folder '${instanceLogFolder}' from ${usingAzureLogs ? "Azure" : "disk"} (${deletedFileCount} files deleted)`);
                }
            }
        }
        else
        {
            if (verboseOutput)
            {
                log(`Warning: No log/checkpoint files found`);
            }
        }
    }

    log(`Instance ${fullInstanceName} successfully erased${replicaCount > 1 ? ` (${replicaCount - 1} replicas remain)` : ""}`);
    // See https://docs.microsoft.com/en-us/azure/storage/common/storage-monitoring-diagnosing-troubleshooting?tabs=dotnet#the-client-is-receiving-409-messages
    log(`Warning: Please wait at least 30 seconds before re-registering / starting the instance, otherwise you may encounter HTTP error 409 (Conflict) from Azure`);
}
// Module for general utility methods.
import OS = require("os");
import Process = require("process");
import File = require("fs");
import Path = require("path");
import { EventEmitter } from "events";
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as Configuration from "../Configuration";
import * as Utils from "../Utils/Utils-Index";
import { RejectedPromise } from "../ICProcess"; // There is no re-write issue here as this is just a type

export const ENTER_KEY: string = "\r"; // Same on ALL platforms (Windows, Linux and MacOS)
export const NEW_LINE: string = OS.EOL; // "\r\n" on Windows, "\n" on Linux and MacOS

/** 
 * A utility type that's an object with a property name indexer.\
 * This allows indexed property lookups when using 'noImplicitAny'.
 */
export interface SimpleObject { [key: string]: any }; // Because the indexer returns an explicit 'any' it's no longer an implicit 'any'

/** A type that excludes _undefined_ from T. */
type NeverUndefined<T> = T extends undefined ? never : T;

/** 
 * Throws if the supplied value is _undefined_ (_null_ is allowed).\
 * Returns (via casting) the supplied value as a T with _undefined_ removed from its type space.
 * This informs the compiler that the value cannot be _undefined_, which is useful when 'strictNullChecks' is enabled.
 */
export function assertDefined<T>(value: T, valueNameOrError?: string | Error): NeverUndefined<T>
{
    if (value === undefined)
    {
        throw (valueNameOrError instanceof Error) ? valueNameOrError : new Error(`Encountered unexpected undefined value${valueNameOrError? ` for '${valueNameOrError}'` : ""}`);
    }
    return (value as NeverUndefined<T>);
}

/** 
 * Class used to create a GUID.\
 * Be careful how this is used: it returns a non-deterministic value. 
 */
export class Guid 
{
    /** 
     * Returns a random GUID as a string.\
     * For example "{d30f4d65-9950-4f02-822d-c33c2d88ce72}". 
     */
    static newGuid(): string
    {
        // 4 = version 4 (random), V = variant (encoded, in our case, into the 2 most-significant bits only)
        return ("{xxxxxxxx-xxxx-4xxx-Vxxx-xxxxxxxxxxxx}".replace(/[xV]/g, function replacer(char: string) 
        {
            let randomByteValue: number = Math.floor(Math.random() * 16); // Note: Math.random() will never return 1
            let byteValue: number = (char === 'x') ? randomByteValue : (randomByteValue & 0x3 | 0x8); // For the 'V' byte we set the random variant to 1 (10xx) [see https://en.wikipedia.org/wiki/Universally_unique_identifier]
            return (byteValue.toString(16));
        }));
    }
}

/** Returns true if the specified IPv4 address is an address that refers to the local machine. */
export function isLocalIPAddress(ipv4Address: string): boolean
{
    return (ipv4Address ? getLocalIPAddresses().indexOf(ipv4Address.trim()) !== -1 : false);
}

/** Returns a list of all IPv4 addresses for the local machine, including the loopback address (typically 127.0.0.1). */
export function getLocalIPAddresses(): string[]
{
    const localIPv4Addresses: string[] = [];
    const NICs: NodeJS.Dict<OS.NetworkInterfaceInfo[]> = OS.networkInterfaces();

    for (const name of Object.keys(NICs))
    {
        const nic: OS.NetworkInterfaceInfo[] | undefined = NICs[name];
        if (nic)
        {
            for (const net of nic)
            {
                if (net.family === "IPv4")
                {
                    localIPv4Addresses.push(net.address);
                }
            }
        }
    }    
    return (localIPv4Addresses);
}

/** Returns the specified XML as a formatted string (with indenting and new-lines). */
export function formatXml(xml: string, tab: string = "  ")
{
    // See https://stackoverflow.com/questions/376373/pretty-printing-xml-with-javascript/ [in the post by 'arcturus' (https://stackoverflow.com/a/49458964)]
    let formattedXml: string = "";
    let indent: string = "";

    xml.split(/>\s*</).forEach((node: string) =>
    {
        if (node.match(/^\/\w/)) // Eg. "/test>"
        {
            indent = indent.substring(tab.length); // Decrease indent by one 'tab'
        } 
        formattedXml += `${indent}<${node}>${OS.EOL}`;
        if (node.match(/^<?\w[^>]*[^\/]$/)) // Eg. "<test" (Note: Only the first [split] node will start with '<')
        {
            indent += tab; // Increase indent by one 'tab'
        } 
    });
    return (formattedXml.substring(1, formattedXml.length - (1 + OS.EOL.length))); // Remove extraneous leading '<' and trailing '>\r\n'
}

/** Encodes a string so that it can be used as an XML value (eg. as an attribute value or as entity content). */
export function encodeXmlValue(value: string)
{
    return (value.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&apos;"));
}

/** Decodes XML which may contain values encoded using encodeXmlValue(). */
export function decodeXml(xml: string)
{
    return (xml.replace(/&apos;/g, "'").replace(/&quot;/g, '"').replace(/&gt;/g, '>').replace(/&lt;/g, '<').replace(/&amp;/g, '&'));
}

/** Escapes a string (that might contain RegExp special characters) for use in a 'new RegExp()' (or 'RegExp()') call. */
export function regexEscape(value: string): string
{
    return (value.replace(/[-\/\\^$*+?.()|[\]{}]/g, "\\$&"));
}

/**
 * Defines the type of a compiled 'enum' object in TypeScript 3.6 (an object with string keys (property names) and string or number property values).
 * This allows enums to be processed as objects when needed, but may break in future versions of TypeScript.
 */
export interface EnumType
{
    [key: string]: string | number;
}

/** 
 * Returns the available values of an specified enum type (eg. "1=Foo,2=Bar").\
 * Will throw if enumType contains any non-integer valued members.
 */
export function getEnumValues(typeName: string, enumType: EnumType): string
{
    let enumValues: string = "";

    checkEnumForNonIntegerValues(typeName, enumType);
    for (let propName in enumType)
    {
        if (typeof enumType[propName] === "number")
        {
            enumValues += `${enumValues.length === 0 ? "" : ","}${enumType[propName]}=${propName}`;
        }
    }
    return (enumValues);
}

/** 
 * Returns all the value names (keys) from the specified enum.\
 * Will throw if enumType contains any non-integer valued members.
 */
export function getEnumKeys(typeName: string, enumType: EnumType): string[]
{
    const enumKeys: string[] = [];

    checkEnumForNonIntegerValues(typeName, enumType);
    for (const propName in enumType)
    {
        if (typeof enumType[propName] === "number")
        {
            enumKeys.push(propName);
        }
    }
    return (enumKeys);
}

/** Throws if the specified enum contains any non-integer valued members. */
function checkEnumForNonIntegerValues(typeName: string, enumType: EnumType): void
{
    let nonIntegerValuedKeys: string[] = Object.keys(enumType).filter(k => !Number.isInteger(parseInt(k)) && !RegExp("^-?[0-9]+$").test(enumType[k].toString()));
    if (nonIntegerValuedKeys.length > 0)
    {
        throw new Error(`The '${typeName}' enum contains a member ('${nonIntegerValuedKeys[0]}') which does not have an integer value ('${enumType[nonIntegerValuedKeys[0]]}')`);
    }
}

const _knownCommandLineArgs: string [] = ["ambrosiaConfigFile", "autoRegister", "registerInstance", "eraseInstance", "eraseInstanceAndReplicas"];

/** 
 * Returns true if the named command-line argument (eg. "arg1") is present without a value. Use this to detect an argument that does not require a value; 
 * its presence alone is sufficient (eg. "--help"). See also: getCommandLineArg().
 * 
 * Most command-line args will belong to the app; Ambrosia uses ambrosiaConfig.json to specify start-up parameters.
 * 
 * Note: 'argName' does not need to match on case. Further, 'argName' is a specifier so it can be of the form "[-]acronym|fullName" in addition to simply "name".
 * For example, "-t|test" means the name can be either "-t" or "--test", and "fn|fileName" means either "fn" or "fileName".
 */
export function hasCommandLineArg(argName: string): boolean
{
    return (getCommandLineArg(argName, "[N/A]") === "[present]");
}

/** 
 * Returns the value of a named command-line argument (eg. "arg1=foo"), or returns 'defaultValue' if the named argument is not found. If 'defaultValue' is not
 * supplied, this indicates that the argument is required and the method will throw if it's missing from the command-line. See also: hasCommandLineArg().
 * 
 * Most command-line args will belong to the app; Ambrosia uses ambrosiaConfig.json to specify start-up parameters.
 * 
 * Note: 'argName' does not need to match on case. Further, 'argName' is a specifier so it can be of the form "[-]acronym|fullName" in addition to simply "name".
 * For example, "-t|test" means the name can be either "-t" or "--test", and "fn|fileName" means either "fn" or "fileName".
 */
export function getCommandLineArg(argName: string, defaultValue?: string): string
{
    argName = argName.replace(/\s/g, "");

    const args: string[] = Process.argv;
    const isRequired: boolean = (defaultValue === undefined);
    const requiresLeadingDash: boolean = argName.startsWith("-");
    let argNameVariants: string[] = [];

    if (argName.split("|").length > 2)
    {
        throw new Error(`Malformed Ambrosia command-line parameter name specifier '${argName}'; Too many '|' characters`);
    }

    if (argName.indexOf("|") !== -1)
    {
        // Note: We want to allow "-f|foo-bar", but not "--f|foo-bar"
        if (argName.startsWith("--"))
        {
            throw new Error(`Malformed Ambrosia command-line parameter name specifier '${argName}'; Too many '-' characters`);
        }
        // Eg. "-acf|ambrosiaConfigFile" becomes ["-acf", "--ambrosiaConfigFile"]
        //     "acf|ambrosiaConfigFile" becomes ["acf", "ambrosiaConfigFile"]
        argNameVariants = argName.replace(/^-*/g, "").split("|").map((name, i) => (requiresLeadingDash ? "-".repeat(i + 1) : "") + name);
    }
    else
    {
        argNameVariants.push(argName);
    }

    // We keep track of all "known" arg names so that getUnknownCommandLineArg() can work
    for (const argName of argNameVariants)
    {
        if (_knownCommandLineArgs.indexOf(argName) === -1)
        {
            _knownCommandLineArgs.push(argName);
        }
    }

    // 'args' will be: [NodeExe] [JSFile] [Arg1] [Arg2] ...
    // eg. "C:\Program Files\nodejs\node.exe C:\src\Git\AMBROSIA\Clients\AmbrosiaJS\TestApp-Node\out\TestApp.js appArg1 appArg2 appArg3 ambrosiaConfigFile=testConfig.json"
    for (let i = 2; i < args.length; i++)
    {
        if (args[i].indexOf("=") !== -1)
        {
            let parts: string[] = args[i].split("=");
            let name: string = parts[0];
            let value: string = parts[1];

            for (let v = 0; v < argNameVariants.length; v++)
            {
                const argNameVariant: string = argNameVariants[v];
                
                if (equalIgnoringCase(name, argNameVariant))
                {
                    if (isRequired && (value.length === 0))
                    {
                        throw new Error(`No value was specified for the required Ambrosia command-line parameter '${argNameVariant}'`);
                    }
                    if (defaultValue === "[N/A]")
                    {
                        throw new Error(`Malformed Ambrosia command-line parameter '${args[i]}'; An "=(value)" is not expected`);
                    }
                    return (value);
                }

                // Is the arg missing leading dash(es)?
                if (equalIgnoringCase(name, argNameVariant.replace(/^-*/g, "")))
                {
                    throw new Error(`Malformed Ambrosia command-line parameter '${name}'; Expected ${argNameVariant}`);
                }
            }
        }
        else
        {
            if (argNameVariants.indexOf(args[i]) !== -1)
            {
                // This is how we handle args like "h|help" which simply need to be present to have meaning (and so don't follow the "arg=value" format)
                if (defaultValue === "[N/A]")
                {
                    return ("[present]");
                }
                throw new Error(`Malformed Ambrosia command-line parameter '${args[i]}'; Missing "=(value)"`);
            }
        }
    }

    if (defaultValue === undefined) // We can't use 'isRequired' here because we need to make the compiler happy when using 'strictNullChecks'
    {
        throw new Error(`The Ambrosia command-line parameter '${argNameVariants.join("' or '")}' is required`);
    }
    return (defaultValue);
}

/** 
 * Returns the names of command-line parameters that are known to either Ambrosia, or to your app 
 * (**after** retrieving all valid app parameters using getCommandLineArg() and/or hasCommandLineArg()).
 */
export function getKnownCommandLineArgs(): readonly string[]
{
    return (_knownCommandLineArgs);
}
 
/** 
 * Returns the first unknown command-line parameter name (if any).\
 * Otherwise, returns null.
 * Should only be called **after** retrieving all valid app parameters with getCommandLineArg() and/or hasCommandLineArg().
 */
export function getUnknownCommandLineArg(): string | null
{
    const args: string[] = Process.argv;
    for (let i = 2; i < args.length; i++)
    {
        const argName: string = (args[i].indexOf("=") !== -1) ? args[i].split("=")[0] : args[i];
        if (_knownCommandLineArgs.indexOf(argName) === -1)
        {
            return (argName);
        } 
    }
    return (null);
}

/** Converts an array of bytes to a string to enable the byte array to be viewed. Set 'base' to 16 to view as hex. The string will contain, at most, maxDisplayBytes (defaults to 2048). */
export function makeDisplayBytes(bytes: Uint8Array, startIndex: number = 0, length: number = bytes.length - startIndex, base: number = 10, maxDisplayBytes: number = 2048): string
{
    let bytesAsString: string = "";
    let requestedEndIndex: number = Math.min(startIndex + length, bytes.length);
    let endIndex: number = requestedEndIndex;
    let outputTruncated: boolean = (endIndex - startIndex) > maxDisplayBytes;

    if (outputTruncated)
    {
        endIndex = startIndex + maxDisplayBytes - 1;
    }

    for (let i = startIndex; i < endIndex; i++)
    {
        switch (base)
        {
            case 16: 
                bytesAsString += ("0" + bytes[i].toString(base)).slice(-2) + " ";
                break;
            case 10:
                bytesAsString += bytes[i].toString(base) + " ";
                break;
            default:
                throw new Error("Only base 16 (hex) and base 10 (decimal) are currently supported");
        }
    }

    if (outputTruncated)
    {
        let omitted: string = ((requestedEndIndex - endIndex - 1) > 0) ? `...(${requestedEndIndex - endIndex - 1} bytes omitted)... ` : "";
        bytesAsString += `${omitted}${bytes[requestedEndIndex - 1]}`;
    }

    return (bytesAsString.trim());
}

/** Return a byte value (0..255) as a fixed 2-digit hex string. */
export function byteToFixedHex(byte: number)
{
    return (("0" + (byte & 0xFF).toString(16)).slice(-2));
}

/** Returns true if the 2 specified strings are equal, regardless of case (but not accent). So "Ambrosia" = "AMBROSIA", but "ámbrosiá" != "ambrosia". */
export function equalIgnoringCase(s1: string, s2: string): boolean
{
    return ((s1.length !== s2.length) ? false : (s1.toLowerCase() === s2.toLowerCase()));
}

/** Deletes [synchronously] the specified fully-pathed file, if it exists. Returns true if the file was deleted. */
export function deleteFile(fullyPathedFileName: string): boolean
{
    if (File.existsSync(fullyPathedFileName))
    {
        File.unlinkSync(fullyPathedFileName); 
        return (true);
    }
    return (false);
}

/** 
 * Creates a [test] file of arbitrary size, overwriting the file if it already exists. 
 * The last byte will always be 0x7F (127). Returns the supplied fileName.
 * Example usage: let data: Uint8Array = fs.readFileSync(Utils.createTestFile("./TestCheckpoint.dat", 2050));
 */
export function createTestFile(fileName: string, sizeInBytes: number, byteValue: number = -1): string
{
    let data: Uint8Array = new Uint8Array(sizeInBytes);
    for (let i = 0; i < sizeInBytes; i++)
    {
        data[i] = (byteValue === -1) ? i % 256 : (byteValue % 256);
    }
    data[sizeInBytes - 1] = 127; // 0x7F 
    File.writeFileSync(fileName, data);
    return (fileName);
}

/** Returns the size of the specified file. */
export function getFileSize(fileName: string): number
{
    return (File.statSync(fileName)["size"]);
}

/** Returns the 'last modified' date of the file. For an executable binary [which doesn't ever change], 'last modified ' is effectively the true creation date. */
export function getFileLastModifiedTime(fileName: string): Date
{
    let creationTime: Date = new Date(File.statSync(fileName)["mtimeMs"]); 
    return (creationTime);
}

/** 
 * Creates (or restarts) a one-time only timer. The timer is restarted (using the original timeout)
 * at each subsequent call until the timer ticks, after which the timer will no longer restart.
 * Returns the timer, which is created if the supplied timer is null.
 */
export function restartOnceOnlyTimer(timer: NodeJS.Timeout & SimpleObject, timeoutInMs: number, onTick: () => void): NodeJS.Timeout
{
    if (timer && timer["__hasTicked__"] === true)
    {
        // The timer has already ticked
        return (timer);
    }

    if (!timer)
    {
        // Create the timer
        let newTimer: NodeJS.Timeout & SimpleObject = setTimeout(() => 
        {
            newTimer["__hasTicked__"] = true;
            onTick();
        }, timeoutInMs);
        return (newTimer);
    }
    else
    {
        // The timer is already running
        timer.refresh(); // Restart the timer
        return (timer);
    }
}

/** 
 * Returns the size (in bytes) of node's long-term ("old") GC heap.
 * 
 * If needed, set the V8 parameter '--max-old-space-size' to raise the size of the heap.
 */
export function getNodeLongTermHeapSizeInBytes(): number
{
    // Related: https://nodejs.org/api/cli.html#cli_max_old_space_size_size_in_megabytes
    //          https://github.com/nodejs/node/issues/7937
    // If node's --max-old-space-size parameter is left at its default value (0), node will use [up to] 1400MB on a 64 bit OS (or 700MB on an 32-bit OS)
    // for the GC's "old generation" heap (see https://v8.dev/blog/trash-talk), which is where objects that survive 2 GC's will end up
    // (see https://github.com/nodejs/node/blob/ec02b811a8a5c999bab4de312be2d732b7d9d50b/deps/v8/src/heap/heap.cc#L82).
    const is64BitNodeExe: boolean = RegExp("64").test(OS.arch());
    const v8MaxOldSpaceSize: number = parseInt(Utils.getCommandLineArg("--max-old-space-size", "0"));
    const nodeMaxOldGenerationSize: number = v8MaxOldSpaceSize ? v8MaxOldSpaceSize : ((is64BitNodeExe ? 1400 : 700) * 1024 * 1024);
    return (nodeMaxOldGenerationSize);
}

/** A dictionary of the currently active spin waits, which are used to wait (using a timer) for a [non-awaitable] asynchronous operation to complete. */
let _spinWaiters: { [waitName: string]: { timer: NodeJS.Timeout, iteration: number, startTime: number, intervalInMs: number } } = {}; // Key = Name of operation being waited on, value = Spin-wait details

/**
 * Waits for a [non-awaitable] asynchronous operation to complete. Calls continueWaiting() every waitIntervalInMs, which can be no smaller than 50ms.\
 * When continueWaiting() returns false, onWaitComplete() will be called - which, typically, should simply re-call the caller of spinWait().\
 * If timeoutInMs (defaults to 8000ms) elapses without continueWaiting() returning false, the wait will be aborted and onWaitTimeout() - if supplied -
 * will be called. Set timeoutInMs to -1 for no timeout.\
 * Returns true if a wait is required (ie. the caller should not continue) or false otherwise (ie. the caller can continue). 
 */
export function spinWait(waitName: string, continueWaiting: () => boolean, onWaitComplete: () => void, waitIntervalInMs: number = 100, timeoutInMs: number = 8000, onWaitTimeout?: (elapsedMs: number) => void): boolean
{
    if (!continueWaiting())
    {
        return (false); // No need to wait (ie. the caller can continue)
    }

    // Ensure waitIntervalInMs is at least 50ms (to avoid a "tight-spin" on whatever continueWaiting() does)
    waitIntervalInMs = Math.max(50, waitIntervalInMs);

    // Ensure that timeoutInMs is not less than waitIntervalInMs
    if (timeoutInMs !== -1)
    {
        timeoutInMs = Math.max(waitIntervalInMs, timeoutInMs);
    }

    Utils.log(`Waiting for '${waitName}'...`);
    
    if (_spinWaiters[waitName] === undefined)
    {
        let timer: NodeJS.Timeout = setInterval(function checkSpinWait() 
        {
            if (!continueWaiting())
            {
                // We can stop waiting
                clearInterval(_spinWaiters[waitName].timer);
                delete _spinWaiters[waitName];
                Utils.log(`Wait for '${waitName}' complete`);
                onWaitComplete();
            }
            else
            {
                // Keep waiting (but only until timeoutInMs is reached, unless the timeout is -1 [infinite])
                _spinWaiters[waitName].iteration++;
                let totalElapsedMs: number = Date.now() - _spinWaiters[waitName].startTime;
                if ((timeoutInMs !== -1) && (totalElapsedMs >= timeoutInMs))
                {
                    Utils.log(`Error: spinWait '${waitName}' timed out after ${totalElapsedMs}ms (${_spinWaiters[waitName].iteration} iterations of ${waitIntervalInMs}ms)`);
                    clearInterval(_spinWaiters[waitName].timer);
                    delete _spinWaiters[waitName];
                    if (onWaitTimeout)
                    {
                        onWaitTimeout(totalElapsedMs);
                    }
                }
            }
        }, waitIntervalInMs);

        _spinWaiters[waitName] = { timer: timer, iteration: 0, startTime: Date.now(), intervalInMs: waitIntervalInMs };
    }
    else
    {
        throw new Error(`The waitName '${waitName}' already exists`);
    }

    return (true); // Wait required (ie. the caller should NOT continue)
}

/** Returns the fully qualified path to the IC executable. Will throw if the path/executable does not exist. */
export function getICExecutable(ambrosiaToolsDir: string = "", useNetCore: boolean = false, isTimeTravelDebugging: boolean = false): string
{
    let fullyPathedExecutable: string = "";
    let fullyPathedExecutableFallback: string = "";
    let executableName: string = isTimeTravelDebugging ? "Ambrosia" : "ImmortalCoordinator";
    let ambrosiaToolsDirs: string[] = [];
    let exMessages: string[] = [];

    if (ambrosiaToolsDir)
    {
        for (let dir of ambrosiaToolsDir.split(";"))
        {
            dir = dir.trim();
            if (!File.existsSync(dir))
            {
                throw new Error(`The specified 'ambrosiaToolsDir' (${dir}) does not exist; check that the 'icBinFolder' setting is correct in ${Configuration.loadedConfigFileName()}`);
            }
            ambrosiaToolsDirs.push(dir);
        }
    }
    else
    {
        if (!Process.env["AMBROSIATOOLS"])
        {
            throw new Error("The 'AMBROSIATOOLS' environment variable is missing");
        }
        ambrosiaToolsDir = Process.env["AMBROSIATOOLS"];
        if (!File.existsSync(ambrosiaToolsDir))
        {
            throw new Error(`The 'AMBROSIATOOLS' environment variable references a folder (${ambrosiaToolsDir}) that does not exist`);
        }
        ambrosiaToolsDirs.push(ambrosiaToolsDir);
    }

    for (const dir of ambrosiaToolsDirs)
    {
        if (isWindows())
        {
            // Note: In .Net Core 2.1 is was necessary to run the app (a [potentially] cross-platform DLL) using "dotnet <filename.dll>",
            //       and this still works in later versions of .Net Core. However, from .Net Core 3.0 onwards, if the .csproj specifies
            //       <OutputType>Exe</OutputType> (which ImmortalCoordinator.csproj does) it also produces a platform-specific executable
            //       (eg. an .exe on Windows) that will launch the app (as an alternative to, but not a replacement for, using "dotnet <filename.dll>"
            //       as the launcher). We continue to use the "dotnet <filename.dll>" approach to start the IC [when 'useNetCore' is true] - see IC.start().
            fullyPathedExecutable = Path.join(dir, `${useNetCore ? "netcoreapp3.1" : "net461"}`, `${executableName}.${useNetCore ? "dll" : "exe"}`);
            // The "fallback" folder handles the case where the user has copied the binaries to a different location than either the local build folder (which
            // has net461/netcoreapp3.1 folders) or the folder structure in Ambrosia-win-x64.zip from https://github.com/microsoft/AMBROSIA/releases (which 
            // also has net461/netcoreapp3.1 folders)
            fullyPathedExecutableFallback = Path.join(dir, `${executableName}.${useNetCore ? "dll" : "exe"}`);
        }
        else
        {
            // Note: The pre-built Ambrosia-linux.tgz (https://github.com/microsoft/AMBROSIA/releases) doesn't have 
            //       either "netcoreapp3.1" or "net461" folders (even though Ambrosia-win-x64.zip does), because the
            //       .Net Framework (eg. net461) is for Windows ONLY (Linux can only use .Net Core).
            //       For non-Windows platforms, the pre-built binaries (eg. from Ambrosia-linux.tgz) are published as 
            //       framework-dependent apps (see https://docs.microsoft.com/en-us/dotnet/core/deploying/#publish-framework-dependent).
            //       This produces a cross-platform binary as a dll file, and a platform-specific executable that targets
            //       the specified platform (eg. "Linux-x64"). 
            //       Note that executables (ie. the equivalent of a .exe on Windows) have no extension on Linux and MacOS.
            fullyPathedExecutable = Path.join(dir, executableName); // Eg. "/ambrosia/bin/ImmortalCoordinator"
        }

        if (!File.existsSync(fullyPathedExecutable))
        {
            if (fullyPathedExecutableFallback)
            {
                if (!File.existsSync(fullyPathedExecutableFallback))
                {
                    exMessages.push(`The computed executable path (${fullyPathedExecutableFallback}) does not exist`);
                }
                else
                {
                    fullyPathedExecutable = fullyPathedExecutableFallback;
                    exMessages.length = 0;
                    break;
                }
            }
            else
            {
                exMessages.push(`The computed executable path (${fullyPathedExecutable}) does not exist`);
            }
        }
        else
        {
            exMessages.length = 0;
            break;
        }
    }

    if (exMessages.length > 0)
    {
        throw new Error(exMessages.join("; "));
    }

    return (fullyPathedExecutable);
}

/** Ensures that the supplied path ends with the path-separator character (eg. "/"). */
export function ensurePathEndsWithSeparator(path: string): string
{
    if (path)
    {
        path = path.trim();
        if (path.trim().length > 0)
        {
            let separator: string = (path.indexOf("\\") !== -1) ? "\\" : "/";
            if (!path.endsWith(separator))
            {
                path += separator;
            }
        }
    }
    return (path);
}

/** Returns the specified 'value' with any trailing 'char' character removed. */
export function trimTrailingChar(value: string, char: string): string
{
    if (value.substr(value.length - 1, 1) === char[0])
    {
        return (value.substr(0, value.length - 1));
    }
    return (value);
}

let _isJsPlatformNode: boolean | null = null;

/** Returns true if the JavaScript platform is Node.js. */
export function isNode(): boolean
{
    if (_isJsPlatformNode === null)
    {
        _isJsPlatformNode = (typeof process === "object") && (typeof process.versions === "object") && (typeof process.versions.node !== "undefined");
    }
    return (_isJsPlatformNode);
}

/** Returns true if OS is little-endian. */
export function isLittleEndian(): boolean
{
    return (OS.endianness() === "LE");
}

/** Returns true if the OS is Windows. */
export function isWindows(): boolean
{
    return (OS.platform() === "win32");
}

/** 
 * Starts accepting user input from the terminal. Calls the supplied callback whenever a character is entered at the terminal (stdin).\
 * Note: If using VSCode, using this method requires this setting in launch.json:\
 * _"console": "integratedTerminal"_
 */
export function consoleInputStart(callback: (char: string) => void)
{
    // When Node.js detects that it is being run with a text terminal ("TTY") attached, process.stdin will, by default, be initialized as an instance of tty.ReadStream
    if (Process.stdin.isTTY)
    {
        Process.stdin.setRawMode(true); // Process input character-by-character (rather than waiting until 'Enter' is pressed)
        Process.stdin.setEncoding("utf8"); // Specify that the input is string (character) data, not Buffer data
        Process.stdin.on("data", function onCharacter(key: string)
        {
            callback(key);
        });
    }
    else
    {
        throw new Error("No text terminal is attached to Node; if using VSCode, try setting \"console\": \"integratedTerminal\" in launch.json");
    }
}

/**
 * Asynchronously waits for one of the specified 'chars' keys to pressed.\
 * To wait for any key, omit the 'chars' parameter. Returns the key pressed.\
 * 'chars' will always be converted to lower-case.\
 * When using this method, do **not** also use consoleInputStart().\
 * Note: If using VSCode, using this method requires this setting in launch.json:\
 * _"console": "integratedTerminal"_
 */
export async function consoleReadKeyAsync(chars: string[] = []): Promise<string>
{
    let promise: Promise<string> = new Promise<string>((resolve, reject: RejectedPromise) => 
    {
        try
        {
            for (const char of chars)
            {
                if (!((char.length === 1) || ((char.length === 2) && (char[0] === "\\"))))
                {
                    throw new Error(`Invalid 'chars' value "${char}"; only single character strings are allowed`);
                }
            }
            const lowerCaseChars: string[] = chars.map(ch => ch.toLowerCase());
            consoleInputStart((char: string) =>
            {
                try
                {
                    char = char.toLowerCase();
                    if ((lowerCaseChars.length === 0) || (lowerCaseChars.indexOf(char) !== -1))
                    {
                        resolve(char);
                        consoleInputStop();
                    }
                }
                catch (error: unknown)
                {
                    reject(Utils.makeError(error));
                    consoleInputStop();
                }
            });
        }
        catch (error: unknown)
        {
            reject(Utils.makeError(error));
            consoleInputStop();
        }
    });
    return (promise);
}

/** Stops accepting user input from the terminal, as started by calling consoleInputStart(). */
export function consoleInputStop()
{
    if (Process.stdin.isTTY)
    {
        Process.stdin.push(null);
    }
}

/** 
 * [Internal] Basic utility method to measure performance.\
 * Returns the number of elapsed milliseconds for 'iterations' executions of 'task'. 
 */
export function basicPerfTest(task: (iteration?: number) => void, iterations: number, logResult: boolean = true): number
{
    const startTime: number = Date.now();

    for (let i = 0; i < iterations; i++)
    {
        task(i);
    }

    const elapsedMs: number = Date.now() - startTime;
    if (logResult)
    {
        Utils.log(`Elapsed: ${elapsedMs}ms (for ${iterations.toLocaleString()} iterations); ${Math.ceil(elapsedMs / iterations)}ms average`);
    }
    return (elapsedMs);
}

/** Returns the FNV-1a (Fowler/Noll/Vo) 32-bit hash value for the specified Buffer. */
// See: https://papa.bretmulvey.com/post/124027987928/hash-functions
// FNV test cases can be found here: http://www.isthe.com/chongo/src/fnv/test_fnv.c        
// Spot checks [test #38]: console.log(Utils.computeHash(Buffer.from("chongo was here!")).toString(16)); // Should return 448524fd
//                       : console.log(Utils.computeHash(Buffer.alloc((8 * 1024 * 1024)).fill(205)).toString(16)); // Should return 8b9c9dc5
function computeHash(data: Buffer): number
{
    const FNV_32_PRIME: number = 16777619;
    const FNV_32_OFFSET_BASIS: number = 2166136261;
    const numBytes: number = data.byteLength;
    let hash: number = FNV_32_OFFSET_BASIS;

    for (let i = 0; i < numBytes; i++)
    {
        hash = multiply_uint32(hash ^ data[i], FNV_32_PRIME);
    }
    return (hash);

    /** [Local function] Multiplies two unsigned 32-bit integers, and returns the "wrapped" result as a Uint32. */
    // See https://stackoverflow.com/questions/6232939/is-there-a-way-to-correctly-multiply-two-32-bit-integers-in-javascript/6422061
    function multiply_uint32(a: number, b: number): number
    {
        const aHigh: number = (a >> 16) & 0xffff;
        const aLow: number = a & 0xffff;
        const bHigh: number = (b >> 16) & 0xffff;
        const bLow: number = b & 0xffff;
        const high = ((aHigh * bLow) + (aLow * bHigh)) & 0xffff;
        return ((((high << 16) >>> 0) + (aLow * bLow)) >>> 0); // Note: Using '>>> 0' to "cast" the result to a Uint32
        
        // Note: To do the complete multiplication (ie. a * b):
        // result = (((aHigh << 16) >>> 0) * ((bHigh << 16) >>> 0)) + 
        //          (((aHigh << 16) >>> 0) * bLow) + 
        //          (aLow * ((bHigh << 16) >>> 0)) + 
        //          (aLow * bLow);
    }
}

/** 
 * Base exception for all errors raised by Ambrosia. 
 * Note: We don't use this because (in VSCode at least) the top of the call stack will 
 *       always be reported (file/line) as the constructor method below, making it harder 
 *       to jump to the method that actually threw the exception.
/*
export class AmbrosiaException extends Error
{
    constructor(message: string, stack?: string)
    {
        super(message);
        this.name = "AmbrosiaException";
        if (stack)
        { 
            this.stack = stack;
        }
    }
}
*/

/** 
 * A utility class used to wrap an async function as a callback function.\
 * The async method should call complete() on the supplied AsyncCompleteWrapper from its 'finally' block.\
 * The async method must also have a 'catch' block to enable it to be called synchronously (ie. without being awaited).\
 * Note: If the wrapped async function doesn't call await, it will run synchronously then call onComplete().
 */
export class AsyncCompleteWrapper extends EventEmitter
{
    private static readonly ASYNC_OP_COMPLETE_EVENT: string = "AsyncOpComplete";

    constructor(onComplete: (error?: Error) => void)
    {
        super();
        this.once(AsyncCompleteWrapper.ASYNC_OP_COMPLETE_EVENT, onComplete);
    }

    /** Called from the 'finally' block of an async method to signal that the async operation has completed (either successfully, or with an error). */
    complete(error?: Error): void
    {
        this.emit(AsyncCompleteWrapper.ASYNC_OP_COMPLETE_EVENT, error);
    }
}
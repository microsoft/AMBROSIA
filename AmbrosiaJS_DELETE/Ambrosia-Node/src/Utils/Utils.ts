// Module for general utility methods.
import OS = require("os");
import Process = require("process");
import File = require("fs");
import Path = require("path");
import { EventEmitter } from "events";
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as Utils from "../Utils/Utils-Index";

export const ENTER_KEY: string = OS.EOL.substring(0, 1);
export const NEW_LINE: string = OS.EOL;

/** 
 * Recursively walks the object tree, returning false if 'leafTest' fails for any leaf property/value pair, 
 * or 'nonLeafTest' (if supplied) fails for any interior property, or if an array is enountered that has 
 * more than arraySizeLimit items (and 'walkArrayElements' is true). Otherwise, returns true.\
 * Note: When 'walkArrayElements' is false, an array will be treated like a leaf so the 'leafTest()' callback
 * will be invoked.
 */
export function walkObjectTree(o: object, 
    depth: number, 
    leafTest: (key: string, value: any, depth?: number) => boolean, 
    nonLeafTest?: (key: string, depth?: number) => boolean, 
    arraySizeLimit: number = -1,
    walkArrayElements: boolean = true): boolean
{
    for (let propName in o)
    {
        let value: any = o[propName];
        if ((typeof value === "object") && (value !== null))
        {
            if (Array.isArray(value) && !walkArrayElements)
            {
                // Treat the array like a leaf, so call leafTest()
                if (!leafTest(propName, value, depth))
                {
                    return (false);
                }
                continue; // Move on to the next property of 'o'
            }

            if (Array.isArray(o))
            {
                // We are walking the elements of an array
                // In this case, propName will be "0", "1", "2", etc. (ie. an index, not a property name) so we don't run nonLeafTest()
                if ((arraySizeLimit !== -1) && (o.length > arraySizeLimit))
                {
                    throw new Error(`Too many items (${o.length}) found in array; only expected ${arraySizeLimit} item(s)`);
                }
            }
            else
            {
                if (nonLeafTest && !nonLeafTest(propName, depth))
                {
                    return (false);
                }
            }
            let newDepth: number = depth + 1;
            if (!walkObjectTree(value, newDepth, leafTest, nonLeafTest, arraySizeLimit, walkArrayElements))
            {
                return (false);
            }
        }
        else
        {
            if (!leafTest(propName, value, depth))
            {
                return (false);
            }
        }
    }
    return (true);
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

/** Returns the available values of an specified enum type (eg. "1=Foo,2=Bar"). */
export function getEnumValues(enumType: EnumType): string
{
    let enumValues: string = "";
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
 * Returns the value of a named command-line argument (eg. "arg1=foo"), or returns 'defaultValue' if the named argument is not found.
 * If 'defaultValue' is not supplied, this indicates that the argument is required and the method will throw if it's missing from the command-line.
 * Note that most command-line args will belong to the app; Ambroisa uses ambrosiaConfig.json to specify start-up parameters.\
 * Note: 'argName' does not need to match on case.
 */
export function getCommandLineArg(argName: string, defaultValue?: string): string
{
    let args: string[] = Process.argv;
    let isRequired: boolean = (defaultValue === undefined);

    // 'args' will be: [NodeExe] [JSFile] [Arg1] [Arg2] ...
    // eg. "C:\Program Files\nodejs\node.exe C:\src\Git\Franklin\AmbrosiaJS\Ambrosia-Node\lib\App.js appArg1 appArg2 appArg3 ambrosiaConfigFile=testConfig.json"
    for (let i = 2; i < args.length; i++)
    {
        if (args[i].indexOf("=") !== -1)
        {
            let parts: string[] = args[i].split("=");
            let name: string = parts[0];
            let value: any = parts[1];

            if (equalIgnoringCase(name, argName))
            {
                if (isRequired && (value.length === 0))
                {
                    throw new Error(`No value was specified for the required Ambrosia command-line parameter '${argName}'`);
                }
                return (value);
            }
        }
    }
    if (isRequired)
    {
        throw new Error(`The Ambrosia command-line parameter '${argName}' is required`);
    }
    return (defaultValue);
}

/** Converts an array of bytes to a string to enable the byte array to be viewed. Set 'base' to 16 to view as hex. Will display at most 2048 bytes. */
export function makeDisplayBytes(bytes: Uint8Array, startIndex: number = 0, length: number = bytes.length - startIndex, base: number = 10)
{
    const MAX_DISPLAY_BYTES: number = 2048;
    let bytesAsString: string = "";
    let requestedEndIndex: number = Math.min(startIndex + length, bytes.length);
    let endIndex: number = requestedEndIndex;
    let outputTruncated: boolean = (endIndex - startIndex) > MAX_DISPLAY_BYTES;

    if (outputTruncated)
    {
        endIndex = startIndex + MAX_DISPLAY_BYTES - 1;
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
        let ommitted: string = ((requestedEndIndex - endIndex - 1) > 0) ? `...(${requestedEndIndex - endIndex - 1} bytes omitted)... ` : "";
        bytesAsString += `${ommitted}${bytes[requestedEndIndex - 1]}`;
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

/** Creates (or restarts) a one-time only timer. Once the timer has ticked, it will never be restarted. */
export function restartOnceOnlyTimer(timer: NodeJS.Timeout, timeoutInMs: number, onTick: () => void): NodeJS.Timeout
{
    if (timer && timer["__hasTicked__"] === true)
    {
        // The timer has already ticked
        return (timer);
    }

    if (!timer)
    {
        // Create the timer
        let newTimer: NodeJS.Timeout = setTimeout(() => 
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
    let executableName: string = isTimeTravelDebugging ? "Ambrosia" : "ImmortalCoordinator";

    if (ambrosiaToolsDir)
    {
        if (!File.existsSync(ambrosiaToolsDir))
        {
            throw new Error(`The specified 'ambrosiaToolsDir' (${ambrosiaToolsDir}) does not exist`);
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
    }

    if (isWindows())
    {
        fullyPathedExecutable = Path.join(ambrosiaToolsDir, `/${useNetCore ? "netcoreapp3.1" : "net461"}/${executableName}.${useNetCore ? "dll" : "exe"}`);
    }
    else
    {
        // Note: The pre-built Ambrosia-linux.tgz (https://github.com/microsoft/AMBROSIA/releases) doesn't have 
        //       either "netcoreapp3.1" or "net461" folders (even though Ambrosia-win-x64.zip does), but if they
        //       build locally then there will be. We assume they're using the pre-built binaries from Ambrosia-linux.tgz.
        fullyPathedExecutable = Path.join(ambrosiaToolsDir, executableName); // Eg. "/ambrosia/bin/ImmortalCoordinator"?
    }

    if (!File.existsSync(fullyPathedExecutable))
    {
        throw new Error(`The computed executable path (${fullyPathedExecutable}) does not exist`);
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
 * Starts accepting user input from the terminal. Calls the supplied callback whenever a character is entered at the terminal (stdin).
 * Note: Using this method requires this setting in launch.json: "console": "integratedTerminal"
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

        // Additionally, ensure that unhandled errors get logged to the Terminal window [these would normally be automatically written to the Debug Console window]. 
        // Note [if not using the source-map-support package (ie. lbOptions.enableTypeScriptStackTraces is false)]:
        // The error will only show the JS stack, not the TS stack (as the Debug Console does). Consequently, it's usually better to just check the
        // "Uncaught Exceptions" option under "Breakpoints" in VSCode.
        Process.on("uncaughtException", function handleUncaughtException(error: Error) 
        {
            Utils.logWithColor(Utils.ConsoleForegroundColors.Red, error.stack ?? error.toString(), "Uncaught Exception");
            Process.exit();
        });

        Process.on("unhandledRejection", function handleUncaughtRejection<T>(error: Error | any, promise: Promise<T>) 
        {
            // Note: 'promise' can only be inspected when using the debugger
            Utils.logWithColor(Utils.ConsoleForegroundColors.Red, (error instanceof Error) ? error.stack ?? error.toString() : error.toString(), "Uncaught Promise Rejection");
            Process.exit();
        });
    }
    else
    {
        throw new Error("No text terminal is attached to Node; if using VSCode, try setting \"console\": \"integratedTerminal\" in launch.json");
    }
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
 * The async method must also have a 'catch' block to enable it to be called synchronously (ie. without being awaited).
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
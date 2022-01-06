// Module for output logging (to file and console).
import Stream = require("stream");
import File = require("fs");
import Path = require("path");
import OS = require("os");
import Process = require("process");
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as Configuration from "../Configuration";
import * as StringEncoding from "../StringEncoding";
import * as Utils from "../Utils/Utils-Index";

/** The detail level of a logged message. */
export enum LoggingLevel
{
    /** The message will always be logged, regardless of the configured 'outputLoggingLevel'. */
    Minimal = 0,
    /** The message will only be logged if the configured 'outputLoggingLevel' is 'Verbose' or 'Debug'. */
    Verbose = 1,
    /** The message will only be logged if the configured 'outputLoggingLevel' is 'Debug'. */
    Debug = 2
}

class LoggingOptions
{
    /** The level of detail to include in the log. */
    loggingLevel: LoggingLevel = LoggingLevel.Minimal;
    
    /** Where log output should be written. */
    logDestination: Configuration.OutputLogDestination = Configuration.OutputLogDestination.Console;

    /** The folder where the output log file will be written (if logDestination is 'File' or 'ConsoleAndFile'). */
    logFolder: string = "./outputLogs";

    /** The set of trace flags specified in the lbOptions.traceFlags configuration setting. */
    traceFlags: Set<TraceFlag> = new Set<TraceFlag>();

    /** Whether to allow the use of color when logging to the console. */
    allowColor: boolean = true;

    /** Returns true if logging is enabled for the specified destination. */
    canLogTo(destination: Configuration.OutputLogDestination): boolean
    {
        return ((this.logDestination & destination) === destination);
    }
}

let _loggingOptions: LoggingOptions;
let _logFileHandle: number = -1;
let _logFileName: string = "";
let _lastMessageLogged: string | null = null;
let _allMessageSuppressionRegExp: RegExp | null = null;
let _consoleMessageSuppressionRegExp: RegExp | null = null;

/** Initializes (from the loaded config file) the options that control output logging. */
function initializeLoggingOptions()
{
    let lbOptions: Configuration.LanguageBindingOptions = Configuration.loadedConfig().lbOptions;
    
    _loggingOptions = new LoggingOptions();
    _loggingOptions.loggingLevel = lbOptions.outputLoggingLevel;
    _loggingOptions.logDestination = lbOptions.outputLogDestination;
    _loggingOptions.logFolder = lbOptions.outputLogFolder;
    _loggingOptions.allowColor = lbOptions.outputLogAllowColor;

    if (lbOptions.traceFlags.length > 0)
    {
        // Note: The lbOptions.traceFlags value has already been verified and formatted        
        const traceFlagNames: string[] = lbOptions.traceFlags.split(";");
        for (let i = 0; i < traceFlagNames.length; i++)
        {
            _loggingOptions.traceFlags.add(TraceFlag[traceFlagNames[i] as keyof typeof TraceFlag]);
        }
    }

    openOutputLog();
}

/** Colors that the foreground text can be set to when writing to the console. */
// See https://stackoverflow.com/questions/9781218/how-to-change-node-jss-console-font-color 
export enum ConsoleForegroundColors
{
    // Note: These are the "bright" values (see https://en.wikipedia.org/wiki/ANSI_escape_code#Colors); the "dim" values are -60 (eg. dim red = "\x1b[31m"),
    //       but the host console (eg. PowerShell) and "theme" (if applicable) determines which actual RGB color to render.
    Red = "\x1b[91m",
    Green = "\x1b[92m",
    Yellow = "\x1b[93m",
    Blue = "\x1b[94m",
    Magenta = "\x1b[95m",
    Cyan = "\x1b[96m",
    White = "\x1b[97m",
    /** Used to reset the color back to the prior color. */
    Reset = "\x1b[0m"
}

/** Returns the text of the last message logged. Primarily used for testing. */
export function getLastMessageLogged(): string | null
{
    return (_lastMessageLogged);
}

/** 
 * [Internal] Sets a regular expression used to (temporarily) suppress logging of messages that match it.
 * If 'regExp' is not supplied, the suppression is cleared.
 * 
 * Errors will not be suppressed, although if 'suppressConsoleOnly' is true then errors _will_ be suppressed
 * in the console (but not in the log file) if the definition of 'regExp' includes them. Will throw if 'suppressConsoleOnly'
 * is true but the 'outputLogDestination' configuration setting is not 'ConsoleAndFile'.
 * 
 * This method is for **internal use only**.
 */
export function suppressLoggingOf(regExp?: RegExp, suppressConsoleOnly: boolean = false): void
{
    if (!regExp)
    {
        _consoleMessageSuppressionRegExp = _allMessageSuppressionRegExp = null;
    }
    else
    {
        if (suppressConsoleOnly)
        {
            if (_loggingOptions.logDestination !== Configuration.OutputLogDestination.ConsoleAndFile)
            {
                throw new Error(`Console messages can only be suppressed when 'outputLogDestination' in ${Configuration.loadedConfigFileName()} is 'ConsoleAndFile'`);
            }
            _consoleMessageSuppressionRegExp = regExp;
        }
        else
        {
            _allMessageSuppressionRegExp = regExp;
        }
    }
}

/** A object with the core properties of an Error object. */
type ErrorLike = Pick<Error, "name" | "message" | "stack">;

/** 
 * If the supplied 'error' is an Error, simply returns it.\
 * If the supplied 'error' is Error-like, creates a new Error instance from it.\
 * Otherwise, returns an Error created from whatever 'error' is.
 */
export function makeError(error: Error | unknown): Error
{
    /** [Local function] Returns true if the supplied 'error' is an object with the same "shape" as an Error. */
    function isErrorLike(error: unknown): error is ErrorLike
    {
        if (error instanceof Object)
        {
            if (("name" in error) && ("message" in error) && ("stack" in error))
            {
                if ((typeof error["name"] === "string") && (typeof error["message"] === "string") && (typeof error["stack"] === "string"))
                {
                    return (true);
                }
            }
        }
        return (false);
    }

    if (error instanceof Error) // This will be true for all derived error types (eg. ReferenceError)
    {
        return (error);
    }
    else
    {
        // Note: This path should only ever happen when we're catching an "error" thrown by user-code, since our code only ever throws Error()
        const newError: Error = new Error();

        if (isErrorLike(error))
        {
            newError.name = error.name;
            newError.message = error.message;
            newError.stack = error.stack;
        }
        else
        {
            try
            {
                newError.message = (typeof error === "string") ? error : ((typeof error === "object") && (error !== null) ? error.toString() : new String(error).valueOf());
                const frames: string[] = newError.stack?.split("\n") || [];
                if (frames.length > 1)
                {
                    // Remove the "makeError()" frame
                    if (frames[1].indexOf(".makeError (") !== -1)
                    {
                        frames.splice(1, 1);
                    }
                    newError.stack = frames.join("\n"); 
                }
                else
                {
                    newError.stack = undefined;
                }
            }
            catch (innerError: unknown)
            {
                newError.message = `[Error could not be produced (reason: ${(innerError as Error).message})]`;
                newError.stack = new Error().stack; // We let the stack include the "makeError()" frame" in this case
            }
        }
        return (newError)
    }
}

/**
 * Returns the current time [or the supplied dateTime] in the format\
 * 'YYYY/MM/dd hh:mm:ss.fff' (without the quotes).
 */
export function getTime(dateTime?: number | Date): string
{
    let now = new Date(dateTime ? dateTime : Date.now());
    let date = now.getFullYear() + "/" + ("0" + (now.getMonth() + 1)).slice(-2) + "/" + ("0" + now.getDate()).slice(-2);
    let time = ("0" + now.getHours()).slice(-2) + ":" + ("0" + now.getMinutes()).slice(-2) + ":" + ("0" + now.getSeconds()).slice(-2) + "." + ("00" + now.getMilliseconds()).slice(-3);
    return (date + " " + time);
}

function makeLogLine(time: string, message: string, prefix?: string): string
{
    prefix = prefix ? (prefix + (prefix.endsWith("]") ? " " : ": ")) : "";
    _lastMessageLogged = `${prefix}${message}`;
    let line: string = `${time}: ${_lastMessageLogged}`;
    return (line);
}

function isLoggable(message: string, msgLevel: LoggingLevel, destination: Configuration.OutputLogDestination): boolean
{
    // Suppressed console messages are never loggable
    // Note: When suppressing console messages, we also allow suppression of errors
    if ((destination === Configuration.OutputLogDestination.Console) && _consoleMessageSuppressionRegExp && _consoleMessageSuppressionRegExp.test(message))
    {
        return (false);
    }
    // Errors are always loggable
    if (/Error:/i.test(message))
    {
        return (true);
    }
    // Suppressed messages are never loggable
    if (_allMessageSuppressionRegExp && _allMessageSuppressionRegExp.test(message))
    {
        return (false);
    }
    return (_loggingOptions.canLogTo(destination) && (msgLevel <= _loggingOptions.loggingLevel));
}

function logToConsole(time: string, message: string, prefix?: string, color?: ConsoleForegroundColors, msgLevel: LoggingLevel = LoggingLevel.Verbose): void
{
    if (!isLoggable(message, msgLevel, Configuration.OutputLogDestination.Console))
    {
        return;
    }

    const line: string = makeLogLine(time, message, prefix);

    if (_loggingOptions.allowColor)
    {
        if (!color && /Warning:/i.test(line))
        {
            color = ConsoleForegroundColors.Yellow;
        }
        if (!color && /Error:/i.test(line))
        {
            color = ConsoleForegroundColors.Red;
        }
    }
    else
    {
        color = undefined;
    }

    console.log(color ? colorize(line, color) : line);
}

// Note: Logging to a file without also logging to the console is the most performant way to do logging.
function logToFile(time: string, message: string, prefix?: string, msgLevel: LoggingLevel = LoggingLevel.Verbose): void
{
    if ((_logFileHandle === -1) || !isLoggable(message, msgLevel, Configuration.OutputLogDestination.File))
    {
        return;
    }

    const line: string = makeLogLine(time, message, prefix);
    File.writeSync(_logFileHandle, line + Utils.NEW_LINE);
}

/** Opens (creates) the output log file (if needed). */
function openOutputLog(): void
{
    if ((_logFileHandle === -1) && _loggingOptions.canLogTo(Configuration.OutputLogDestination.File))
    {
        if (!File.existsSync(_loggingOptions.logFolder))
        {
            File.mkdirSync(_loggingOptions.logFolder);
        }

        const fileName: string = `traceLog_${Utils.getTime().replace(/\//g, "").replace(" ", "_").replace(/:/g, "").slice(0, -4)}.txt`;
        _logFileName = Path.resolve(Path.join(_loggingOptions.logFolder, fileName));

        // Note: This will overwrite the file if it already exists [see https://nodejs.org/api/fs.html#fs_file_system_flags]
        _logFileHandle = File.openSync(_logFileName, "w");
        Utils.log(`Logging output to ${_logFileName}`);
    }
}

/** [Internal] Closes the output log file. */
export function closeOutputLog(): void
{
    try
    {
        if (_logFileHandle !== -1)
        {
            File.closeSync(_logFileHandle);
            _logFileHandle = -1;
            _logFileName = "";
        }
    }
    catch (error: unknown)
    {
        Utils.log(`Error: Unable to close output log (reason: ${Utils.makeError(error).message})`);
    }
}

/** [Internal] Returns the pathed output log file name. */
export function getOutputLog(): string
{
    return (_logFileName);
}

/** 
 * [Internal] Returns true if logging messages of the specified level is allowed.\
 * Note: For non-error messages in high-frequency code paths, it is more efficient to call this first to determine if log() should be called.
 */
export function canLog(msgLevel: LoggingLevel): boolean
{
    if (!_loggingOptions)
    {
        initializeLoggingOptions();
    }
    return (msgLevel <= _loggingOptions.loggingLevel);
}

/** [Internal] Returns true if logging is enabled for the console. */
export function canLogToConsole(): boolean
{
    if (!_loggingOptions)
    {
        initializeLoggingOptions();
    }
    return (_loggingOptions.canLogTo(Configuration.OutputLogDestination.Console));
}

/**
 * Logs the specified message (with the [optional] specified prefix), along with a timestamp, to the console and/or output log file.
 * @param {string | Error} message Message (or Error) to log.
 * @param {string} [prefix] [Optional] Prefix for the message. Can be specified as null.
 * @param {LoggingLevel} [level] [Optional] The level that logging must be set to (in ambrosiaConfig.json) for the message to be logged.
 */
export function log(message: string | Error, prefix: string | null = null, level: LoggingLevel = LoggingLevel.Verbose): void
{
    if (!_loggingOptions)
    {
        initializeLoggingOptions();
    }

    let errorMessage: string = (message instanceof Error) ? (message.stack || message.toString()) : message;
    let time: string = getTime();
    logToConsole(time, errorMessage, prefix || undefined, undefined, level);
    logToFile(time, errorMessage, prefix || undefined, level);
}

/** Output logging trace flags. See traceLog(). */
export enum TraceFlag
{
    /** Logs trace messages related to log page back-pressure (see bug #194). */
    LogPageInterruption = 1,
    /** Logs trace messages related to memory usage. */
    MemoryUsage = 2
}

/**
 * Logs the specified message, along with a timestamp, to the console and/or output log file, but **only** if
 * the specified traceFlag is included (enabled) in the lbOptions.traceFlags configuration setting.\
 * If the requested trace flag is enabled, the message will **always** be logged, regardless of the configured 'outputLoggingLevel'.
 */
export function traceLog(traceFlag: TraceFlag, message: string): void
{
    if (!_loggingOptions)
    {
        initializeLoggingOptions();
    }

    if (_loggingOptions.traceFlags.has(traceFlag))
    {
        log(message, `TRACE (${TraceFlag[traceFlag]})`, LoggingLevel.Minimal);
    }
}

/** Returns true if the specified trace flag is enabled (configured). */
export function isTraceFlagEnabled(traceFlag: TraceFlag): boolean
{
    if (!_loggingOptions)
    {
        initializeLoggingOptions();
    }
    return (_loggingOptions.traceFlags.has(traceFlag));
}

/** 
 * A fail-safe version of log(). Falls back to console.log() if Utils.log() fails.\
 * Typically only used before Ambrosia.initialize() / initializeAsync() is called.
 */
export function tryLog(message: string | Error, prefix?: string, level: LoggingLevel = LoggingLevel.Verbose): void
{
    try
    {
        log(message, prefix, level);
    }
    catch 
    {
        let errorMessage: string = (message instanceof Error) ? (message.stack || message.toString()) : message;
        let line: string = makeLogLine(getTime(), errorMessage, prefix);
        console.log(line);
    }
}

/**
 * Logs the specified message (with the [optional] specified prefix) in the specified color, along with a timestamp, to the console and/or output log file.
 * @param {ConsoleForegroundColors} color Foreground color for the message. Only applies to the console, not the output log file.
 * @param {string} message Message to log.
 * @param {string} [prefix] [Optional] Prefix for the message.
 * @param {LoggingLevel} [level] [Optional] The level that logging must be set to (in ambrosiaConfig.json) for the message to be logged.
 */
export function logWithColor(color: ConsoleForegroundColors, message: string | Error, prefix?: string, level: LoggingLevel = LoggingLevel.Verbose): void
{
    if (!_loggingOptions)
    {
        initializeLoggingOptions();
    }

    let errorMessage: string = (message instanceof Error) ? (message.stack || message.toString()) : message;
    let time: string = getTime();
    logToConsole(time, errorMessage, prefix, color, level);
    logToFile(time, errorMessage, prefix, level);
}

/** Logs a "header" to make it easier to find a section of [subsequent] output. */
export function logHeader(title: string, char: string = "-")
{
    let separatorLine: string = char.repeat(title.length);
    Utils.log(separatorLine);
    Utils.log(title);
    Utils.log(separatorLine);
}

/** Logs memory usage statistics. Only logs if the 'MemoryUsage' trace flag is set, and will log regardless of the configured 'outputLoggingLevel'. */
export function logMemoryUsage(): void
{
    if (!isTraceFlagEnabled(TraceFlag.MemoryUsage))
    {
        return;
    }

    const ONE_MB: number = 1024 * 1024;
    const memStats: NodeJS.MemoryUsage = Process.memoryUsage();
    const computerMemTotal: number = OS.totalmem();
    const computerMemUsed: number = computerMemTotal - OS.freemem();
    const percentComputerMemUsed: string = ((computerMemUsed / computerMemTotal) * 100).toFixed(1);
    const percentHeapUsed: string = ((memStats.heapUsed / memStats.heapTotal) * 100).toFixed(1);
    Utils.log(`Memory: RSS = ${toMB(memStats.rss)}, Heap (Total / Used) = ${toMB(memStats.heapTotal)} / ${toMB(memStats.heapUsed)} (${percentHeapUsed}%), Computer (Total / Used) = ${toMB(computerMemTotal)} / ${toMB(computerMemUsed)} (${percentComputerMemUsed}%)`, null, LoggingLevel.Minimal);

    function toMB(valueInBytes: number): string
    {
        return (`${(valueInBytes / ONE_MB).toFixed(2)} MB`);
    }
}

/** Formats the message so that, when written to the console, it will have the specified color. */
export function colorize(message: string, color: ConsoleForegroundColors): string
{
    return (color ? `${color}${message}${ConsoleForegroundColors.Reset}` : message);
}

/** Returns the file/line/char location (eg. "src\Meta.ts:3142:19") for the top-most (last) stack frame of the specified error. May return null. */
export function getErrorOrigin(error: Error): string | null
{
    let origin: string | null = null;

    if (error.stack?.indexOf("\n") !== -1)
    {
        const lines: string[] = error.stack?.split("\n") ?? [];
        if (lines.length >= 2)
        {
            // See: https://v8.dev/docs/stack-trace-api
            // lines[1] style #1: '    at emitTypeScriptFileEx (C:\src\Git\AMBROSIA\Clients\AmbrosiaJS\Ambrosia-Node\src\Meta.ts:1583:23)'
            // lines[1] style #2: '    at C:\src\Git\AMBROSIA\Clients\AmbrosiaJS\Ambrosia-Node\src\Meta.ts:1583:23'
            const topFrame: string = lines[1].trim();
            const regExps: RegExp[] = [ RegExp(/^at[ ]{1}.+[ ]{1}\((.+)\)$/), RegExp(/^at[ ]{1}([^ ]+)$/) ];

            origin = topFrame; // In case the RegExp's fail
            for (let i = 0; i < regExps.length; i++)
            {
                const execResult: RegExpExecArray | null = regExps[i].exec(topFrame);
                if (execResult && (execResult.length > 1))
                {
                    const location: string = execResult[1];
                    const extension: string = Path.extname(Path.basename(location).split(":")[0]);
                    if (Utils.equalIgnoringCase(extension, ".ts") || Utils.equalIgnoringCase(extension, ".js"))
                    {
                        origin = Path.relative(process.cwd(), location);
                        break;
                    }
                }
            }
        }
    }
    return (origin);
}

/** 
 * A transform stream used to format StandardOutput (stdout) and/or StandardError (stderr).
 * Each line in the [piped-in] output will be transformed to include a timestamp and, optionally, a prefix (source) and color.
 * Further, the stream can be watched for certain token values (using tokenWatchList) that - if found - will cause a 'tokenFound'
 * event to be emitted [handled with 'tokenFoundHandler(token: string, line: string)'].\
 * Note: Tokens are case-sensitive.
 */
export class StandardOutputFormatter extends Stream.Transform
{
    private _source: string = ""; // Eg. "[IC]""
    private _color: ConsoleForegroundColors; // Initialized in constructor
    private _errorColor: ConsoleForegroundColors; // Initialized in constructor
    private _tokenWatchList: RegExp[] = [];
    // Eg: Parse "ImmortalCoordinator.exe", "Information", "0" and "Ready ..." from "ImmortalCoordinator.exe Information: 0 : Ready ..."
    private readonly _lineParser: RegExp = RegExp(/^(\S+)?[\s]*([\S]+)[\s]*[:][\s]*(\d+?)[\s]*[:][\s]*(.*$)/); 

    constructor(source: string = "", color: ConsoleForegroundColors, errorColor: ConsoleForegroundColors = ConsoleForegroundColors.Red, tokenWatchList: RegExp[] = [], options?: Stream.TransformOptions)
    {
        super(options);
        this._source = source;
        this._color = color;
        this._errorColor = errorColor;
        this._tokenWatchList = tokenWatchList;
    }

    /** Parses a single line of IC output and returns a more concise version. */
    private parseLine(line: string): string
    {
        let modifiedLine: string = line;
        if (line.length > 0)
        {
            let results: RegExpExecArray | null = this._lineParser.exec(line);
            if (results && (results.length === 5))
            {
                let binaryName: string = results[1]; // Eg. "ImmortalCoordinator.exe"
                let messageType: string = results[2]; // "Information", "Warning", or "Error"
                let messageID: number = parseInt(results[3]); // Unused by the IC (ie. always 0)
                let message: string = results[4]; // Message

                modifiedLine = (messageType !== "Information") ? `${messageType}: ${message}` : message;
            }
        }
        return (modifiedLine);
    }

    /** Returns true if the specified line is an error or an exception. */
    private isLineErrorOrException(line: string): boolean
    {
        line = line.trimLeft();
        const result: boolean = /Exception:/.test(line) || /^Error:\s+/.test(line);
        // Note: We don't include stack trace lines because - due to chunking - lines (especially for large stack traces) can
        //       end up being split over 2 chunks, and this "line break" also breaks the regex tests for a stack trace line.
        // /^\s+at\s+/.test(line) ||
        // /^\s*--- End of/.test(line) || 
        return (result);
    }

    // private _testExceptionCase: boolean = true; // Used for testing the handling of exceptions (with stacks) from the IC

    _transform(chunk: Buffer, encoding: string, callback: (error?: Error) => void): void
    {
        const lineSplitRegEx: RegExp = new RegExp(`${OS.EOL}|\n`); // Sometime CRA messages include just a '\n' character
        // Note: A chunk may end in the middle of a line (eg. for large stack traces), which will result in us "breaking" the line across 2 logged lines
        let output: string = chunk.toString();
        let unprocessedLines: string[] = output.split(lineSplitRegEx);
        // We use the same timestamp for ALL lines in a given 'chunk' (which is the most accurate thing to
        // do, but it also provides us with visibility into the chunk size and "line breaks" across chunks)
        const time: string = getTime(); 

        // if (this._testExceptionCase)
        // {
        //     this._testExceptionCase = false;
        //     try
        //     {
        //         let foo: string[] = [];
        //         let bar: number = foo.length;
        //     }
        //     catch (error: unknown)
        //     {
        //         output = Utils.makeError(error).stack || "";
        //         unprocessedLines = output.split("\n");
        //         unprocessedLines[0] = unprocessedLines[0].replace("Error:", "Exception:");
        //         unprocessedLines.push("ImmortalCoordinator.exe Error: 0 : Error doing something!!");
        //     }
        // }

        for (let i = 0; i < unprocessedLines.length; i++)
        {
            const line: string = this.parseLine(unprocessedLines[i]);
            if (line.length > 0)
            {
                // We output the modified line immediately, even if a "tokenFound" handler ends up also writing the line to stdout.
                // Since we can't know what the handler does, we output it to maintain the correct sequence of the output (even if
                // it may result in the line being outputted twice).
                if (_loggingOptions.canLogTo(Configuration.OutputLogDestination.Console))
                {
                    // Note: Stack trace lines will use the standard _color, not _errorColor [see comment in isLineErrorOrException()]
                    const color: ConsoleForegroundColors = this.isLineErrorOrException(line) ? this._errorColor : this._color;
                    const modifiedOutput: string = colorize(`${time}: ${this._source}${this._source ? " " : ""}${line}${OS.EOL}`, color);
                    this.push(StringEncoding.toUTF8Bytes(modifiedOutput)); // If this returns false, it will just buffer
                }

                // Since the StandardOutputFormatter output doesn't observe [by design] the configured 'outputLoggingLevel',
                // we [effectively] do the same if logging to file by specifying the loggingLevel as 'Minimal'
                logToFile(time, line, this._source, LoggingLevel.Minimal);

                // If needed, fire "tokenFound" handler
                if (this.listenerCount("tokenFound") > 0)
                {
                    for (let i = 0; i < this._tokenWatchList.length; i++)
                    {
                        if (this._tokenWatchList[i].test(line))
                        {
                            // Note: It's not reliable to try to re-combine related lines (eg. the stack trace lines for an exception) because the
                            //       chunk that unprocessedLines came from may not include all the related lines (eg. the entire stack trace).
                            this.emit("tokenFound", this._tokenWatchList[i].toString(), line); // Note: emit() synchronously calls each of the listeners
                            break; // Stop on the first token we find
                        }
                    }
                }
            }
        }
        callback();
    }
}

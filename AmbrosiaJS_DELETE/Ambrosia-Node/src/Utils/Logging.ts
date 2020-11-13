// Module for output logging (to file and console).
import Stream = require("stream");
import File = require("fs");
import Path = require("path");
import OS = require("os");
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as Configuration from "../Configuration";
import * as StringEncoding from "../StringEncoding";
import * as Utils from "../Utils/Utils-Index";

/** The detail level of a logged message. */
export enum LoggingLevel
{
    /** The message will always be logged, regardless of the configured 'outputLoggingLevel'. */
    Minimal = 0,
    /** The message will only be logged if the configured 'outputLoggingLevel' is 'Normal' or 'Verbose'. */
    Normal = 1,
    /** The message will only be logged if the configured 'outputLoggingLevel' is 'Verbose'. */
    Verbose = 2
}

class LoggingOptions
{
    /** The level of detail to include in the log. */
    loggingLevel: LoggingLevel = LoggingLevel.Normal;
    
    /** Where log output should be written. */
    logDestination: Configuration.OutputLogDestination = Configuration.OutputLogDestination.Console;

    /** The folder where the output log file will be written (if logDestination is 'File' or 'ConsoleAndFile'). */
    logFolder: string = "./outputLogs";

    /** Returns true if logging is enabled for the specified destination. */
    canLogTo(destination: Configuration.OutputLogDestination): boolean
    {
        return ((this.logDestination & destination) === destination);
    }
}

let _loggingOptions: LoggingOptions = null;
let _logFileHandle: number = -1;

/** Initializes (from the loaded config file) the options that control output logging. */
function initializeLoggingOptions()
{
    if (Configuration.loadedConfig() === null)
    {
        throw new Error("Ambrosia.initializeAsync() has not been called");
    }

    let lbOptions: Configuration.LanguageBindingOptions = Configuration.loadedConfig().lbOptions;
    
    _loggingOptions = new LoggingOptions();
    _loggingOptions.loggingLevel = lbOptions.outputLoggingLevel;
    _loggingOptions.logDestination = lbOptions.outputLogDestination;
    _loggingOptions.logFolder = lbOptions.outputLogFolder;
}

/** Colors that the foreground text can be set to when writing to the console. */
// See https://stackoverflow.com/questions/9781218/how-to-change-node-jss-console-font-color 
export enum ConsoleForegroundColors
{
    // Note: These are the "bright" values (see https://en.wikipedia.org/wiki/ANSI_escape_code#Colors); the "dim" values are -60 (eg. dim red = "\x1b[31m")
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

/**
 * Returns the current time [or the supplied dateTime] in 'YYYY/MM/dd hh:mm:ss.fff' format.
 * @returns {string} Result.
 */
export function getTime(dateTime?: number): string
{
    let now = new Date(dateTime ? dateTime : Date.now());
    let date = now.getFullYear() + "/" + ("0" + (now.getMonth() + 1)).slice(-2) + "/" + ("0" + now.getDate()).slice(-2);
    let time = ("0" + now.getHours()).slice(-2) + ":" + ("0" + now.getMinutes()).slice(-2) + ":" + ("0" + now.getSeconds()).slice(-2) + "." + ("00" + now.getMilliseconds()).slice(-3);
    return (date + " " + time);
}

function makeLogLine(time: string, message: string, prefix?: string): string
{
    prefix = prefix ? (prefix + (prefix.endsWith("]") ? " " : ": ")) : "";
    let line: string = `${time}: ${prefix}${message}`;
    return (line);
}

function isLoggable(message: string, msgLevel: LoggingLevel, destination: Configuration.OutputLogDestination): boolean
{
    // Errors are always loggable
    if (message.indexOf("Error:") !== -1)
    {
        return (true);
    }
    return (_loggingOptions.canLogTo(destination) && (msgLevel <= _loggingOptions.loggingLevel));
}

function logToConsole(time: string, message: string, prefix?: string, color?: ConsoleForegroundColors, msgLevel: LoggingLevel = LoggingLevel.Normal): void
{
    if (!isLoggable(message, msgLevel, Configuration.OutputLogDestination.Console))
    {
        return;
    }

    if (!color && message.startsWith("Warning:"))
    {
        color = ConsoleForegroundColors.Yellow;
    }
    if (!color && message.startsWith("Error:"))
    {
        color = ConsoleForegroundColors.Red;
    }

    let line: string = makeLogLine(time, message, prefix);
    console.log(color ? colorize(line, color) : line);
}

// Note: Logging to a file without also logging to the console is the most performant way to do logging.
function logToFile(time: string, message: string, prefix?: string, msgLevel: LoggingLevel = LoggingLevel.Normal): void
{
    let logFileCreated: boolean = false;
    let logFile = null;

    if (!isLoggable(message, msgLevel, Configuration.OutputLogDestination.File))
    {
        return;
    }

    if (_logFileHandle === -1)
    {
        if (!File.existsSync(_loggingOptions.logFolder))
        {
            File.mkdirSync(_loggingOptions.logFolder);
        }

        let fileName: string = `traceLog_${Utils.getTime().replace(/\//g, "").replace(" ", "_").replace(/:/g, "").slice(0, -4)}.txt`;
        logFile = Path.resolve(Path.join(_loggingOptions.logFolder, fileName));

        // Note: This will overwrite the file if it already exists [see https://nodejs.org/api/fs.html#fs_file_system_flags]
        _logFileHandle = File.openSync(logFile, "w");
        logFileCreated = true;
    }

    let line: string = makeLogLine(time, message, prefix);
    File.writeSync(_logFileHandle, line + Utils.NEW_LINE)

    if (logFileCreated)
    {
        Utils.log(`Logging output to ${logFile}`);
    }
}

/** Closes the output log file. */
export function closeLog(): void
{
    if (_logFileHandle !== -1)
    {
        File.closeSync(_logFileHandle);
        _logFileHandle = -1;
    }
}

/** 
 * Returns true if logging messages of the specified level is allowed.\
 * Note: For non-error messages in high-frequency code paths, it is more efficient to call this first to determine if log() should be called.
 */
export function canLog(msgLevel: LoggingLevel)
{
    if (_loggingOptions === null)
    {
        initializeLoggingOptions();
    }
    return (msgLevel <= _loggingOptions.loggingLevel);
}

/**
 * Logs the specified message (with the [optional] specified prefix), along with a timestamp, to the console and/or output log file.
 * @param {string | Error} message Message (or Error) to log.
 * @param {string} [prefix] [Optional] Prefix for the message.
 * @param {LoggingLevel} [level] [Optional] The level that logging must be set to for the message to be logged.
 */
export function log(message: string | Error, prefix: string = null, level: LoggingLevel = LoggingLevel.Normal): void
{
    if (_loggingOptions === null)
    {
        initializeLoggingOptions();
    }

    let errorMessage: string = (message instanceof Error) ? (message.stack || message.toString()) : message;
    let time: string = getTime();
    logToConsole(time, errorMessage, prefix, undefined, level);
    logToFile(time, errorMessage, prefix, level);
}

/** 
 * A fail-safe version of log(). Falls back to console.log() if Utils.log() fails.\
 * Typically only used before Ambrosia.initializeAsync() is called.
 */
export function tryLog(message: string | Error, prefix: string = null, level: LoggingLevel = LoggingLevel.Normal): void
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
 * @param {LoggingLevel} [level] [Optional] The level that logging must be set to for the message to be logged.
 */
export function logWithColor(color: ConsoleForegroundColors, message: string | Error, prefix?: string, level: LoggingLevel = LoggingLevel.Normal): void
{
    if (_loggingOptions === null)
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

/** Formats the message so that, when written to the console, it will have the specified color. */
export function colorize(message: string, color: ConsoleForegroundColors): string
{
    return (color ? `${color}${message}${ConsoleForegroundColors.Reset}` : message);
}

/** 
 * A transform stream used to format StandardOutput (stdout) and/or StandardError (stderr).
 * Each line in the [piped-in] output will be transformed to include a timestamp and, optionally, a prefix (source) and color.
 * Further, the stream can be watched for certain token values (using tokenWatchList) that - if found - will cause a 'tokenFound'
 * event to be emitted [handled with 'tokenFoundHandler(token: string, line: string, multiLine: string)'].\
 * Note: Tokens are case-sensitive.
 */
export class StandardOutputFormatter extends Stream.Transform
{
    private _source: string = "";
    private _color: ConsoleForegroundColors = null;
    private _tokenWatchList: RegExp[] = [];
    private _unprocessedLines: string[] = null;
    // Eg: Parse "ImmortalCoordinator.exe", "Information", "0" and "Ready ..." from "ImmortalCoordinator.exe Information: 0 : Ready ..."
    private _lineParser: RegExp = RegExp(/^(\S+)?[\s]*([\S]+)[\s]*[:][\s]*(\d+?)[\s]*[:][\s]*(.*$)/); 

    constructor(source: string = "", color: ConsoleForegroundColors = null, tokenWatchList: RegExp[] = [], options?: Stream.TransformOptions)
    {
        super(options);
        this._source = source;
        this._color = color;
        this._tokenWatchList = tokenWatchList;
    }

    /** Called by a 'tokenFound' event handler if it outputs the 'multiLine' parameter of the event. */
    clearUnprocessedLines()
    {
        this._unprocessedLines = [];
    }

    // _testMultLine: boolean = true; // Used for testing the 'multiLine' parameter of "tokenHandler" event data

    _transform(chunk: Buffer, encoding: string, callback: (error?: Error) => void): void
    {
        let output: string = chunk.toString();
        this._unprocessedLines = output.split(OS.EOL);

        // if (this._testMultLine)
        // {
        //     this._testMultLine = false;
        //     try
        //     {
        //         let foo: string[] = null;
        //         let bar: number = foo.length;
        //     }
        //     catch (error)
        //     {
        //         output = error.stack.toString();
        //         this._unprocessedLines = output.split("\n");
        //         this._unprocessedLines[0] = this._unprocessedLines[0].replace("Error:", "Exception:");
        //     }
        // }

        while (this._unprocessedLines.length > 0)
        {
            let line: string = this._unprocessedLines[0].trim();

            if (this._lineParser && (line.length > 0))
            {
                 let results: RegExpExecArray = this._lineParser.exec(line);
                 if (results && (results.length === 5))
                 {
                     let binaryName: string = results[1]; // Eg. "ImmortalCoordinator.exe"
                     let messageType: string = results[2]; // "Information", "Warning", or "Error"
                     let messageID: number = parseInt(results[3]); // Unused by the IC (ie. always 0)
                     line = results[4]; // Message

                     if ((messageType !== "Information") && (line.indexOf(messageType) !== 0))
                     {
                        line = `${messageType}: ${line}`;
                     }
                 }
            }

            if (line.length > 0)
            {
                // We output the modified line immediately, even if a "tokenFound" handler ends up also writing the line to stdout.
                // Since we can't know what the handler does, we output it to maintain the correct sequence of the output (even if
                // it may result in the line being outputted twice).
                let time: string = getTime();
                if (_loggingOptions.canLogTo(Configuration.OutputLogDestination.Console))
                {
                    let modifiedOutput: string = colorize(`${time}: ${this._source}${this._source ? " " : ""}${line}${OS.EOL}`, this._color);
                    this.push(StringEncoding.toUTF8Bytes(modifiedOutput)); // If this returns false, it will just buffer
                }

                // Since the StandardOutputFormatter output doesn't observe the configured 'outputLoggingLevel', 
                // we [effectively] do the same if logging to file by specifying the loggingLevel as 'Minimal'
                logToFile(time, line, this._source, LoggingLevel.Minimal);

                if (this.listenerCount("tokenFound") > 0)
                {
                    for (let i = 0; i < this._tokenWatchList.length; i++)
                    {
                        if (this._tokenWatchList[i].test(line))
                        {
                            // This idea behind 'multiLine' is that an error/exception message often include a stack trace
                            // and so spans multiple lines. To give a "tokenFound" handler access to all these lines, we
                            // concatenate all the remaining lines together and provide them as the 'multiLine' event parameter.
                            // This way, if the handler stops the process it will be able to report the full error message before
                            // the process exits. Further, to prevent the remaining lines from being outputted twice [by both the
                            // handler and us], the handler can (and should, if it outputs multiLine) call clearUnprocessedLines().
                            // Note: It's possible that non-error lines will be inadvertently included at the end of 'multiLine'.
                            let multiLine: string = this._unprocessedLines.join(OS.EOL).trimRight();
                            this.emit("tokenFound", this._tokenWatchList[i].toString(), line, multiLine); // Note: emit() synchronously calls each of the listeners
                            break; // Stop on the first token we find
                        }
                    }
                }
            }
            this._unprocessedLines.shift(); // Remove element [0]
        }
        callback();
    }
}

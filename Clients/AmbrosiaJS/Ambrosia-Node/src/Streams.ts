// Module for Ambrosia streams (OutgoingMessageStream, MemoryStream, StreamByteCounter, simpleCheckpointProducer/Consumer).
import Stream = require("stream");
import File = require("fs");
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as IC from "./ICProcess";
import * as Configuration from "./Configuration";
import * as StringEncoding from "./StringEncoding";
import * as Utils from "./Utils/Utils-Index";
import * as Messages from "./Messages";
import * as Root from "./AmbrosiaRoot";

export type BasicEventHandler = () => void;

/** Type of a handler for OutgoingMessageStream errors. */
export type ErrorHandler = (error: Error) => void;

/** Type of the object returned by a CheckpointProducer method. When the checkpoint has been sent (to the IC), onFinished will be called. */
export type OutgoingCheckpoint = 
{ 
    /** The outgoing [to the IC] stream of checkpoint data (serialized application state). */
    dataStream: Stream.Readable, 
    /** The length (in bytes) of the dataStream. */
    length: number, 
    /** 
     * Callback invoked when the send is complete, or an error has occurred.\
     * Note: Setting this to null is reserved for future use.
     */
    onFinished: ((error?: Error) => void) | null
};

/** Type of the object returned by a CheckpointConsumer method. */
export type IncomingCheckpoint = 
{
    /** The incoming [from the IC] stream of checkpoint data (serialized application state). */
    dataStream: Stream.Writable, 
    /** Callback invoked when the receive is complete, or an error has occurred. */
    onFinished: (error?: Error) => void 
};

/** Type of a method that generates (writes) serialized application state. */
export type CheckpointProducer = () => OutgoingCheckpoint;

/** Type of a method that loads (reads) serialized application state. */
export type CheckpointConsumer = () => IncomingCheckpoint;

/** 
 * A utility method that returns a simple in-memory, outgoing [to IC] checkpoint from the supplied application state.
 * After the checkpoint has been sent (to the IC), onFinished will be called.\
 * Use in conjunction with simpleCheckpointConsumer().
 */
export function simpleCheckpointProducer(appState: Root.AmbrosiaAppState, onFinished: ((error?: Error) => void) | null): OutgoingCheckpoint
{
    // Verify that the appState we're about to save [as a checkpoint] is the same appState that Ambrosia has been using to persist its internal state.
    // If this throws, it indicates that the user has [illegally] reassigned the appState since it was returned by the IC.initializeAmbrosiaState() call.
    IC.checkAmbrosiaState(appState);

    let stream: MemoryStream = new MemoryStream();
    let jsonAppState: string = Utils.jsonStringify(appState);
    let serializedState: Uint8Array = StringEncoding.toUTF8Bytes(jsonAppState);
    stream.end(serializedState);
    return ({ dataStream: stream, length: serializedState.length, onFinished: onFinished });
}

/** 
 * A utility method that returns a simple in-memory, incoming [from IC] checkpoint consumer. 
 * After the checkpoint has been received (from the IC), 'onFinished' will be called with the checkpoint deserialized to the 
 * application state (as instantiated by 'appStateConstructor', the application state class name).\
 * Use in conjunction with simpleCheckpointProducer().
 */
export function simpleCheckpointConsumer<T extends Root.AmbrosiaAppState>(appStateConstructor: new (restoredAppState?: T) => T, onFinished: (appState?: T, error?: Error) => void): IncomingCheckpoint
{
    Root.checkAppStateConstructor(appStateConstructor);

    let receiverStream: MemoryStream = new MemoryStream();

    function onCheckpointReceived(error?: Error)
    {
        let appState: T | undefined = undefined;

        if (!error)
        {
            appState = Utils.jsonParse(StringEncoding.fromUTF8Bytes(receiverStream.readAll())); // This deserializes the data
            appState = IC.initializeAmbrosiaState<T>(appStateConstructor, appState); // This rehydrates the class instance from the data
        }
        onFinished(appState, error);
    }

    return ({ dataStream: receiverStream, onFinished: onCheckpointReceived });
}

/** 
 * A class respresenting a stream for outgoing messages [to the IC].\
 * Add messages to the stream using either addBytes() or addStreamedBytes(), or queueBytes() followed by flushQueue().\
 * Note: This "wrapper" (over _lbSendSocket) enables us to implement control over the batching of outgoing messages (using RPCBatch).
 */
export class OutgoingMessageStream extends Stream.Readable
{
    private _allowRPCBatching: boolean;
    private _pendingMessages: Uint8Array[] = [];
    private _maxQueuedBytes: number = 1024 * 1024 * 256; // 256 MB, but optionally re-initialized in constructor
    private _queuedByteCount: number = 0;
    private _destinationStream: Stream.Writable; // Initialized in constructor
    private _onErrorHandler: ErrorHandler; // Initialized in constructor
    private _isProcessingByteStream: boolean = false;
    private _loggingPrefix = "READER";
    private _isClosing: boolean = false;
    private _flushPending = false;

    /** 
     * [ReadOnly] Whether a byte stream is currently in the process of being added [via addStreamedBytes()]. 
     * If true, wait until the onFinished callback of the current stream is invoked before attempting to add another stream.
     */
    get isProcessingByteStream(): boolean { return (this._isProcessingByteStream); }

    /** [ReadOnly] The current length of the message queue (in messages). */
    get queueLength(): number { return (this._pendingMessages.length); }

    /** [ReadOnly] The maximum number of bytes that the stream can queue (or internally buffer). Defaults to 256 MB. Cannot be smaller than 32 MB. */
    get maxQueuedBytes(): number { return (this._maxQueuedBytes);  }

    constructor(destinationStream: Stream.Writable, onError: ErrorHandler, allowRPCBatching: boolean = true, maxQueueSizeInBytes?: number, options?: Stream.ReadableOptions)
    {
        super(options)

        this._destinationStream = destinationStream;
        this._onErrorHandler = onError;
        this._allowRPCBatching = allowRPCBatching;
        if (maxQueueSizeInBytes)
        {
            this._maxQueuedBytes = Math.max(1024 * 1024 * 32, maxQueueSizeInBytes); // Enforce a 32 MB minimum
        }

        this.addEventHandlers();
        this.pipe(this._destinationStream); // pipe() sets up a pipeline orchestration between 2 streams, so this call does not block
    }

    private addEventHandlers(): void
    {
        // An error in the destination (writable) stream
        // Note: This [external] stream may already have its own on("error") handler; multiple event handlers will fire in the order they were added
        this._destinationStream.on("error", (error: Error) => 
        {
            this.handleError(error, "WRITER");
        });

        // An error in our (readable) stream
        this.on("error", (error: Error) => 
        {
            this.handleError(error, this._loggingPrefix);
        });

        // End-of-stream reached, eg. push(null)
        this.on("end", () => 
        {
            Utils.log("End event", this._loggingPrefix, Utils.LoggingLevel.Debug);
            if (!this._isClosing && !this.destroyed)
            {
                this.handleError(new Error("Stream unexpectedly ended"), this._loggingPrefix);
            }
        });

        // Stream closed (ie. destroyed). After this event, no more events will be emitted by the stream.
        this.on("close", () =>
        {
            Utils.log("Close event", this._loggingPrefix, Utils.LoggingLevel.Debug);
        });
    }

    private handleError(error: Error, source: string)
    {
        // We do this to avoid duplicate logging. The assumption is that if the user provided their own error handler, they will also take responsibility for logging.
        if (!this._onErrorHandler)
        {
            Utils.log("Error: " + error.message, source);
        }

        try
        {
            this.unpipe(this._destinationStream);
            if (!this._destinationStream.destroyed)
            {
                this._destinationStream.end(); // This will make _destinationStream [synchronously] emit a 'finish' event
                this._destinationStream.destroy(); // This will make _destinationStream [asynchronously] emit a 'close' event
            }
            if (!this.destroyed)
            {
                this.destroy();
            }

            if (this._onErrorHandler)
            {
                // We use setImmediate() here to allow any [asynchronous] events (like 'close') to fire
                setImmediate(() => this._onErrorHandler(error));
            }
        }
        catch (error: unknown)
        {
            Utils.log(Utils.makeError(error));
        }
    }
        
    // Called when a consumer (in our case 'pipe()') wants to read data from the stream.  
    // Note: We MUST provide an implementation for this method, even if it's a no-op.
    // Note: When we push() to the stream this method will end up being called, even if the stream isn't being piped.
    _read(size: number): void
    {
        try
        {
            // No-op
        }
        catch (error: unknown)
        {
            // This is the ONLY supported way to report an error [to the on("error") handler] from _read
            // The stream will emit 'error' (synchronously) then 'close' (asynchronously) events
            this.destroy(Utils.makeError(error));
        }
    }

    /** Returns true if the stream can be closed using close(). */
    canClose(): boolean
    {
        return (!this.destroyed && !this._isClosing);
    }

    /** Closes the stream. The stream cannot be used again after this has been called. */
    close(onClosed?: BasicEventHandler): void
    {
        this.checkDestroyed();

        if (this._isClosing)
        {
            return;
        }
        this._isClosing = true;

        // Push null to cause the destination stream to emit the 'finish' event [after it flushes its last write]
        this.push(null); // EOF [push() will return false indicating that no more data can be pushed]
        
        // Wait for the destination stream to finish
        this._destinationStream.once("finish", () => 
        { 
            this.unpipe(this._destinationStream);
            this.destroy();
            this._destinationStream.destroy();

            if (onClosed)
            {
                // We use setImmediate() here to allow our 'end'/'close' events, and _destinationStream's 'close' event, to fire
                setImmediate(onClosed);
            }
        });
    }

    /** Checks if the stream has been destroyed and, if so, throws. */
    private checkDestroyed(): void
    {
        if (this.destroyed)
        {
            throw new Error("Invalid operation: The stream has already been destroyed");
        }
    }

    /** Checks if the stream's internal buffer has exceeded the size limit (_maxQueuedBytes) and, if so, throws. */
    private checkInternalBuffer(): void
    {
        if (this.readableLength > this._maxQueuedBytes)
        {
            // Throwing here is more deterministic than failing at some unpredictable location with OOM (if we let the internal buffer grow unchecked).
            // This exception indicates that the stream is not emptying (likely due to I/O starvation).
            throw new Error(`The stream buffer size limit (${this._maxQueuedBytes} bytes) has been exceeded (${this.readableLength} bytes))`);
        }
    }

    /** 
     * Asynchronously adds bytes to the stream from another stream.
     * Only one byte stream at-a-time can be added, and messages added with addBytes() will queue until the byte stream ends.
     * Useful, for example, to send a large checkpoint (binary blob).
     *
     * This method will flush any queued messages before streaming begins, and after streaming ends.
     */
     addStreamedBytes(byteStream: Stream.Readable, expectedStreamLength: number = -1, onFinished?: (error?: Error) => void): void
     {
        let startTime: number = Date.now();
        let bytesSentCount: number = 0;

        this.checkDestroyed();
        this.checkInternalBuffer();
        
        if (this._isProcessingByteStream)
        {
            throw new Error("Another byte stream is currently being being added; try again later (after the onFinished callback for that stream has been invoked)");
        }

        const pendingMessageCount: number = this.flushQueue();
        if (pendingMessageCount > 0)
        {
            Utils.log(`Flushed ${pendingMessageCount} messages from the outgoing queue (before streaming)`, null, Utils.LoggingLevel.Minimal);
        }

        this._isProcessingByteStream = true; // Must be set AFTER calling flushQueue()
        
        const onAddStreamedBytesFinished: (error?: Error) => void = (error?: Error) =>
        {
            if (!this._isProcessingByteStream)
            {
                return;
            }
            this._isProcessingByteStream = false;

            const elapsedMs: number = Date.now() - startTime;

            if (error)
            {
                Utils.log(`Byte stream read failed (in ${elapsedMs}ms): ${Utils.makeError(error).message}`, this._loggingPrefix, Utils.LoggingLevel.Minimal);
            }
            else
            {
                Utils.log(`Byte stream read ended (in ${elapsedMs}ms)`, this._loggingPrefix, Utils.LoggingLevel.Debug);
                if ((expectedStreamLength >= 0) && (bytesSentCount !== expectedStreamLength))
                {
                    // For example, if we're sending checkpoint data to the IC but the length we sent to it (in a Checkpoint 
                    // message) has a length that doesn't match the length of the data stream we sent, then we should error
                    throw new Error(`The stream contained ${bytesSentCount} bytes when ${expectedStreamLength} were expected`);
                }
            }

            if (onFinished)
            {
                onFinished(error);
            }

            // Flush anything that accumulated in the queue while we were processing byteStream [typically, there should be nothing to flush]
            const pendingMessageCount: number = this.flushQueue();
            if (pendingMessageCount > 0)
            {
                Utils.log(`Flushed ${pendingMessageCount} messages from the outgoing queue (after streaming)`, null, Utils.LoggingLevel.Minimal);
            }
    
            // Remove handlers
            byteStream.removeListener("data", onByteStreamData);
            byteStream.removeListener("end", onAddStreamedBytesFinished);
            byteStream.removeListener("error", onAddStreamedBytesFinished);
        };

        const onByteStreamData: (data: Buffer) => void = (data: Buffer) =>
        {
            this.push(data); // If this returns false, it will just buffer
            bytesSentCount += data.length;
        };

        byteStream.on("data", onByteStreamData);
        byteStream.on("end", onAddStreamedBytesFinished); // 'end' will be raise when there is no more data to consume from byteStream (ie. end-of-stream is reached)
        byteStream.on("error", onAddStreamedBytesFinished);
    } 

    /** 
     * Adds byte data to the stream.
     * The data ('bytes') should always represent a complete message (to enable batching).\
     * To avoid excessive memory usage, for very large data (ie. 10's of MB) consider using addStreamedBytes(). 
     */
    addBytes(bytes: Uint8Array, immediateFlush: boolean = false): void
    {
        this.queueBytes(bytes);
        if (this._isProcessingByteStream)
        {
            // We must not push() to the stream while we're in the process of adding a byte stream [using addStreamedBytes()],
            // so instead we just accumulate the messages in the queue (ignoring the immediateFlush value) which will be 
            // automatically flushed when the byte stream completes.
            return;
        }
    
        if (immediateFlush)
        {
            this.flushQueue();
        }
        else
        {
            if (!this._flushPending)
            {
                // We use setImmediate() here so that addBytes() can be called repeatedly (eg. in a loop) without
                // flushing on each call (the flush will happen AFTER the function containing the loop finishes).
                // This is "implicit" batching ("explicit" batching is using queueBytes() and calling flushQueue() explicitly).
                // Note: This will result in messages accumulating in _pendingMessages.
                setImmediate(() => this.flushQueue());
                this._flushPending = true;
            }
        }
    }

    /** 
     * Queues byte data to be flushed to the stream later using flushQueue().
     * The data ('bytes') should always represent a complete message (to enable batching).\
     * To avoid excessive memory usage, for very large data (ie. 10's of MB) consider using addStreamedBytes(). 
     */
    queueBytes(bytes: Uint8Array): void
    {
        this.checkDestroyed();
        this.checkInternalBuffer();

        /*
        ** WARNING: This is untested code!
        ** 9/8/21: It's not clear if this added complexity is required, so leaving commented out for now.
        // If recovery is running and there's no room for 'bytes' in the queue, then try to make room by flushing.
        // This can [theoretically] happen if a received log page (containing multiple messages) is larger than _maxQueuedBytes.
        // In practice, the IC seems to avoid sending "oversize" log pages (eg. bundling 2+ "large" messages into a single log page)
        // which means that flushQueue() already gets called "naturally" [via setImmediate()] between received log pages.
        if (Messages.isRecoveryRunning() && ((this._queuedByteCount + bytes.length) > this._maxQueuedBytes))
        {
            this.flushQueue();
        }
        */

        if ((this._queuedByteCount + bytes.length) <= this._maxQueuedBytes)
        {
            this._pendingMessages.push(bytes);
            this._queuedByteCount += bytes.length;
        }
        else
        {
            // Note: This could also be the result of the user not calling IC.flushQueue() frequently enough
            throw new Error(`Cannot add the supplied ${bytes.length} bytes (reason: The stream queue size limit (${this._maxQueuedBytes} bytes) would be exceeded; consider increasing the 'lbOptions.maxMessageQueueSizeInMB' configuration setting)`);
        }        
    }

    /** 
     * Flushes the message queue to the stream.\
     * Returns the number of messages (not bytes) that were queued (and flushed). 
     * A returned negative number indicates that the queue was not flushed and still contains that number of messages.\
     * Note: Calling flushQueue() [synchronously] while reading from the IC will NOT result in interleaved I/O, so OutgoingMessageStream will just buffer.
     */
    flushQueue(): number
    {
        let bytes: Uint8Array;
        let messageCount: number = this._pendingMessages.length;
        let logLevel: Utils.LoggingLevel = Utils.LoggingLevel.Debug;

        if (this._isProcessingByteStream)
        {
            // When addStreamedBytes() completes it will flush any messages that accumulated in the queue while it was was processing the stream.
            // Aside: If a message is accidentally flushed AFTER a Checkpoint message has been sent but BEFORE the checkpoint stream starts, then
            //        the IC will fail with "FATAL ERROR 0: Illegal leading byte in local message".
            return (-messageCount);
        }

        this._flushPending = false;

        if (messageCount === 0)
        {
            return (0);
        }

        if (messageCount === 1)
        {
            // No need to batch when we only have one message
            bytes = this._pendingMessages[0];                
        }
        else
        {
            // Concatenate into a "batch", optionally using an RPCBatch [which has an optimized processing path in the IC].
            // Note: It's valid to re-batch outgoing messages [with RPCBatch] during recovery.
            if (this._allowRPCBatching && Messages.canUseRPCBatch(this._pendingMessages))
            {
                // Rather than pre-pending the RPCBatch header to _pendingMessages, we just push it directly [thereby avoiding a potentially costly memory copy]
                let rpcBatchHeader: Uint8Array = Messages.makeRPCBatchMessageHeader(messageCount, this._queuedByteCount);
                let readableLength: number = this.readableLength; // The number of bytes in the queue ready to be read
                let wasBuffered: boolean = !this.push(rpcBatchHeader); // If this returns false, it will just buffer
                let headerBytesPushed: number = wasBuffered ? (this.readableLength - readableLength) : rpcBatchHeader.length;
                if (headerBytesPushed !== rpcBatchHeader.length)
                {
                    throw new Error(`Only ${headerBytesPushed} of ${rpcBatchHeader.length} RPCBatch-header bytes were pushed`);
                }            
                if (Utils.canLog(logLevel))
                {
                    Utils.log(`${messageCount} RPC messages batched: Pushed ${headerBytesPushed} RPCBatch-header bytes ${wasBuffered ? "[buffered] " : ""}`, null, logLevel);
                }
                if (Utils.canLog(Utils.LoggingLevel.Verbose))
                {
                    // An RPCBatch only contains outgoing RPC messages, each of which specifies a destination, so the batch can ONLY be sent to the local IC
                    Utils.log(`Sending 'RPCBatch' (of ${messageCount} messages) to local IC (${rpcBatchHeader.length + this._queuedByteCount} bytes)`, null, Utils.LoggingLevel.Verbose);
                }
            }
            bytes = Buffer.concat(this._pendingMessages, this._queuedByteCount);
        }

        let readableFlowing: boolean | null = this.readableFlowing; // If readableFlowing is null it means that "no mechanism for consuming the stream's data is provided"
        let readableLength: number = this.readableLength; // The number of bytes in the queue ready to be read
        let bytesPushed: number = 0;
        let warning: string = "";

        if (Utils.canLog(logLevel))
        {
            Utils.log(`Pushing ${bytes.length} bytes, ${messageCount} messages`, this._loggingPrefix, logLevel);
        }

        // Note: The writable being piped to will respond immediately to push(), NOT at the next tick of the event loop.
        if (this.push(bytes)) // If this returns false, it will just buffer
        {
            bytesPushed = bytes.length;
        }
        else
        {
            // The number of pushed bytes (or this.readableLength + pushed bytes) exceeded this.readableHighWaterMark.
            // Note: The stream's internal buffer will accumulate the bytes, but seems to grow indefinitely (presumably 
            //       until Node.js fails with OOM; which is why we have checkInternalBuffer()).
            bytesPushed = this.readableLength - readableLength; // This *should* be the same as bytes.length
            warning = `(Warning: the stream is full [${this.readableLength} bytes buffered / ${this.readableHighWaterMark} bytes highWaterMark])`;
        }

        if (bytesPushed === bytes.length)
        {
            let logLevel: Utils.LoggingLevel = warning ? Utils.LoggingLevel.Verbose : Utils.LoggingLevel.Debug;
            if (Utils.canLog(logLevel))
            {
                Utils.log(`Pushed ${bytesPushed} bytes ${warning}`, this._loggingPrefix, logLevel);
            }
            this._pendingMessages = [];
            this._queuedByteCount = 0;
        }
        else
        {
            throw new Error(`Only ${bytesPushed} of ${bytes.length} bytes were pushed, so the pending message queue could not be cleared (messageCount: ${messageCount}, readableLength: ${this.readableLength}, readableFlowing: ${readableFlowing}, destroyed: ${this.destroyed})`);
        }

        return (messageCount);
    }
}

/** 
 * Provides a basic in-memory stream, which can be both written to and read from. 
 * Read the entire stream at once with readAll(), or read the entire stream in chunks (in a loop) using readUpTo().
 * The lastReadSize/lastWriteSize properties track the result of the last read/write operation.
 * The maximum size of the MemoryStream can be set by specifying a maxSize value in the constructor.
 */
export class MemoryStream extends Stream.Duplex
{
    private _maxSize: number;
    private _lastReadSize: number = 0;
    private _lastWriteSize: number = 0;
    private _totalBytesWritten: number = 0;

    /** [ReadOnly] The maximum size (in bytes) of the MemoryStream (-1 means no limit). */
    get maxSize(): number { return (this._maxSize); }
    /** [ReadOnly] The number of bytes read with the last read/readAll/readUpTo operation. */
    get lastReadSize(): number { return (this._lastReadSize); }
    /** [ReadOnly] The number of bytes written with the last write operation. */
    get lastWriteSize(): number { return (this._lastWriteSize); }
    /** [ReadOnly] The total number of bytes written (so far). */
    get totalBytesWritten(): number { return (this._totalBytesWritten); }
    /** [ReadOnly] Whether writing to the stream has ended. Note: This will only become true if MemoryStream.end() is called. */
    get writingEnded(): boolean { return (this.writableEnded); }

    constructor(maxSize: number = -1)
    {
        super();
        this._maxSize = Math.max(-1, maxSize);
    }

    _write(chunk: Buffer, encoding: string, callback: (error?: Error) => void): void
    {
        if ((this._maxSize !== -1) && (this.readableLength + chunk.length > this._maxSize))
        {
            callback(new Error(`Writing ${chunk.length} bytes would exceed the MemoryStream maxSize (${this._maxSize} bytes) by ${ this.readableLength + chunk.length - this._maxSize} bytes`));
        }
        else
        {
            if (chunk.length > 0)
            {
                this.push(chunk); // If this returns false, it will just buffer
                this._lastWriteSize = chunk.length; 
                this._totalBytesWritten += chunk.length;
            }
            // Utils.log(`DEBUG: MemoryStream wrote ${this._lastWriteSize} bytes`);
            callback(); 
        }
    }

    // Called before the stream closes (ie. [some time] after Duplex.end() is called)
    _final(callback: (error?: Error) => void): void
    {
        // Push null to cause the destination stream [when piping] to emit the 'finish' event [after it flushes its last write]
        this.push(null); // EOF [push() will return false indicating that no more data can be pushed]
        callback();
    }

    _read(size: number): void
    {
    }

    /** Reads all available data in the MemoryStream. Returns null if no data is available. */
    readAll(): Buffer
    {
        let isEmptyStream: boolean = this.writableEnded && (this._totalBytesWritten === 0);
        let chunk: Buffer = isEmptyStream ? Buffer.alloc(0) : this.read(this.readableLength); // Note: read() returns null if no data is available
        this._lastReadSize = (chunk === null) ? 0 : chunk.length;
        // Utils.log(`DEBUG: MemoryStream read ${this._lastReadSize} bytes`);
        return (chunk);
    }

    /** Reads - at most - size bytes, but may read less if fewer bytes are available. Returns null if no data is available. */
    readUpTo(size: number): Buffer
    {
        let isEmptyStream: boolean = this.writableEnded && (this._totalBytesWritten === 0);
        let chunk: Buffer = isEmptyStream ? Buffer.alloc(0) : this.read(Math.min(size, this.readableLength));
        this._lastReadSize = (chunk === null) ? 0 : chunk.length;
        return (chunk);
    }
}

/** 
 * A simple stream processor that asynchronously counts the bytes in a stream, calling the supplied onFinished handler when the count is complete.\
 * Example usage: fs.createReadStream("./lib/Demo.js").pipe(new StreamByteCounter((count: number) => Utils.log(count)));
 */
class StreamByteCounter extends Stream.Transform
{
    private _onFinished: ((finalByteCount: number) => void) | undefined;
    private _finalByteCount: number = -1;
    private _currentByteCount: number = 0;
    private _bufferingCount: number = 0; // Used for debugging only
    private _chunkCount: number = 0; // Used for debugging only

    /** The length of the stream in bytes. Will be -1 until writing to the stream has finished. */
    public get byteCount(): number { return (this._finalByteCount); }

    constructor(onFinished?: (finalByteCount: number) => void, options?: Stream.WritableOptions)
    {
        super(options);
        this._onFinished = onFinished;
    }

    // Called when there's no more written data to be consumed (ie. StreamByteCounter.end() is [implicitly] called, 
    // which typically happens when the stream that's being piped into StreamByteCounter has it's end() called).
    // Consequently, it's possible that _flush() may never be called (but which would indicate a coding error in
    // the stream being piped into StreamByteCounter).
    _flush(callback: (error?: Error) => void): void
    {
        this._finalByteCount = this._currentByteCount; 
        // Utils.log(`StreamByteCounter: Number of times buffering occurred: ${this._bufferingCount} (${((this._bufferingCount / this._chunkCount) * 100).toFixed(2)}%)`);

        if (this._onFinished)
        {
            this._onFinished(this._finalByteCount);
        }
        callback();
    }

    _transform(chunk: Buffer, encoding: string, callback: (error?: Error) => void): void
    {
        try
        {
            if (!this.push(chunk)) // If this returns false, it will just buffer
            {
                this._bufferingCount++;
            }
            this._chunkCount++;
            this._currentByteCount += chunk.length;
            callback(); // Signal [to the caller of 'write'] that the write completed
        }
        catch (error: unknown)
        {
            callback(Utils.makeError(error)); // Signal [to the caller of 'write'] that the write failed
        }
    }
}

/** 
 * Asynchronously counts the bytes in a stream, calling the supplied onDone handler when the count is complete. 
 * Note: The supplied stream will not be re-readable (unless it has persistent storage, like a file).
 */
export function getStreamLength(stream: Stream.Readable, onDone: (length: number) => void): void
{
    stream.pipe(new StreamByteCounter((count: number) => onDone(count)));
}

export function inMemStreamTest(): void
{
    let memStream: MemoryStream = new MemoryStream();
    let iterations: number = 3;
    let writeChunkSize: number = 64;

    for (let i = 0; i < iterations; i++)
    {
        memStream.write(new Uint8Array(writeChunkSize + i).fill(99));
        Utils.log(`lastWriteSize: ${memStream.lastWriteSize}`);
    }

    let data: Uint8Array;
    while (data = memStream.readUpTo(61)) // Math.min(61, memStream.readableLength)))
    {
        Utils.log(`lastReadSize: ${memStream.lastReadSize}, writingEnded: ${memStream.writingEnded}`);
    }

    memStream.end(new Uint8Array(123).fill(88));
    data = memStream.readAll();
    Utils.log(`lastReadSize: ${memStream.lastReadSize}, writingEnded: ${memStream.writingEnded}`);
}

/** Tests OutgoingMessageStream [without using the IC]. */
export function startStreamTest(): void
{
    // Check that we'll see OutgoingMessageStream output in the console
    if (!Utils.canLog(Utils.LoggingLevel.Debug) || !Utils.canLogToConsole())
    {
        Utils.log(`Incorrect configuration for test: 'outputLoggingLevel' must be '${Utils.LoggingLevel[Utils.LoggingLevel.Debug]}', and 'outputLogDestination' must include '${Configuration.OutputLogDestination[Configuration.OutputLogDestination.Console]}'`);
        return;
    }

    let stopStreamTest: BasicEventHandler = () =>
    {
        Utils.consoleInputStop();
    };

    Utils.log("startStreamTest() running: Press 'x' or 'Enter' to stop");

    // Send the [readable] inStream to the [writable] outStream, ie. this is how to consume a "flowing" readable stream using a
    // writable stream [the writable stream here serves as a stream processor, it doesn't actually need to "write" the stream].
    // When using pipe() we don't need to worry about responding to stream events ('drain', 'readable, 'data', etc.) because pipe()
    // handles them internally. It is recommended to avoid mixing the pipe end event-driven approaches.
    let outStream: ConsoleOutStream = new ConsoleOutStream({}, 64);
    let inStream: OutgoingMessageStream = new OutgoingMessageStream(outStream, (error: Error) => stopStreamTest(), false); // Note: We disable RPC batching

    // Add to the inStream each time a key is pressed
    Utils.consoleInputStart((char: string) => 
    {
        let message: string = "";

        switch (char)
        {
            case "x":
            case Utils.ENTER_KEY:
                inStream.close(() => 
                {
                    Utils.log("startStreamTest() stopping");
                    stopStreamTest();
                });
                break;
            case "b": // Add a "big" message
                message = char.repeat(127112 * 64);
                inStream.addBytes(StringEncoding.toUTF8Bytes(message));
                break;
            case "s": // Add from a stream
                inStream.addStreamedBytes(File.createReadStream("./lib/Demo.js"));
                return;
            case "l": // Add in a loop
                for (let i = 0; i < 12; i++)
                {
                    inStream.addBytes(StringEncoding.toUTF8Bytes(char));
                }
                return;
            case "f": // Flush test
                inStream.addBytes(StringEncoding.toUTF8Bytes("Hello"));
                inStream.addBytes(StringEncoding.toUTF8Bytes("World"));
                inStream.addStreamedBytes(File.createReadStream("./lib/Demo.js")); // This will automatically flush the 2 items in the queue
                break;
            default:
                message = `Key pressed! ('${char}')`; 
                inStream.addBytes(StringEncoding.toUTF8Bytes(message));
            }
    });
}

/** [For testing only] A writable stream that reports (to the console) how much data it was sent. */
class ConsoleOutStream extends Stream.Writable
{
    _maxBytesToDisplay: number = 0;
    _loggingPrefix: string = "WRITER";

    constructor(options?: Stream.WritableOptions, maxBytesToDisplay: number = 128)
    {
        super(options);
        this._maxBytesToDisplay = maxBytesToDisplay;
        this.addEventHandlers();
    }

    // Called when a consumer wants to write data to the stream
    _write(chunk: Buffer, encoding: string, callback: (error?: Error) => void): void
    {
        try
        {
            if (encoding !== "buffer")
            {
                throw new Error(`his stream can only write Buffer/Uint8Array data, not '${encoding}' data`);
            }

            if (chunk.length > 0)
            {
                // Write the data to its destination (in our case, to the console)
                let displayBytes: string = (chunk.length <= this._maxBytesToDisplay) ? Utils.makeDisplayBytes(chunk) : "...";
                Utils.log(`Writing ${chunk.length} bytes (${displayBytes})`, this._loggingPrefix);
            }
            callback(); // Signal [to the caller of 'write'] that the write completed
        }
        catch (error: unknown)
        {
            // This is the ONLY supported way to report an error [to the on("error") handler] from _write()
            callback(Utils.makeError(error));
        }
    }

    private addEventHandlers(): void
    {
        this.on("error", (error: Error) => 
        {
            Utils.log("Error: " + error.message, this._loggingPrefix);
        });

        // All writes are complete and flushed
        this.on("finish", (...args: any[]) =>
        {
            Utils.log("Finish event", this._loggingPrefix);
        });

        // Stream closed (ie. destroyed). After this event, no more events will be emitted by the stream.
        this.on("close", () =>
        {
            Utils.log("Close event", this._loggingPrefix);
        });

        // The stream has drained all data from the internal buffer
        this.on("drain", () =>
        {
            Utils.log("Drained event", this._loggingPrefix);
        })
    }
}
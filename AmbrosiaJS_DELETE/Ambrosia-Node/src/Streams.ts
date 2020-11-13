// Module for Ambrosia streams (OutgoingMessageStream, MemoryStream, StreamByteCounter, simpleCheckpointProducer/Consumer).
import Stream = require("stream");
import File = require("fs");
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as IC from "./ICProcess";
import * as Configuration from "./Configuration";
import * as StringEncoding from "./StringEncoding";
import * as Utils from "./Utils/Utils-Index";
import * as Messages from "./Messages";

export type BasicEventHandler = () => void;

/** Type of a handler for OutgoingMessageStream errors. */
export type ErrorHandler = (error: Error) => void;

/** Type of the object returned by a CheckpointProducer method. When the checkpoint has been sent (to the IC), onFinished will be called. */
export type OutgoingCheckpoint = { dataStream: Stream.Readable, length: number, onFinished: (error?: Error) => void };

/** Type of the object returned by a CheckpointConsumer method. */
export type IncomingCheckpoint = { dataStream: Stream.Writable, onFinished: (error?: Error) => void };

/** Type of a method that generates (writes) serialized application state. */
export type CheckpointProducer = () => OutgoingCheckpoint;

/** Type of a method that loads (reads) serialized application state. */
export type CheckpointConsumer = () => IncomingCheckpoint;

const END_OF_DATA_MARKER: Uint8Array = new Uint8Array([0xFB, 0xFC, 0xFA, 0xFE]); // Experimental [not used]
const EOD_MARKER_DETECTED_EVENT: string = "EODMarkerDetected"; // Experimental [not used]
const QUEUE_FLUSHED_EVENT: string = "Queueflushed";

/** 
 * A utility method that returns a simple in-memory, outgoing [to IC] checkpoint from the supplied JSON string (of application state).
 * When the checkpoint has been sent (to the IC), onFinished will be called.\
 * Use in conjunction with simpleCheckpointConsumer().
 */
export function simpleCheckpointProducer(jsonAppState: string, onFinished?: (error?: Error) => void): OutgoingCheckpoint
{
    let stream: MemoryStream = new MemoryStream();
    let serializedState: Uint8Array = StringEncoding.toUTF8Bytes(jsonAppState);
    stream.end(serializedState);
    return ({ dataStream: stream, length: serializedState.length, onFinished: onFinished });
}

/** 
 * A utility method that returns a simple in-memory, incoming [from IC] checkpoint consumer. 
 * When the checkpoint has been received (from the IC), onFinished will be called with the checkpoint deserialized to a JSON string (of application state).\
 * Use in conjunction with simpleCheckpointProducer().
 */
export function simpleCheckpointConsumer(onFinished: (jsonAppState: string, error?: Error) => void): IncomingCheckpoint
{
    let receiverStream: MemoryStream = new MemoryStream();

    function onCheckpointReceived(error?: Error)
    {
        let jsonAppState: string = error ? null : StringEncoding.fromUTF8Bytes(receiverStream.readAll());
        IC.initializeAmbrosiaState(Utils.jsonParse(jsonAppState));
        onFinished(jsonAppState, error);
    }

    return ({ dataStream: receiverStream, onFinished: onCheckpointReceived });
}

/** 
 * A class respresenting a stream for outgoing messages [to the IC].\
 * Add messages to the stream using either addBytes() or addStreamedBytes(), or queueBytes() followed by flushAsync().
 */
export class OutgoingMessageStream extends Stream.Readable
{
    private _allowRPCBatching: boolean;
    private _pendingMessages: Uint8Array[] = [];
    private _maxQueuedBytes: number = 1024 * 1024 * 64; // 64MB
    private _maxBufferedBytes: number = 1024 * 1024 * 256; // 256MB
    private _queuedByteCount: number = 0;
    private _destinationStream: Stream.Writable = null;
    private _isProcessingByteStream: boolean = false;
    private _loggingPrefix = "READER";
    private _isClosing: boolean = false;
    private _flushPending = false;
    private _onErrorHandler: ErrorHandler = null;

    /** 
     * [ReadOnly] Whether a byte stream is currently in the process of being added [via addStreamedBytes()]. 
     * If true, wait until the onFinished callback of the current stream is invoked before attempting to add another stream.
     */
    get isProcessingByteStream(): boolean { return (this._isProcessingByteStream); }

    /** [ReadOnly] The current length of the message queue (in messages). */
    get queueLength(): number { return (this._pendingMessages.length); }

    constructor(destinationStream: Stream.Writable, onError: ErrorHandler, allowRPCBatching: boolean = true, maxQueueSizeInBytes?: number, options?: Stream.ReadableOptions)
    {
        super(options)

        this._destinationStream = destinationStream;
        this._onErrorHandler = onError;
        this._allowRPCBatching = allowRPCBatching;
        if (maxQueueSizeInBytes)
        {
            this._maxQueuedBytes = maxQueueSizeInBytes;
        }

        this.addEventHandlers();
        this.pipe(this._destinationStream); // pipe() sets up a pipeline orchestration between 2 streams, so this call does not block
    }

    private addEventHandlers(): void
    {
        // An error in the destination (writable) stream
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
            Utils.log("End event", this._loggingPrefix, Utils.LoggingLevel.Verbose);
            if (!this._isClosing && !this.destroyed)
            {
                this.handleError(new Error("Stream unexpectedly ended"), this._loggingPrefix);
            }
        });

        // Stream closed (ie. destroyed). After this event, no more events will be emitted by the stream.
        this.on("close", () =>
        {
            Utils.log("Close event", this._loggingPrefix, Utils.LoggingLevel.Verbose);
        });
    }

    private handleError(error: Error, source: string)
    {
        Utils.log("Error: " + error.message, source);

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
        catch (error)
        {
            Utils.log(error);
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
        catch (error)
        {
            // This is the ONLY supported way to report an error [to the on("error") handler] from _read
            // The stream will emit 'error' (synchronously) then 'close' (asynchronously) events
            this.destroy(error);
        }
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

    /** Checks if the stream's internal buffer has exceeded the size limit (_maxBufferedBytes) and, if so, throws. */
    private checkInternalBuffer(): void
    {
        if (this.readableLength > this._maxBufferedBytes)
        {
            // This is more deterministic than failing at some unpredictable location with OOM
            throw new Error(`The stream buffer size limit (${this._maxBufferedBytes} bytes) has been exceeded (${this.readableLength} bytes))`);
        }
    }

    /** 
     * Adds bytes to the stream from another stream.
     * Only one byte stream at-a-time can be added, and messages added with addBytes() will queue until the byte stream ends.
     * Useful, for example, to send a large checkpoint (binary blob) eg. from a file. 
     */
    addStreamedBytes(byteStream: Stream.Readable, expectedStreamLength: number = -1, onFinished?: (error?: Error) => void): void
    {
        let startTime: number = Date.now();

        this.checkDestroyed();
        this.checkInternalBuffer();
        
        if (this._isProcessingByteStream)
        {
            throw new Error("Another byte stream is currently being being added; try again later (after the onFinished callback for that stream has been invoked)");
        }

        // This is a 'callback' that will be invoked once any pending messages have been flushed (or if there are no pending messages to flush)
        let onQueueFlushed: BasicEventHandler = () =>
        {
            let byteCounter: StreamByteCounter = new StreamByteCounter(); // Used to count the bytes in byteStream [so that we can verify against expectedStreamLength]

            this.unpipe(this._destinationStream); // Temporarily disconnect our stream from _destinationStream
            byteStream.pipe(byteCounter).pipe(this._destinationStream, { end: false }); // Prevent _destinationStream from ending when byteStream ends

            // Because we unpiped our stream to use byteStream, our 'readableFlowing' property will become false
            let readableFlowing: boolean | null = this.readableFlowing; // If readableFlowing is null it means that "no mechanism for consuming the stream's data is provided"
            Utils.log(`Pushing byte stream: New messages will queue until the byte stream ends`, this._loggingPrefix, Utils.LoggingLevel.Verbose);
            
            let onAddStreamedBytesFinished: (error?: Error) => void = (error?: Error) =>
            {
                if (!this._isProcessingByteStream)
                {
                    return;
                }

                this._isProcessingByteStream = false; 
                this.pipe(this._destinationStream);
                Utils.log(`Byte stream read ${error ? "failed: " + error.message : "ended"} (in ${Date.now() - startTime}ms)`, this._loggingPrefix, error ? Utils.LoggingLevel.Normal : Utils.LoggingLevel.Verbose);

                if (!error && (expectedStreamLength >= 0) && (byteCounter.byteCount !== expectedStreamLength))
                {
                    // For example, if we're sending checkpoint data to the IC but the length we sent to it (in a Checkpoint 
                    // message) has a length that doesn't match the length of the data stream we sent, then we should error
                    throw new Error(`The stream contained ${byteCounter.byteCount} bytes when ${expectedStreamLength} were expected`);
                }

                if (onFinished)
                {
                    onFinished(error);
                }

                // Flush anything that accumulated in the queue while we were processing byteStream
                this.flushQueue();

                // Remove handlers - only one of which will have been invoked
                byteStream.removeListener("end", onAddStreamedBytesFinished);
                byteStream.removeListener("error", onAddStreamedBytesFinished);
            };

            // 'end' will be raise when there is no more data to consume from byteStream (ie. end-of-stream is reached).
            // Note: We can't use the 'finish' event of _destinationStream, because it won't get raised due to the "{ end: false }" option on [the temporary] pipe()
            byteStream.on("end", onAddStreamedBytesFinished);
            byteStream.on("error", onAddStreamedBytesFinished);
        };

        if (this._pendingMessages.length > 0)
        {
            // Don't start streaming until the queue is flushed (because when we start streaming we're going to [temporarily] unpipe from it, and 
            // we don't want this to compromise the delivery of any queued messages). 
            // TODO: We need more testing of this because even though the READER has pushed all the queued messages it's not clear if this 
            //       means that the WRITER has (or will) receive them, ie. is it really safe to unpipe in this state? The approach that uses
            //       EOD_MARKER_DETECTED_EVENT requires the WRITER to support this, so we'd need to create a wrapper for Net.Socket (rather 
            //       than just passing a Net.Socket [IC._icSendSocket] directly in the OutgoingMessageStream constructor).
            this.once(QUEUE_FLUSHED_EVENT, () =>
            {
                Utils.log("Queue flushed", this._loggingPrefix, Utils.LoggingLevel.Verbose);
                onQueueFlushed(); 
            });
            this.flushQueue();
            this._isProcessingByteStream = true; // Must be set AFTER calling flushQueue()

            // So that we can be sure that the pending messages reached the _destinationStream (and therefore it's safe to unpipe from it),
            // we add some "End Of Data" marker bytes which when the _destinationStream sees it will raise the EOD_MARKER_DETECTED_EVENT
            /*
            this._destinationStream.once(EOD_MARKER_DETECTED_EVENT, () => 
            { 
                Utils.log("Destination stream flushed", this._loggingPrefix);
                onQueueFlushed(); 
            });
            this.addBytes(END_OF_DATA_MARKER);
            this.flushQueue();
            */
        }
        else
        {
            this._isProcessingByteStream = true;
            onQueueFlushed();
        }
    }

    /** 
     * Adds byte data to the stream.
     * The data ('bytes') should always represent a complete message (to enable batching).\
     * To avoid excessive memory usage, for very large data (ie. 10's of MB) consider using addStreamedBytes(). 
     */
    addBytes(bytes: Uint8Array, immediateFlush: boolean = false)
    {
        this.queueBytes(bytes);
        if (this._isProcessingByteStream)
        {
            // We don't push() to the [unpiped, ie. paused] stream while we're in the process of [temporarily] piping a byte stream
            // [see addStreamedBytes()], and instead we just accumulate the messages in the queue (ignoring the immediateFlush value)
            // which will be automatically flushed when the byte stream completes.
            // This allows us to set independent limits for both the queue and the stream's internal buffer.
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
    queueBytes(bytes: Uint8Array)
    {
        this.checkDestroyed();
        this.checkInternalBuffer();

        if ((this._queuedByteCount + bytes.length) < this._maxQueuedBytes)
        {
            this._pendingMessages.push(bytes);
            this._queuedByteCount += bytes.length;
        }
        else
        {
            throw new Error(`Cannot add the supplied ${bytes.length} bytes (reason: The stream queue size limit (${this._maxQueuedBytes} bytes) would be exceeded)`);
        }        
    }

    /** 
     * Flushes the message queue to the stream.\
     * Returns the number of messages (not bytes) that were queued (and flushed). 
     * A returned negative number indicates that the queue was not flushed and still contains that number of messages.
     */
    flushQueue(): number
    {
        let bytes: Uint8Array = null;
        let messageCount: number = this._pendingMessages.length;
        let logLevel:  Utils.LoggingLevel = Utils.LoggingLevel.Verbose;

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
            // Concatenate into a "batch", optionally using an RPCBatch [which has an optimized processing path in the IC]
            if (this._allowRPCBatching && Messages.canUseRPCBatch(this._pendingMessages))
            {
                // Rather than pre-pending the RPCBatch header to _pendingMessages, we just push it directly [thereby avoiding a potentially costly memory copy]
                let rpcBatchHeader: Uint8Array = Messages.makeRPCBatchMessageHeader(messageCount, this._queuedByteCount);
                let readableLength: number = this.readableLength; // The number of bytes in the queue ready to be read
                let wasBuffered: Boolean = !this.push(rpcBatchHeader); // If this returns false, it will just buffer
                let headerBytesPushed: number = wasBuffered ? (this.readableLength - readableLength) : rpcBatchHeader.length;
                if (headerBytesPushed !== rpcBatchHeader.length)
                {
                    throw new Error(`Only ${headerBytesPushed} of ${rpcBatchHeader.length} RPCBatch-header bytes were pushed`);
                }            
                if (Utils.canLog(logLevel))
                {
                    Utils.log(`${messageCount} RPC messages batched: Pushed ${headerBytesPushed} RPCBatch-header bytes ${wasBuffered ? "[buffered] " : ""}`, null, logLevel);
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
            let logLevel: Utils.LoggingLevel = warning ? Utils.LoggingLevel.Normal : Utils.LoggingLevel.Verbose;
            if (Utils.canLog(logLevel))
            {
                Utils.log(`Pushed ${bytesPushed} bytes ${warning}`, this._loggingPrefix, logLevel);
            }
            this._pendingMessages = [];
            this._queuedByteCount = 0;
            // We use setImmediate() here to queue the "flushed" event after any pending I/O events
            setImmediate(() => this.emit(QUEUE_FLUSHED_EVENT)); // Note: emit() synchronously calls each of the listeners
        }
        else
        {
            throw new Error(`Only ${bytesPushed} of ${bytes.length} bytes were pushed, so the pending message queue could not be cleared`);
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
    private _onFinished: (finalByteCount: number) => void = null;
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
        catch (error)
        {
            callback(error); // Signal [to the caller of 'write'] that the write failed
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

    for (let i = 0 ; i < iterations; i++)
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
    if ((Configuration.loadedConfig().lbOptions.outputLoggingLevel !== Utils.LoggingLevel.Verbose) ||
        ((Configuration.loadedConfig().lbOptions.outputLogDestination & Configuration.OutputLogDestination.Console) !== Configuration.OutputLogDestination.Console))
    {
        Utils.log("Incorrect configuration for test: 'outputLoggingLevel' must be 'Verbose', and 'outputLogDestination' must include 'Console'");
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
            case "f": // Flush test (aka. EOD test)
                inStream.addBytes(StringEncoding.toUTF8Bytes("Hello"));
                inStream.addBytes(StringEncoding.toUTF8Bytes("World"));
                inStream.addStreamedBytes(File.createReadStream("./lib/Demo.js")); // This will have to wait for a flush because there will be 2 items in the queue
                break;
            default:
                message = `Key pressed! ('${char}')`; 
                inStream.addBytes(StringEncoding.toUTF8Bytes(message));
            }
    });
}

/** [For testing only] A writable stream that reports (to the console) how much data it was sent. Supports our [unused] experimental "EOD" detection. */
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
            let emitEndOfDataMarkerDetected: boolean = false;

            if (encoding !== "buffer")
            {
                throw new Error(`his stream can only write Buffer/Uint8Array data, not '${encoding}' data`);
            }

            // The end-of-data (EOD) bytes are our custom mechanism used to detemine if data has been flushed.
            // Note: 1) This check assumes that the EOD marker will not be split across 2 (or more) chunks.
            //       2) This check will result in a false-positive if any non EOD-marker chunk ends with the EOD marker bytes.
            //       However, since the chunks received by this writable always seem to match the size of the chunks pushed by the 
            //       readable, and since we control how the pushed data is composed, neither of these 2 conditions ever happens.
            if (this.endsWithEODMarker(chunk))
            {
                // CRITICAL! DO NOT write the end-of-data marker bytes! If we did it would corrupt the stream wire format  
                chunk = chunk.slice(0, -END_OF_DATA_MARKER.length);
                emitEndOfDataMarkerDetected = true;
            }

            if (chunk.length > 0) // May be 0 if all the chunk contained was the EOD marker
            {
                // Write the data to its destination (in our case, to the console)
                let displayBytes: string = (chunk.length <= this._maxBytesToDisplay) ? Utils.makeDisplayBytes(chunk) : "...";
                Utils.log(`Writing ${chunk.length} bytes (${displayBytes})`, this._loggingPrefix);
            }
            callback(null); // Signal [to the caller of 'write'] that the write completed

            if (emitEndOfDataMarkerDetected)
            {
                this.emit(EOD_MARKER_DETECTED_EVENT); // Note: emit() synchronously calls each of the listeners
            }
        }
        catch (error)
        {
            // This is the ONLY supported way to report an error [to the on("error") handler] from _write()
            callback(error);
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

    /** Experimental: Returns true if the specified data chunk ends with the END_OF_DATA_MARKER bytes. */
    private endsWithEODMarker(chunk: Uint8Array): boolean
    {
        let result: boolean = true;

        if (chunk.length >= END_OF_DATA_MARKER.length)
        {
            for (let i = 0, startPos = chunk.length - END_OF_DATA_MARKER.length; i < END_OF_DATA_MARKER.length; i++)
            {
                if (chunk[startPos + i] !== END_OF_DATA_MARKER[i])
                {
                    result = false;
                    break;
                }
            }
        }
        else
        {
            result = false;
        }
        return (result);
    }
}
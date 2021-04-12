using Ambrosia;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncLog
{
    public interface ILogWriter : IDisposable
    {
        ulong FileSize { get; }

        void Flush();

        Task FlushAsync();

        void WriteInt(int value);

        void WriteIntFixed(int value);

        void WriteLongFixed(long value);

        void Write(byte[] buffer,
                   int offset,
                   int length);

        Task WriteAsync(byte[] buffer,
                        int offset,
                        int length);
    }

    public struct LSN
    {
        public long sequenceID;
        public long epochID;
        public long logID;

        public bool LessThanOrEqualTo(LSN other)
        {
            Debug.Assert(this.logID == other.logID);

            return (this.epochID < other.epochID) ||
                (this.epochID == other.epochID && this.sequenceID <= other.sequenceID);
        }
    }

    public interface ILog
    {
        Task<LSN> AppendAsync(byte[] payload, int size, LSN[] dependencies, int numDependencies);
        void Snapshot();
        byte[] GetNext();
    }

    public interface ILogAppendClient
    {
        Task<ValueTuple<byte[], int>> BeforeEpochAsync(LSN lsn);
        Task AfterEpochAsync(LSN lsn);
        void Commit(LSN lsn);
    }

    internal class Committer
    {
        byte[] _buf;
        volatile byte[] _bufbak;
        long _maxBufSize;
        // Used in CAS. The first 31 bits are the #of writers, the next 32 bits is the buffer size, the last bit is the sealed bit
        long _status;
        const int SealedBits = 1;
        const int TailBits = 32;
        const int numWritesBits = 31;
        const long Last32Mask = 0x00000000FFFFFFFF;
        const long First32Mask = Last32Mask << 32;
        ILogWriter _logStream;
        Stream _workStream;
        ConcurrentDictionary<string, LongPair> _uncommittedWatermarks;
        ConcurrentDictionary<string, LongPair> _uncommittedWatermarksBak;
        internal ConcurrentDictionary<string, long> _trimWatermarks;
        ConcurrentDictionary<string, long> _trimWatermarksBak;
        internal const int HeaderSize = 24;  // 4 Committer ID, 8 Write ID, 8 check bytes, 4 page size
        Task _lastCommitTask;
        bool _persistLogs;
        int _committerID;
        internal long _nextWriteID;
        AmbrosiaRuntime _myAmbrosia;

        public Committer(Stream workStream,
                         bool persistLogs,
                         AmbrosiaRuntime myAmbrosia,
                         long maxBufSize = 8 * 1024 * 1024,
                         ILogReader recoveryStream = null)
        {
            _myAmbrosia = myAmbrosia;
            _persistLogs = persistLogs;
            _uncommittedWatermarksBak = new ConcurrentDictionary<string, LongPair>();
            _trimWatermarksBak = new ConcurrentDictionary<string, long>();
            if (maxBufSize <= 0)
            {
                // Recovering
                _committerID = recoveryStream.ReadIntFixed();
                _nextWriteID = recoveryStream.ReadLongFixed();
                _maxBufSize = recoveryStream.ReadIntFixed();
                _buf = new byte[_maxBufSize];
                var bufSize = recoveryStream.ReadIntFixed();
                _status = bufSize << SealedBits;
                recoveryStream.ReadAllRequiredBytes(_buf, 0, bufSize);
                _uncommittedWatermarks = _uncommittedWatermarks.AmbrosiaDeserialize(recoveryStream);
                _trimWatermarks = _trimWatermarks.AmbrosiaDeserialize(recoveryStream);
            }
            else
            {
                // starting for the first time
                _status = HeaderSize << SealedBits;
                _maxBufSize = maxBufSize;
                _buf = new byte[maxBufSize];
                _uncommittedWatermarks = new ConcurrentDictionary<string, LongPair>();
                _trimWatermarks = new ConcurrentDictionary<string, long>();
                long curTime;
                GetSystemTimePreciseAsFileTime(out curTime);
                _committerID = (int)((curTime << 33) >> 33);
                _nextWriteID = 0;
            }
            _bufbak = new byte[_maxBufSize];
            var memWriter = new MemoryStream(_buf);
            var memWriterBak = new MemoryStream(_bufbak);
            memWriter.WriteIntFixed(_committerID);
            memWriterBak.WriteIntFixed(_committerID);
            _logStream = null;
            _workStream = workStream;
        }

        internal int CommitID { get { return _committerID; } }

        internal void Serialize(ILogWriter serializeStream)
        {
            var localStatus = _status;
            var bufLength = ((localStatus >> SealedBits) & Last32Mask);
            serializeStream.WriteIntFixed(_committerID);
            serializeStream.WriteLongFixed(_nextWriteID);
            serializeStream.WriteIntFixed((int)_maxBufSize);
            serializeStream.WriteIntFixed((int)bufLength);
            serializeStream.Write(_buf, 0, (int)bufLength);
            _uncommittedWatermarks.AmbrosiaSerialize(serializeStream);
            _trimWatermarks.AmbrosiaSerialize(serializeStream);
        }

        public byte[] Buf { get { return _buf; } }


        private void SendInputWatermarks(ConcurrentDictionary<string, LongPair> uncommittedWatermarks,
                                         ConcurrentDictionary<string, OutputConnectionRecord> outputs)
        {
            // trim output buffers of inputs
            lock (outputs)
            {
                foreach (var kv in uncommittedWatermarks)
                {
                    OutputConnectionRecord outputConnectionRecord;
                    if (!outputs.TryGetValue(kv.Key, out outputConnectionRecord))
                    {
                        // Set up the output record for the first time and add it to the dictionary
                        outputConnectionRecord = new OutputConnectionRecord(_myAmbrosia);
                        outputs[kv.Key] = outputConnectionRecord;
                        Trace.TraceInformation("Adding output:{0}", kv.Key);
                    }
                    // Must lock to atomically update due to race with ToControlStreamAsync
                    lock (outputConnectionRecord._remoteTrimLock)
                    {
                        outputConnectionRecord.RemoteTrim = Math.Max(kv.Value.First, outputConnectionRecord.RemoteTrim);
                        outputConnectionRecord.RemoteTrimReplayable = Math.Max(kv.Value.Second, outputConnectionRecord.RemoteTrimReplayable);
                    }
                    if (outputConnectionRecord.ControlWorkQ.IsEmpty)
                    {
                        outputConnectionRecord.ControlWorkQ.Enqueue(-2);
                    }
                }
            }
        }

        private async Task Harden(byte[] firstBufToCommit,
                                  int length1,
                                  byte[] secondBufToCommit,
                                  int length2,
                                  ConcurrentDictionary<string, LongPair> uncommittedWatermarks,
                                  ConcurrentDictionary<string, long> trimWatermarks,
                                  ConcurrentDictionary<string, OutputConnectionRecord> outputs,
                                  ConcurrentDictionary<string, InputConnectionRecord> inputs,
                                  ConcurrentDictionary<long, int> pageDependencies,
                                  long pageID)
        {
            try
            {
                _workStream.Write(firstBufToCommit, 0, 4);
                _workStream.WriteIntFixed(length1 + length2);
                _workStream.Write(firstBufToCommit, 8, 16);
                await _workStream.WriteAsync(firstBufToCommit, HeaderSize, length1 - HeaderSize);
                await _workStream.WriteAsync(secondBufToCommit, 0, length2);
                var flushtask = _workStream.FlushAsync();


                // writes to _logstream - don't want to persist logs when perf testing so this is optional parameter
                if (_persistLogs)
                {
                    _logStream.Write(firstBufToCommit, 0, 4);
                    _logStream.WriteIntFixed(length1 + length2);
                    _logStream.Write(firstBufToCommit, 8, 16);
                    await _logStream.WriteAsync(firstBufToCommit, HeaderSize, length1 - HeaderSize);
                    await _logStream.WriteAsync(secondBufToCommit, 0, length2);
                    await writeFullWaterMarksAsync(uncommittedWatermarks);
                    await writeSimpleWaterMarksAsync(trimWatermarks);
                    await _logStream.FlushAsync();
                }

                var prevDurablePage = Interlocked.Exchange(ref _myAmbrosia._durablePageID, pageID);
                Debug.Assert(prevDurablePage == pageID - 1);

                TryCommit(uncommittedWatermarks, outputs, pageDependencies, pageID);

                SendInputWatermarks(uncommittedWatermarks, outputs);
                // Return the second byte array to the FlexReader pool
                FlexReadBuffer.ReturnBuffer(secondBufToCommit);
                _uncommittedWatermarksBak = uncommittedWatermarks;
                _uncommittedWatermarksBak.Clear();
                _trimWatermarksBak = trimWatermarks;
                _trimWatermarksBak.Clear();
            }
            catch (Exception e)
            {
                _myAmbrosia.OnError(5, e.Message);
            }
            _bufbak = firstBufToCommit;
            await TryHardenAsync(outputs, inputs, pageDependencies);
        }

        private async Task writeFullWaterMarksAsync(ConcurrentDictionary<string, LongPair> uncommittedWatermarks)
        {
            _logStream.WriteInt(uncommittedWatermarks.Count);
            foreach (var kv in uncommittedWatermarks)
            {
                var sourceBytes = Encoding.UTF8.GetBytes(kv.Key);
                _logStream.WriteInt(sourceBytes.Length);
                await _logStream.WriteAsync(sourceBytes, 0, sourceBytes.Length);
                _logStream.WriteLongFixed(kv.Value.First);
                _logStream.WriteLongFixed(kv.Value.Second);
            }
        }

        private async Task writeSimpleWaterMarksAsync(ConcurrentDictionary<string, long> uncommittedWatermarks)
        {
            _logStream.WriteInt(uncommittedWatermarks.Count);
            foreach (var kv in uncommittedWatermarks)
            {
                var sourceBytes = Encoding.UTF8.GetBytes(kv.Key);
                _logStream.WriteInt(sourceBytes.Length);
                await _logStream.WriteAsync(sourceBytes, 0, sourceBytes.Length);
                _logStream.WriteLongFixed(kv.Value);
            }
        }
        private async Task Harden(byte[] buf,
                                  int length,
                                  ConcurrentDictionary<string, LongPair> uncommittedWatermarks,
                                  ConcurrentDictionary<string, long> trimWatermarks,
                                  ConcurrentDictionary<string, OutputConnectionRecord> outputs,
                                  ConcurrentDictionary<string, InputConnectionRecord> inputs,
                                  ConcurrentDictionary<long, int> pageDependencies,
                                  long pageID)
        {
            try
            {
                await _workStream.WriteAsync(buf, 0, length);
                var flushtask = _workStream.FlushAsync();

                // writes to _logstream - don't want to persist logs when perf testing so this is optional parameter
                if (_persistLogs)
                {
                    await _logStream.WriteAsync(buf, 0, length);
                    await writeFullWaterMarksAsync(uncommittedWatermarks);
                    await writeSimpleWaterMarksAsync(trimWatermarks);
                    await _logStream.FlushAsync();
                }

                // Update the durable pageID.
                var prevDurablePage = Interlocked.Exchange(ref _myAmbrosia._durablePageID, pageID);
                Debug.Assert(prevDurablePage == pageID - 1);

                TryCommit(uncommittedWatermarks, outputs, pageDependencies, pageID);

                SendInputWatermarks(uncommittedWatermarks, outputs);
                _uncommittedWatermarksBak = uncommittedWatermarks;
                _uncommittedWatermarksBak.Clear();
                _trimWatermarksBak = trimWatermarks;
                _trimWatermarksBak.Clear();
            }
            catch (Exception e)
            {
                _myAmbrosia.OnError(5, e.Message);
            }
            _bufbak = buf;
            await TryHardenAsync(outputs, inputs, pageDependencies);
        }

        private void TryCommit(ConcurrentDictionary<string, LongPair> uncommittedWatermarks,
                               ConcurrentDictionary<string, OutputConnectionRecord> outputs,
                               ConcurrentDictionary<long, int> pageDependencies,
                               long pageID)
        {
            var depCount = uncommittedWatermarks.Count;
            var hasLocalDep = uncommittedWatermarks.ContainsKey("");
            var hasRemoteDeps = !(depCount == 0 || (depCount == 1 && hasLocalDep));

            if (hasRemoteDeps)
            {
                if (hasLocalDep)
                {
                    pageDependencies[pageID] = uncommittedWatermarks.Count - 1;
                }
                else
                {
                    pageDependencies[pageID] = uncommittedWatermarks.Count;
                }

                foreach (var kv in uncommittedWatermarks)
                {
                    if (!kv.Key.Equals(""))
                    {
                        var outputConnection = outputs[kv.Key];
                        outputConnection.RecordDependency(pageID, kv.Value.First, outputs, pageDependencies);
                    }
                }
            }
            else
            {
                // The page has no remote dependencies, try to commit it.
                var lastCommittedPage = Interlocked.Read(ref _myAmbrosia._committedPageID);
                var startPage = lastCommittedPage;

                // Try to commit as many pages as possible, up until the current durablePageID. 
                // Two cases in which we break out of this loop:
                // 1) A page with a lower pageID has yet to commit, in which case we can't commit the current page.
                // 2) We raced and lost with another thread that's committing pages. Give up and let that thread continue.
                while (!pageDependencies.ContainsKey(lastCommittedPage + 1) && lastCommittedPage + 1 <= pageID)
                {
                    var cmpXchgOut = Interlocked.CompareExchange(ref _myAmbrosia._committedPageID, lastCommittedPage + 1, lastCommittedPage);
                    if (cmpXchgOut != lastCommittedPage)
                    {
                        break;
                    }
                    else
                    {
                        lastCommittedPage += 1;
                    }
                }

                if (startPage != lastCommittedPage)
                {
                    foreach (var kv in outputs)
                    {
                        if (kv.Value.ControlWorkQ.IsEmpty)
                        {
                            kv.Value.ControlWorkQ.Enqueue(-2);
                        }
                    }
                }
            }
        }

        public async Task SleepAsync()
        {
            while (true)
            {
                // We're going to try to seal the buffer
                var localStatus = Interlocked.Read(ref _status);
                // Yield if the sealed bit is set
                while (localStatus % 2 == 1)
                {
                    await Task.Yield();
                    localStatus = Interlocked.Read(ref _status);
                }
                var newLocalStatus = localStatus + 1;
                var origVal = Interlocked.CompareExchange(ref _status, newLocalStatus, localStatus);

                // Check if the compare and swap succeeded, otherwise try again
                if (origVal == localStatus)
                {
                    // We successfully sealed the buffer and must wait until any active commit finishes
                    while (_bufbak == null)
                    {
                        await Task.Yield();
                    }

                    // Wait for all writes to complete before sleeping
                    while (true)
                    {
                        localStatus = Interlocked.Read(ref _status);
                        var numWrites = (localStatus >> (64 - numWritesBits));
                        if (numWrites == 0)
                        {
                            break;
                        }
                        await Task.Yield();
                    }
                    return;
                }
            }
        }

        // This method switches the log stream to the provided stream and removes the write lock on the old file
        public void SwitchLogStreams(ILogWriter newLogStream)
        {
            if (_status % 2 != 1 || _bufbak == null)
            {
                _myAmbrosia.OnError(5, "Committer is trying to switch log streams when awake");
            }
            // Release resources and lock on the old file
            if (_logStream != null)
            {
                _logStream.Dispose();
            }
            _logStream = newLogStream;
        }

        public async Task WakeupAsync()
        {
            var localStatus = Interlocked.Read(ref _status);
            if (localStatus % 2 == 0 || _bufbak == null)
            {
                _myAmbrosia.OnError(5, "Tried to wakeup committer when not asleep");
            }
            // We're going to try to unseal the buffer
            var newLocalStatus = localStatus - 1;
            var origVal = Interlocked.CompareExchange(ref _status, newLocalStatus, localStatus);
            // Check if the compare and swap succeeded
            if (origVal != localStatus)
            {
                _myAmbrosia.OnError(5, "Tried to wakeup committer when not asleep 2");
            }
        }

        byte[] _checkTempBytes = new byte[8];
        byte[] _checkTempBytes2 = new byte[8];

        internal unsafe long CheckBytesExtra(int offset,
                                             int length,
                                             byte[] extraBytes,
                                             int extraLength)
        {
            var firstBufferCheck = CheckBytes(offset, length);
            var secondBufferCheck = CheckBytes(extraBytes, 0, extraLength);
            long shiftedSecondBuffer = secondBufferCheck;
            var lastByteLongOffset = length % 8;
            if (lastByteLongOffset != 0)
            {
                fixed (byte* p = _checkTempBytes)
                {
                    *((long*)p) = secondBufferCheck;
                }
                // Create new buffer with circularly shifted secondBufferCheck
                for (int i = 0; i < 8; i++)
                {
                    _checkTempBytes2[i] = _checkTempBytes[(i - lastByteLongOffset + 8) % 8];
                }
                fixed (byte* p = _checkTempBytes2)
                {
                    shiftedSecondBuffer = *((long*)p);
                }
            }
            return firstBufferCheck ^ shiftedSecondBuffer;
        }

        internal unsafe long CheckBytes(int offset,
                                        int length)
        {
            long checkBytes = 0;

            fixed (byte* p = _buf)
            {
                if (offset % 8 == 0)
                {
                    int startLongCalc = offset / 8;
                    int numLongCalcs = length / 8;
                    int numByteCalcs = length % 8;
                    long* longPtr = ((long*)p) + startLongCalc;
                    for (int i = 0; i < numLongCalcs; i++)
                    {
                        checkBytes ^= longPtr[i];
                    }
                    if (numByteCalcs != 0)
                    {
                        var lastBytes = (byte*)(longPtr + numLongCalcs);
                        for (int i = 0; i < 8; i++)
                        {
                            if (i < numByteCalcs)
                            {
                                _checkTempBytes[i] = lastBytes[i];
                            }
                            else
                            {
                                _checkTempBytes[i] = 0;
                            }
                        }
                        fixed (byte* p2 = _checkTempBytes)
                        {
                            checkBytes ^= *((long*)p2);
                        }
                    }
                }
                else
                {
                    _myAmbrosia.OnError(0, "checkbytes case not implemented");
                }
            }
            return checkBytes;
        }


        internal unsafe long CheckBytes(byte[] bufToCalc,
                                        int offset,
                                        int length)
        {
            long checkBytes = 0;

            fixed (byte* p = bufToCalc)
            {
                if (offset % 8 == 0)
                {
                    int startLongCalc = offset / 8;
                    int numLongCalcs = length / 8;
                    int numByteCalcs = length % 8;
                    long* longPtr = ((long*)p) + startLongCalc;
                    for (int i = 0; i < numLongCalcs; i++)
                    {
                        checkBytes ^= longPtr[i];
                    }
                    if (numByteCalcs != 0)
                    {
                        var lastBytes = (byte*)(longPtr + numLongCalcs);
                        for (int i = 0; i < 8; i++)
                        {
                            if (i < numByteCalcs)
                            {
                                _checkTempBytes[i] = lastBytes[i];
                            }
                            else
                            {
                                _checkTempBytes[i] = 0;
                            }
                        }
                        fixed (byte* p2 = _checkTempBytes)
                        {
                            checkBytes ^= *((long*)p2);
                        }
                    }
                }
                else
                {
                    _myAmbrosia.OnError(0, "checkbytes case not implemented 2");
                }
            }
            return checkBytes;
        }


        public async Task<long> AddRow(FlexReadBuffer copyFromFlexBuffer,
                                       string outputToUpdate,
                                       long newSeqNo,
                                       long newReplayableSeqNo,
                                       ConcurrentDictionary<string, OutputConnectionRecord> outputs,
                                       ConcurrentDictionary<string, InputConnectionRecord> inputs,
                                       ConcurrentDictionary<long, int> pageDependencies)
        {
            var copyFromBuffer = copyFromFlexBuffer.Buffer;
            var length = copyFromFlexBuffer.Length;
            while (true)
            {
                bool sealing = false;
                long localStatus;
                localStatus = Interlocked.Read(ref _status);

                // Yield if the sealed bit is set
                while (localStatus % 2 == 1)
                {
                    await Task.Yield();
                    localStatus = Interlocked.Read(ref _status);
                }
                var oldBufLength = ((localStatus >> SealedBits) & Last32Mask);
                var newLength = oldBufLength + length;

                // Assemble the new status 
                long newLocalStatus;
                if ((newLength > _maxBufSize) || (_bufbak != null))
                {
                    // We're going to try to seal the buffer
                    newLocalStatus = localStatus + 1;
                    sealing = true;
                }
                else
                {
                    // We're going to try to add to the end of the existing buffer
                    var newWrites = (localStatus >> (64 - numWritesBits)) + 1;
                    newLocalStatus = ((newWrites) << (64 - numWritesBits)) | (newLength << SealedBits);
                }
                var origVal = Interlocked.CompareExchange(ref _status, newLocalStatus, localStatus);

                // Check if the compare and swap succeeded, otherwise try again
                if (origVal == localStatus)
                {
                    // We are now preventing recovery until addrow finishes and all resulting commits have completed. We can safely update
                    // LastProcessedID and LastProcessedReplayableID
                    var associatedInputConnectionRecord = inputs[outputToUpdate];
                    associatedInputConnectionRecord.LastProcessedID = newSeqNo;
                    associatedInputConnectionRecord.LastProcessedReplayableID = newReplayableSeqNo;
                    if (sealing)
                    {
                        // This call successfully sealed the buffer. Remember we still have an extra
                        // message to take care of

                        // We have just filled the backup buffer and must wait until any other commit finishes
                        int counter = 0;
                        while (_bufbak == null)
                        {
                            counter++;
                            if (counter == 100000)
                            {
                                counter = 0;
                                await Task.Yield();
                            }
                        }

                        // There is no other write going on. Take the backup buffer
                        var newUncommittedWatermarks = _uncommittedWatermarksBak;
                        var newWriteBuf = _bufbak;
                        _bufbak = null;
                        _uncommittedWatermarksBak = null;

                        // Wait for other writes to complete before committing
                        while (true)
                        {
                            localStatus = Interlocked.Read(ref _status);
                            var numWrites = (localStatus >> (64 - numWritesBits));
                            if (numWrites == 0)
                            {
                                break;
                            }
                            await Task.Yield();
                        }

                        // Filling header with enough info to detect incomplete writes and also writing the page length
                        var writeStream = new MemoryStream(_buf, 4, 20);
                        int lengthOnPage;
                        if (newLength <= _maxBufSize)
                        {
                            lengthOnPage = (int)newLength;
                        }
                        else
                        {
                            lengthOnPage = (int)oldBufLength;
                        }
                        writeStream.WriteIntFixed(lengthOnPage);
                        if (newLength <= _maxBufSize)
                        {
                            // Copy the contents into the log record buffer
                            Buffer.BlockCopy(copyFromBuffer, 0, _buf, (int)oldBufLength, length);
                        }
                        long checkBytes;
                        if (length <= (_maxBufSize - HeaderSize))
                        {
                            // new message will end up in a commit buffer. Use normal CheckBytes
                            checkBytes = CheckBytes(HeaderSize, lengthOnPage - HeaderSize);
                        }
                        else
                        {
                            // new message is too big to land in a commit buffer and will be tacked on the end.
                            checkBytes = CheckBytesExtra(HeaderSize, lengthOnPage - HeaderSize, copyFromBuffer, length);
                        }
                        writeStream.WriteLongFixed(checkBytes);
                        writeStream.WriteLongFixed(_nextWriteID);
                        var pageID = _nextWriteID;
                        _nextWriteID++;

                        // Do the actual commit
                        // Grab the current state of trim levels since the last write
                        // Note that the trim thread may want to modify the table, requiring a lock
                        ConcurrentDictionary<string, long> oldTrimWatermarks;
                        lock (_trimWatermarks)
                        {
                            oldTrimWatermarks = _trimWatermarks;
                            _trimWatermarks = _trimWatermarksBak;
                            _trimWatermarksBak = null;
                        }
                        if (newLength <= _maxBufSize)
                        {
                            // add row to current buffer and commit
                            _uncommittedWatermarks[outputToUpdate] = new LongPair(newSeqNo, newReplayableSeqNo);
                            _lastCommitTask = Harden(_buf, (int)newLength, _uncommittedWatermarks, oldTrimWatermarks, outputs, inputs, pageDependencies, pageID);
                            newLocalStatus = HeaderSize << SealedBits;
                        }
                        else if (length > (_maxBufSize - HeaderSize))
                        {
                            // Steal the byte array in the flex buffer to return it after writing
                            copyFromFlexBuffer.StealBuffer();
                            // write new event as part of commit
                            _uncommittedWatermarks[outputToUpdate] = new LongPair(newSeqNo, newReplayableSeqNo);
                            var commitTask = Harden(_buf, (int)oldBufLength, copyFromBuffer, length, _uncommittedWatermarks, oldTrimWatermarks, outputs, inputs, pageDependencies, pageID);
                            newLocalStatus = HeaderSize << SealedBits;
                        }
                        else
                        {
                            // commit and add new event to new buffer
                            newUncommittedWatermarks[outputToUpdate] = new LongPair(newSeqNo, newReplayableSeqNo);
                            _lastCommitTask = Harden(_buf, (int)oldBufLength, _uncommittedWatermarks, oldTrimWatermarks, outputs, inputs, pageDependencies, pageID);
                            Buffer.BlockCopy(copyFromBuffer, 0, newWriteBuf, (int)HeaderSize, length);
                            newLocalStatus = (HeaderSize + length) << SealedBits;
                        }
                        _buf = newWriteBuf;
                        _uncommittedWatermarks = newUncommittedWatermarks;
                        _status = newLocalStatus;
                        return (long)_logStream.FileSize;
                    }
                    // Add the message to the existing buffer
                    Buffer.BlockCopy(copyFromBuffer, 0, _buf, (int)oldBufLength, length);
                    _uncommittedWatermarks[outputToUpdate] = new LongPair(newSeqNo, newReplayableSeqNo);
                    // Reduce write count
                    while (true)
                    {
                        localStatus = Interlocked.Read(ref _status);
                        var newWrites = (localStatus >> (64 - numWritesBits)) - 1;
                        newLocalStatus = (localStatus & ((Last32Mask << 1) + 1)) |
                                      (newWrites << (64 - numWritesBits));
                        origVal = Interlocked.CompareExchange(ref _status, newLocalStatus, localStatus);
                        if (origVal == localStatus)
                        {
                            if (localStatus % 2 == 0 && _bufbak != null)
                            {
                                await TryHardenAsync(outputs, inputs, pageDependencies);
                            }
                            return (long)_logStream.FileSize;
                        }
                    }
                }
            }
        }

        public async Task TryHardenAsync(ConcurrentDictionary<string, OutputConnectionRecord> outputs,
                                         ConcurrentDictionary<string, InputConnectionRecord> inputs,
                                         ConcurrentDictionary<long, int> pageDependencies)
        {
            long localStatus;
            localStatus = Interlocked.Read(ref _status);

            var bufLength = ((localStatus >> SealedBits) & Last32Mask);
            // give up and try later if the sealed bit is set or there is nothing to write
            if (localStatus % 2 == 1 || bufLength == HeaderSize || _bufbak == null)
            {
                return;
            }

            // Assemble the new status 
            long newLocalStatus;
            newLocalStatus = localStatus + 1;
            var origVal = Interlocked.CompareExchange(ref _status, newLocalStatus, localStatus);

            // Check if the compare and swap succeeded, otherwise skip flush
            if (origVal == localStatus)
            {
                // This call successfully sealed the buffer.

                // We have just filled the backup buffer and must wait until any other commit finishes
                int counter = 0;
                while (_bufbak == null)
                {
                    counter++;
                    if (counter == 100000)
                    {
                        counter = 0;
                        await Task.Yield();
                    }
                }

                // There is no other write going on. Take the backup buffer
                var newUncommittedWatermarks = _uncommittedWatermarksBak;
                var newWriteBuf = _bufbak;
                _bufbak = null;
                _uncommittedWatermarksBak = null;

                // Wait for other writes to complete before committing
                while (true)
                {
                    localStatus = Interlocked.Read(ref _status);
                    var numWrites = (localStatus >> (64 - numWritesBits));
                    if (numWrites == 0)
                    {
                        break;
                    }
                    await Task.Yield();
                }

                // Filling header with enough info to detect incomplete writes and also writing the page length
                var writeStream = new MemoryStream(_buf, 4, 20);
                writeStream.WriteIntFixed((int)bufLength);
                long checkBytes = CheckBytes(HeaderSize, (int)bufLength - HeaderSize);
                writeStream.WriteLongFixed(checkBytes);
                writeStream.WriteLongFixed(_nextWriteID);
                var pageID = _nextWriteID;
                _nextWriteID++;

                // Grab the current state of trim levels since the last write
                // Note that the trim thread may want to modify the table, requiring a lock
                ConcurrentDictionary<string, long> oldTrimWatermarks;
                lock (_trimWatermarks)
                {
                    oldTrimWatermarks = _trimWatermarks;
                    _trimWatermarks = _trimWatermarksBak;
                    _trimWatermarksBak = null;
                }
                _lastCommitTask = Harden(_buf, (int)bufLength, _uncommittedWatermarks, oldTrimWatermarks, outputs, inputs, pageDependencies, pageID);
                newLocalStatus = HeaderSize << SealedBits;
                _buf = newWriteBuf;
                _uncommittedWatermarks = newUncommittedWatermarks;
                _status = newLocalStatus;
            }
        }

        internal void ClearNextWrite()
        {
            _uncommittedWatermarksBak.Clear();
            _trimWatermarksBak.Clear();
            _status = HeaderSize << SealedBits;
        }

        internal void SendUpgradeRequest()
        {
            _workStream.WriteIntFixed(_committerID);
            var numMessageBytes = StreamCommunicator.IntSize(1) + 1;
            var messageBuf = new byte[numMessageBytes];
            var memStream = new MemoryStream(messageBuf);
            memStream.WriteInt(1);
            memStream.WriteByte(upgradeServiceByte);
            memStream.Dispose();
            _workStream.WriteIntFixed((int)(HeaderSize + numMessageBytes));
            long checkBytes = CheckBytes(messageBuf, 0, (int)numMessageBytes);
            _workStream.WriteLongFixed(checkBytes);
            _workStream.WriteLongFixed(-1);
            _workStream.Write(messageBuf, 0, numMessageBytes);
            _workStream.Flush();
        }

        internal void QuiesceServiceWithSendCheckpointRequest(bool upgrading = false, bool becomingPrimary = false)
        {
            _workStream.WriteIntFixed(_committerID);
            var numMessageBytes = StreamCommunicator.IntSize(1) + 1;
            var messageBuf = new byte[numMessageBytes];
            var memStream = new MemoryStream(messageBuf);
            memStream.WriteInt(1);
#if DEBUG
            // We are about to request a checkpoint from the language binding. Get ready to error check the incoming checkpoint
            _myAmbrosia.ExpectingCheckpoint = true;
#endif
            if (upgrading)
            {
                memStream.WriteByte(upgradeTakeCheckpointByte);
            }
            else if (becomingPrimary)
            {
                memStream.WriteByte(takeBecomingPrimaryCheckpointByte);
            }
            else
            {
                memStream.WriteByte(takeCheckpointByte);
            }
            memStream.Dispose();
            _workStream.WriteIntFixed((int)(HeaderSize + numMessageBytes));
            long checkBytes = CheckBytes(messageBuf, 0, (int)numMessageBytes);
            _workStream.WriteLongFixed(checkBytes);
            _workStream.WriteLongFixed(-1);
            _workStream.Write(messageBuf, 0, numMessageBytes);
            _workStream.Flush();
        }

        internal void SendBecomePrimaryRequest()
        {
            _workStream.WriteIntFixed(_committerID);
            var numMessageBytes = StreamCommunicator.IntSize(1) + 1;
            var messageBuf = new byte[numMessageBytes];
            var memStream = new MemoryStream(messageBuf);
            memStream.WriteInt(1);
            memStream.WriteByte(becomingPrimaryByte);
            memStream.Dispose();
            _workStream.WriteIntFixed((int)(HeaderSize + numMessageBytes));
            long checkBytes = CheckBytes(messageBuf, 0, (int)numMessageBytes);
            _workStream.WriteLongFixed(checkBytes);
            _workStream.WriteLongFixed(-1);
            _workStream.Write(messageBuf, 0, numMessageBytes);
            _workStream.Flush();
        }


        internal void SendCheckpointToRecoverFrom(byte[] buf, int length, ILogReader checkpointStream)
        {
            _workStream.WriteIntFixed(_committerID);
            _workStream.WriteIntFixed((int)(HeaderSize + length));
            _workStream.WriteLongFixed(0);
            _workStream.WriteLongFixed(-2);
            _workStream.Write(buf, 0, length);
            var sizeBytes = StreamCommunicator.ReadBufferedInt(buf, 0);
            var checkpointSize = StreamCommunicator.ReadBufferedLong(buf, StreamCommunicator.IntSize(sizeBytes) + 1);
            checkpointStream.ReadBig(_workStream, checkpointSize);
            _workStream.Flush();
        }

        internal async Task AddInitialRowAsync(FlexReadBuffer serviceInitializationMessage)
        {
            var numMessageBytes = serviceInitializationMessage.Length;
            if (numMessageBytes > _buf.Length - HeaderSize)
            {
                _myAmbrosia.OnError(0, "Initial row is too many bytes");
            }
            Buffer.BlockCopy(serviceInitializationMessage.Buffer, 0, _buf, (int)HeaderSize, numMessageBytes);
            _status = (HeaderSize + numMessageBytes) << SealedBits;
            await SleepAsync();
        }
    }

    public class AmbrosiaLog : ILog
    {
        private ILogAppendClient _appendClient;
        private Task _epochDurabilityTask;

        private ILogWriter _logStream;
        private long _logID;
        private long _lastEpochSequenceID;
        private long _epochID;
        private long _durablePageID;

        private long _completedWrites;

        private Task _lastCommitTask;

        private bool _persistLogs;

        byte[] _buf;
        volatile byte[] _bufbak;
        long _maxBufSize;
        internal const int HeaderSize = 40;  // 4 Committer ID, 8 Epoch ID, 8 check bytes, 4 page size, 8 min LSN, 8 max LSN

        byte[] _checkTempBytes = new byte[8];
        byte[] _checkTempBytes2 = new byte[8];

        ConcurrentBag<Stream> _subscriberStreams;

        // Used in CAS. The first 31 bits are the #of writers, the next 32 bits is the buffer size, the last bit is the sealed bit
        long _status;
        const int SealedBits = 1;
        const int TailBits = 32;
        const int numWritesBits = 31;
        const long Last32Mask = 0x00000000FFFFFFFF;
        const long First32Mask = Last32Mask << 32;

        ConcurrentDictionary<long, LSN> _pageDeps;
        ConcurrentDictionary<long, LSN> _pageDepsBak;

        internal unsafe long CheckBytes(byte[] bufToCalc,
                                        int offset,
                                        int length)
        {
            long checkBytes = 0;

            fixed (byte* p = bufToCalc)
            {
                if (offset % 8 == 0)
                {
                    int startLongCalc = offset / 8;
                    int numLongCalcs = length / 8;
                    int numByteCalcs = length % 8;
                    long* longPtr = ((long*)p) + startLongCalc;
                    for (int i = 0; i < numLongCalcs; i++)
                    {
                        checkBytes ^= longPtr[i];
                    }
                    if (numByteCalcs != 0)
                    {
                        var lastBytes = (byte*)(longPtr + numLongCalcs);
                        for (int i = 0; i < 8; i++)
                        {
                            if (i < numByteCalcs)
                            {
                                _checkTempBytes[i] = lastBytes[i];
                            }
                            else
                            {
                                _checkTempBytes[i] = 0;
                            }
                        }
                        fixed (byte* p2 = _checkTempBytes)
                        {
                            checkBytes ^= *((long*)p2);
                        }
                    }
                }
                else
                {
                    // XXX Jose: This is a hack. Fix it for real.
                    Trace.Assert(false);
                    // _myAmbrosia.OnError(0, "checkbytes case not implemented 2");
                }
            }
            return checkBytes;
        }

        internal unsafe long CheckBytes(int offset,
                                int length)
        {
            long checkBytes = 0;

            fixed (byte* p = _buf)
            {
                if (offset % 8 == 0)
                {
                    int startLongCalc = offset / 8;
                    int numLongCalcs = length / 8;
                    int numByteCalcs = length % 8;
                    long* longPtr = ((long*)p) + startLongCalc;
                    for (int i = 0; i < numLongCalcs; i++)
                    {
                        checkBytes ^= longPtr[i];
                    }
                    if (numByteCalcs != 0)
                    {
                        var lastBytes = (byte*)(longPtr + numLongCalcs);
                        for (int i = 0; i < 8; i++)
                        {
                            if (i < numByteCalcs)
                            {
                                _checkTempBytes[i] = lastBytes[i];
                            }
                            else
                            {
                                _checkTempBytes[i] = 0;
                            }
                        }
                        fixed (byte* p2 = _checkTempBytes)
                        {
                            checkBytes ^= *((long*)p2);
                        }
                    }
                }
                else
                {
                    _myAmbrosia.OnError(0, "checkbytes case not implemented");
                }
            }
            return checkBytes;
        }

        internal unsafe long CheckBytesExtra(int offset,
                                             int length,
                                             byte[] extraBytes,
                                             int extraLength)
        {
            var firstBufferCheck = CheckBytes(offset, length);
            var secondBufferCheck = CheckBytes(extraBytes, 0, extraLength);
            long shiftedSecondBuffer = secondBufferCheck;
            var lastByteLongOffset = length % 8;
            if (lastByteLongOffset != 0)
            {
                fixed (byte* p = _checkTempBytes)
                {
                    *((long*)p) = secondBufferCheck;
                }
                // Create new buffer with circularly shifted secondBufferCheck
                for (int i = 0; i < 8; i++)
                {
                    _checkTempBytes2[i] = _checkTempBytes[(i - lastByteLongOffset + 8) % 8];
                }
                fixed (byte* p = _checkTempBytes2)
                {
                    shiftedSecondBuffer = *((long*)p);
                }
            }
            return firstBufferCheck ^ shiftedSecondBuffer;
        }

        public async Task<LSN> AppendAsync(byte[] payload, int length, LSN[] dependencies, int num_dependencies)
        {
            while (true)
            {
                bool sealing = false;
                long localStatus;
                localStatus = Interlocked.Read(ref _status);

                // Yield if the sealed bit is set
                while (localStatus % 2 == 1)
                {
                    await Task.Yield();
                    localStatus = Interlocked.Read(ref _status);
                }
                var oldBufLength = ((localStatus >> SealedBits) & Last32Mask);
                var newLength = oldBufLength + length;

                // Assemble the new status 
                long newLocalStatus;
                if ((newLength > _maxBufSize) || (_bufbak != null))
                {
                    // We're going to try to seal the buffer
                    newLocalStatus = localStatus + 1;
                    sealing = true;
                }
                else
                {
                    // We're going to try to add to the end of the existing buffer
                    var newWriteCount = (localStatus >> (64 - numWritesBits)) + 1;
                    newLocalStatus = ((newWriteCount) << (64 - numWritesBits)) | (newLength << SealedBits);
                }
                var origVal = Interlocked.CompareExchange(ref _status, newLocalStatus, localStatus);

                // Check if the compare and swap succeeded, otherwise try again
                if (origVal == localStatus)
                {
                    LSN retLSN;

                    retLSN.logID = _logID;
                    retLSN.epochID = _epochID;

                    // Keep track of specified dependencies.
                    for (int i = 0; i < num_dependencies; ++i)
                    {
                        // We're only accounting for dependencies on other logs. 
                        // Local dependencies are implicitly handled via log ordering.
                        Debug.Assert(dependencies[i].logID != _logID);

                        if (!_pageDeps.TryAdd(dependencies[i].logID, dependencies[i]))
                        {
                            LSN currentDep = _pageDeps[dependencies[i].logID];
                            while (!dependencies[i].LessThanOrEqualTo(currentDep))
                            {
                                if (_pageDeps.TryUpdate(dependencies[i].logID, dependencies[i], currentDep))
                                {
                                    break;
                                }
                                currentDep = _pageDeps[dependencies[i].logID];
                            }
                        }
                    }

                    // This call successfully sealed the buffer. Remember we still have an extra
                    // message to take care of
                    if (sealing)
                    {
                        // We have just filled the backup buffer and must wait until any other commit finishes
                        int counter = 0;
                        while (_bufbak == null)
                        {
                            counter++;
                            if (counter == 100000)
                            {
                                counter = 0;
                                await Task.Yield();
                            }
                        }

                        // There is no other write going on. Take the backup buffer
                        var newWriteBuf = _bufbak;
                        _bufbak = null;

                        // Wait for other writes to complete before committing
                        var numWrites = (newLocalStatus >> (64 - numWritesBits));
                        while (true)
                        {
                            var completedWrites = Interlocked.Read(ref _completedWrites);
                            if (completedWrites != numWrites)
                            {
                                await Task.Yield();
                            }
                            else
                            {
                                break;
                            }
                        }

                        // Filling header with enough info to detect incomplete writes and also writing the page length
                        var writeStream = new MemoryStream(_buf, 4, 36);
                        int lengthOnPage;
                        if (newLength <= _maxBufSize)
                        {
                            lengthOnPage = (int)newLength;
                        }
                        else
                        {
                            lengthOnPage = (int)oldBufLength;
                        }
                        writeStream.WriteIntFixed(lengthOnPage);

                        if (newLength <= _maxBufSize)
                        {
                            // Copy the contents into the log record buffer
                            Buffer.BlockCopy(payload, 0, _buf, (int)oldBufLength, length);
                        }
                        long checkBytes;
                        if (length <= (_maxBufSize - HeaderSize))
                        {
                            // new message will end up in a commit buffer. Use normal CheckBytes
                            checkBytes = CheckBytes(HeaderSize, lengthOnPage - HeaderSize);
                        }
                        else
                        {
                            // new message is too big to land in a commit buffer and will be tacked on the end.
                            checkBytes = CheckBytesExtra(HeaderSize, lengthOnPage - HeaderSize, payload, length);
                        }
                        writeStream.WriteLongFixed(checkBytes);
                        writeStream.WriteLongFixed(_epochID);
                        writeStream.WriteLongFixed(_lastEpochSequenceID + 1);
                        writeStream.WriteLongFixed(_lastEpochSequenceID + (newLocalStatus >> (64 - numWritesBits)) + 1);

                        // Do the actual commit
                        if (newLength <= _maxBufSize)
                        {
                            // add row to current buffer and commit
                            _lastCommitTask = HardenAsync(_buf, (int)newLength);
                            newLocalStatus = HeaderSize << SealedBits;
                        }
                        else if (length > (_maxBufSize - HeaderSize))
                        {
                            // XXX Jose: Read up on FlexBuffer to figure out how to deal with the steal below.
                            // Steal the byte array in the flex buffer to return it after writing
                            // copyFromFlexBuffer.StealBuffer();
                            var commitTask = HardenAsync(_buf, (int)oldBufLength, payload, length);
                            newLocalStatus = HeaderSize << SealedBits;
                        }
                        else
                        {
                            // commit and add new event to new buffer
                            _lastCommitTask = HardenAsync(_buf, (int)oldBufLength);
                            Buffer.BlockCopy(payload, 0, newWriteBuf, (int)HeaderSize, length);
                            newLocalStatus = (HeaderSize + length) << SealedBits;
                        }
                        _buf = newWriteBuf;
                        _status = newLocalStatus;

                        retLSN.sequenceID = _lastEpochSequenceID + (newLocalStatus >> (64 - numWritesBits)) + 1;
                        return retLSN;
                    }

                    // Add the message to the existing buffer
                    Buffer.BlockCopy(payload, 0, _buf, (int)oldBufLength, length);
                    // Update completed write count.
                    Interlocked.Increment(ref _completedWrites);
                    localStatus = Interlocked.Read(ref _status);
                    if (localStatus % 2 == 0 && _bufbak != null)
                    {
                        await TryHardenAsync();
                    }

                    retLSN.sequenceID = _lastEpochSequenceID + (newLocalStatus >> (64 - numWritesBits));
                    return retLSN;
                }
            }
        }

        private async Task HardenAsync(byte[] buf, int length)
        {
            LSN lastLSN;
            lastLSN.sequenceID = _completedWrites + 1 + _lastEpochSequenceID;
            lastLSN.epochID = _epochID;
            lastLSN.logID = _logID;

            try
            {
                // Get the client's footer to associate with the epoch.
                var clientFooter = await _appendClient.BeforeEpochAsync(lastLSN);

                foreach (var subscriber in _subscriberStreams)
                {
                    await subscriber.WriteAsync(buf, 0, length);
                    var flushtask = subscriber.FlushAsync();
                }

                // writes to _logstream - don't want to persist logs when perf testing so this is optional parameter
                if (_persistLogs)
                {
                    await _logStream.WriteAsync(buf, 0, length);
                    await _logStream.FlushAsync();
                }

                // Signal completion to the client.
                _epochDurabilityTask = _appendClient.AfterEpochAsync(lastLSN);

                // Update the durable pageID.
                Interlocked.Increment(ref _durablePageID);

                // XXX Jose: Need to handle this below!
                // TryCommit(uncommittedWatermarks, outputs, pageDependencies, pageID);
            }
            catch (Exception e)
            {
                throw e;
            }

            // Jose: The Interlocked statements below might be overkill.
            _lastEpochSequenceID = lastLSN.sequenceID;
            var newEpoch = Interlocked.Increment(ref _epochID);
            Debug.Assert(newEpoch == lastLSN.epochID + 1);
            Interlocked.Exchange(ref _completedWrites, 0);

            _bufbak = buf;
            await TryHardenAsync();
        }

        private async Task HardenAsync(byte[] firstBufToCommit,
                          int length1,
                          byte[] secondBufToCommit,
                          int length2)
        {
            try
            { 
                // writes to _logstream - don't want to persist logs when perf testing so this is optional parameter
                if (_persistLogs)
                {
                    _logStream.Write(firstBufToCommit, 0, 4);
                    _logStream.WriteIntFixed(length1 + length2);
                    _logStream.Write(firstBufToCommit, 8, 32);
                    await _logStream.WriteAsync(firstBufToCommit, HeaderSize, length1 - HeaderSize);
                    await _logStream.WriteAsync(secondBufToCommit, 0, length2);
                    await _logStream.FlushAsync();
                }

                Interlocked.Increment(ref _durablePageID);

                // XXX Jose: Need to deal with this!
                // TryCommit(uncommittedWatermarks, outputs, pageDependencies, pageID);
            }
            catch (Exception e)
            {
                throw e;
            }
            _bufbak = firstBufToCommit;
            await TryHardenAsync();
        }

        public async Task TryHardenAsync()
        {
            long localStatus;
            localStatus = Interlocked.Read(ref _status);

            var bufLength = ((localStatus >> SealedBits) & Last32Mask);
            // give up and try later if the sealed bit is set or there is nothing to write
            if (localStatus % 2 == 1 || bufLength == HeaderSize || _bufbak == null)
            {
                return;
            }

            // Assemble the new status 
            long newLocalStatus;
            newLocalStatus = localStatus + 1;
            var origVal = Interlocked.CompareExchange(ref _status, newLocalStatus, localStatus);

            // Check if the compare and swap succeeded, otherwise skip flush
            if (origVal == localStatus)
            {
                // This call successfully sealed the buffer.

                // We have just filled the backup buffer and must wait until any other commit finishes
                int counter = 0;
                while (_bufbak == null)
                {
                    counter++;
                    if (counter == 100000)
                    {
                        counter = 0;
                        await Task.Yield();
                    }
                }

                // There is no other write going on. Take the backup buffer
                var newWriteBuf = _bufbak;
                _bufbak = null;

                // Wait for other writes to complete before committing
                while (true)
                {
                    localStatus = Interlocked.Read(ref _status);
                    var numWrites = (localStatus >> (64 - numWritesBits));
                    if (numWrites == 0)
                    {
                        break;
                    }
                    await Task.Yield();
                }

                // Filling header with enough info to detect incomplete writes and also writing the page length
                var writeStream = new MemoryStream(_buf, 4, 20);
                writeStream.WriteIntFixed((int)bufLength);
                long checkBytes = CheckBytes(HeaderSize, (int)bufLength - HeaderSize);
                writeStream.WriteLongFixed(checkBytes);
                writeStream.WriteLongFixed(_nextWriteID);
                var pageID = _nextWriteID;
                _nextWriteID++;

                _lastCommitTask = HardenAsync(_buf, (int)bufLength);
                newLocalStatus = HeaderSize << SealedBits;
                _buf = newWriteBuf;
                _status = newLocalStatus;
            }
        }

        public void Snapshot()
        {
            Debug.Assert(false);
        }

        public byte[] GetNext()
        {
            Debug.Assert(false);
        }
    }
}

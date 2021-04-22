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
        Task<LSN> SleepAsync();
        void Snapshot();
        byte[] GetNext();
    }

    public interface ILogAppendClient
    {
        Task<ValueTuple<byte[], long>> BeforeEpochAsync(LSN lsn);
        Task AfterEpochAsync(LSN lsn);
        void Commit(LSN lsn);
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
        internal const int HeaderSize = 44;  // 4 Committer ID, 8 Epoch ID, 8 check bytes, 4 page size, 4 footer size, 8 min LSN, 8 max LSN

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

        public async Task<LSN> SleepAsync()
        {
            while (true)
            {
                // We're going to try to seal the buffer to prevent any more appends.
                var localStatus = Interlocked.Read(ref _status);
                while (localStatus % 2 == 1)
                {
                    await Task.Yield();
                    localStatus = Interlocked.Read(ref _status);
                }

                var newLocalStatus = localStatus + 1;
                var origVal = Interlocked.CompareExchange(ref _status, newLocalStatus, localStatus);
                if (origVal == localStatus)
                {
                    // Sealed the active buffer, wait for prior buff to be made durable.
                    while (_bufbak == null)
                    {
                        await Task.Yield();
                    }

                    // Wait for all outstanding writes to complete.
                    var numWrites = (newLocalStatus >> (64 - numWritesBits));
                    var completedWrites = Interlocked.Read(ref _completedWrites);
                    while (numWrites < completedWrites)
                    {
                        await Task.Yield();
                        Interlocked.Read(ref _completedWrites);
                    }

                    LSN retLSN;
                    retLSN.sequenceID = _lastEpochSequenceID + (newLocalStatus >> (64 - numWritesBits));
                    retLSN.epochID = _epochID;
                    retLSN.logID = _logID;
                    return retLSN;
                }
            }
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.IO;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.VisualStudio.Threading;
using Microsoft.WindowsAzure.Storage.Queue;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.CompilerServices;
using CRA.ClientLibrary;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Reflection;
using System.Xml.Serialization;

namespace Ambrosia
{
    internal struct LongPair
    {
        public LongPair(long first,
                        long second)
        {
            First = first;
            Second = second;
        }
        internal long First { get; set; }
        internal long Second { get; set; }
    }

    internal static class DictionaryTools
    {
        internal static void AmbrosiaSerialize(this ConcurrentDictionary<string, long> dict, LogWriter writeToStream)
        {
            writeToStream.WriteIntFixed(dict.Count);
            foreach (var entry in dict)
            {
                var encodedKey = Encoding.UTF8.GetBytes(entry.Key);
                writeToStream.WriteInt(encodedKey.Length);
                writeToStream.Write(encodedKey, 0, encodedKey.Length);
                writeToStream.WriteLongFixed(entry.Value);
            }
        }

        internal static ConcurrentDictionary<string, long> AmbrosiaDeserialize(this ConcurrentDictionary<string, long> dict, LogReader readFromStream)
        {
            var _retVal = new ConcurrentDictionary<string, long>();
            var dictCount = readFromStream.ReadIntFixed();
            for (int i = 0; i < dictCount; i++)
            {
                var myString = Encoding.UTF8.GetString(readFromStream.ReadByteArray());
                long seqNo = readFromStream.ReadLongFixed();
                _retVal.TryAdd(myString, seqNo);
            }
            return _retVal;
        }

        internal static void AmbrosiaSerialize(this ConcurrentDictionary<string, LongPair> dict, LogWriter writeToStream)
        {
            writeToStream.WriteIntFixed(dict.Count);
            foreach (var entry in dict)
            {
                var encodedKey = Encoding.UTF8.GetBytes(entry.Key);
                writeToStream.WriteInt(encodedKey.Length);
                writeToStream.Write(encodedKey, 0, encodedKey.Length);
                writeToStream.WriteLongFixed(entry.Value.First);
                writeToStream.WriteLongFixed(entry.Value.Second);
            }
        }

        internal static ConcurrentDictionary<string, LongPair> AmbrosiaDeserialize(this ConcurrentDictionary<string, LongPair> dict, LogReader readFromStream)
        {
            var _retVal = new ConcurrentDictionary<string, LongPair>();
            var dictCount = readFromStream.ReadIntFixed();
            for (int i = 0; i < dictCount; i++)
            {
                var myString = Encoding.UTF8.GetString(readFromStream.ReadByteArray());
                var newLongPair = new LongPair();
                newLongPair.First = readFromStream.ReadLongFixed();
                newLongPair.Second = readFromStream.ReadLongFixed();
                _retVal.TryAdd(myString, newLongPair);
            }
            return _retVal;
        }

        internal static void AmbrosiaSerialize(this ConcurrentDictionary<Guid, IPAddress> dict, Stream writeToStream)
        {
            writeToStream.WriteIntFixed(dict.Count);
            foreach (var entry in dict)
            {
                writeToStream.Write(entry.Key.ToByteArray(), 0, 16);
                var IPBytes = entry.Value.GetAddressBytes();
                writeToStream.WriteByte((byte)IPBytes.Length);
                writeToStream.Write(IPBytes, 0, IPBytes.Length);
            }
        }

        internal static ConcurrentDictionary<Guid, IPAddress> AmbrosiaDeserialize(this ConcurrentDictionary<Guid, IPAddress> dict, LogReader readFromStream)
        {
            var _retVal = new ConcurrentDictionary<Guid, IPAddress>();
            var dictCount = readFromStream.ReadIntFixed();
            for (int i = 0; i < dictCount; i++)
            {
                var myBytes = new byte[16];
                readFromStream.Read(myBytes, 0, 16);
                var newGuid = new Guid(myBytes);
                byte addressSize = (byte)readFromStream.ReadByte();
                if (addressSize > 16)
                {
                    myBytes = new byte[addressSize];
                }
                readFromStream.Read(myBytes, 0, addressSize);
                var newAddress = new IPAddress(myBytes);
                _retVal.TryAdd(newGuid, newAddress);
            }
            return _retVal;
        }

        internal static void AmbrosiaSerialize(this ConcurrentDictionary<string, InputConnectionRecord> dict, LogWriter writeToStream)
        {
            writeToStream.WriteIntFixed(dict.Count);
            foreach (var entry in dict)
            {
                var keyEncoding = Encoding.UTF8.GetBytes(entry.Key);
                Console.WriteLine("input {0} seq no: {1}", entry.Key, entry.Value.LastProcessedID);
                Console.WriteLine("input {0} replayable seq no: {1}", entry.Key, entry.Value.LastProcessedReplayableID);
                writeToStream.WriteInt(keyEncoding.Length);
                writeToStream.Write(keyEncoding, 0, keyEncoding.Length);
                writeToStream.WriteLongFixed(entry.Value.LastProcessedID);
                writeToStream.WriteLongFixed(entry.Value.LastProcessedReplayableID);
            }
        }

        internal static ConcurrentDictionary<string, InputConnectionRecord> AmbrosiaDeserialize(this ConcurrentDictionary<string, InputConnectionRecord> dict, LogReader readFromStream)
        {
            var _retVal = new ConcurrentDictionary<string, InputConnectionRecord>();
            var dictCount = readFromStream.ReadIntFixed();
            for (int i = 0; i < dictCount; i++)
            {
                var myString = Encoding.UTF8.GetString(readFromStream.ReadByteArray());
                long seqNo = readFromStream.ReadLongFixed();
                var newRecord = new InputConnectionRecord();
                newRecord.LastProcessedID = seqNo;
                seqNo = readFromStream.ReadLongFixed();
                newRecord.LastProcessedReplayableID = seqNo;
                _retVal.TryAdd(myString, newRecord);
            }
            return _retVal;
        }

        internal static void AmbrosiaSerialize(this ConcurrentDictionary<string, OutputConnectionRecord> dict, LogWriter writeToStream)
        {
            writeToStream.WriteIntFixed(dict.Count);
            foreach (var entry in dict)
            {
                var keyEncoding = Encoding.UTF8.GetBytes(entry.Key);
                writeToStream.WriteInt(keyEncoding.Length);
                writeToStream.Write(keyEncoding, 0, keyEncoding.Length);
                writeToStream.WriteLongFixed(entry.Value.LastSeqNoFromLocalService);
                // Lock to ensure atomic update of both variables due to race in InputControlListenerAsync
                long trimTo;
                long replayableTrimTo;
                lock (entry.Value._trimLock)
                {
                    trimTo = entry.Value.TrimTo;
                    replayableTrimTo = entry.Value.ReplayableTrimTo;
                }
                writeToStream.WriteLongFixed(trimTo);
                writeToStream.WriteLongFixed(replayableTrimTo);
                entry.Value.BufferedOutput.Serialize(writeToStream);
            }
        }

        internal static ConcurrentDictionary<string, OutputConnectionRecord> AmbrosiaDeserialize(this ConcurrentDictionary<string, OutputConnectionRecord> dict, LogReader readFromStream, AmbrosiaRuntime thisAmbrosia)
        {
            var _retVal = new ConcurrentDictionary<string, OutputConnectionRecord>();
            var dictCount = readFromStream.ReadIntFixed();
            for (int i = 0; i < dictCount; i++)
            {
                var myString = Encoding.UTF8.GetString(readFromStream.ReadByteArray());
                var newRecord = new OutputConnectionRecord(thisAmbrosia);
                newRecord.LastSeqNoFromLocalService = readFromStream.ReadLongFixed();
                newRecord.TrimTo = readFromStream.ReadLongFixed();
                newRecord.ReplayableTrimTo = readFromStream.ReadLongFixed();
                newRecord.BufferedOutput = EventBuffer.Deserialize(readFromStream, thisAmbrosia, newRecord);
                _retVal.TryAdd(myString, newRecord);
            }
            return _retVal;
        }
    }

    // Note about this class: contention becomes significant when MaxBufferPages > ~50. This could be reduced by having page level locking.
    // It seems experimentally that having many pages is good for small message sizes, where most of the page ends up empty. More investigation
    // is needed to autotune defaultPageSize and MaxBufferPages
    internal class EventBuffer
    {
        const int defaultPageSize = 1024 * 1024;
        int NormalMaxBufferPages = 30;
        static ConcurrentQueue<BufferPage> _pool = null;
        int _curBufPages;
        AmbrosiaRuntime _owningRuntime;
        OutputConnectionRecord _owningOutputRecord;

        internal class BufferPage
        {
            public byte[] PageBytes { get; set; }
            public int curLength { get; set; }
            public long HighestSeqNo { get; set; }
            public long UnsentReplayableMessages { get; set; }
            public long LowestSeqNo { get; set; }
            public long TotalReplayableMessages { get; internal set; }

            public BufferPage(byte[] pageBytes)
            {
                PageBytes = pageBytes;
                curLength = 0;
                HighestSeqNo = 0;
                LowestSeqNo = 0;
                UnsentReplayableMessages = 0;
                TotalReplayableMessages = 0;
            }

            public void CheckPageIntegrity()
            {
                var numberOfRPCs = HighestSeqNo - LowestSeqNo + 1;
                var lengthOfCurrentRPC = 0;
                int endIndexOfCurrentRPC = 0;
                int cursor = 0;

                for (int i = 0; i < numberOfRPCs; i++)
                {
                    lengthOfCurrentRPC = PageBytes.ReadBufferedInt(cursor);
                    cursor += StreamCommunicator.IntSize(lengthOfCurrentRPC);
                    endIndexOfCurrentRPC = cursor + lengthOfCurrentRPC;
                    if (endIndexOfCurrentRPC > curLength)
                    {
                        Console.WriteLine("RPC Exceeded length of Page!!");
                        throw new Exception("RPC Exceeded length of Page!!");
                    }

                    var shouldBeRPCByte = PageBytes[cursor];
                    if (shouldBeRPCByte != AmbrosiaRuntime.RPCByte)
                    {
                        Console.WriteLine("UNKNOWN BYTE: {0}!!", shouldBeRPCByte);
                        throw new Exception("Illegal leading byte in message");
                    }
                    cursor++;

                    var isReturnValue = (PageBytes[cursor++] == (byte)1);

                    if (isReturnValue) // receiving a return value
                    {
                        var sequenceNumber = PageBytes.ReadBufferedLong(cursor);
                        cursor += StreamCommunicator.LongSize(sequenceNumber);
                    }
                    else // receiving an RPC
                    {
                        var methodId = PageBytes.ReadBufferedInt(cursor);
                        cursor += StreamCommunicator.IntSize(methodId);
                        var fireAndForget = (PageBytes[cursor] == (byte)1) || (PageBytes[cursor] == (byte)2);
                        cursor++;

                        string senderOfRPC = null;
                        long sequenceNumber = 0;

                        if (!fireAndForget)
                        {
                            // read return address and sequence number
                            var senderOfRPCLength = PageBytes.ReadBufferedInt(cursor);
                            var sizeOfSender = StreamCommunicator.IntSize(senderOfRPCLength);
                            cursor += sizeOfSender;
                            senderOfRPC = Encoding.UTF8.GetString(PageBytes, cursor, senderOfRPCLength);
                            cursor += senderOfRPCLength;
                            sequenceNumber = PageBytes.ReadBufferedLong(cursor);
                            cursor += StreamCommunicator.LongSize(sequenceNumber);
                            //Console.WriteLine("Received RPC call to method with id: {0} and sequence number {1}", methodId, sequenceNumber);
                        }
                        else
                        {

                            //Console.WriteLine("Received fire-and-forget RPC call to method with id: {0}", methodId);
                        }

                        var lengthOfSerializedArguments = endIndexOfCurrentRPC - cursor;
                        cursor += lengthOfSerializedArguments;
                    }
                }
            }

            internal void CheckSendBytes(int posToStart,
                                         int numRPCs,
                                         int bytes)
            {
                int cursor = posToStart;
                for (int i = 0; i < numRPCs; i++)
                {
                    var lengthOfCurrentRPC = PageBytes.ReadBufferedInt(cursor);
                    cursor += StreamCommunicator.IntSize(lengthOfCurrentRPC);
                    var endIndexOfCurrentRPC = cursor + lengthOfCurrentRPC;
                    if (endIndexOfCurrentRPC > curLength)
                    {
                        Console.WriteLine("RPC Exceeded length of Page!!");
                        throw new Exception("RPC Exceeded length of Page!!");
                    }

                    var shouldBeRPCByte = PageBytes[cursor];
                    if (shouldBeRPCByte != AmbrosiaRuntime.RPCByte)
                    {
                        Console.WriteLine("UNKNOWN BYTE: {0}!!", shouldBeRPCByte);
                        throw new Exception("Illegal leading byte in message");
                    }
                    cursor++;

                    var isReturnValue = (PageBytes[cursor++] == (byte)1);

                    if (isReturnValue) // receiving a return value
                    {
                        var sequenceNumber = PageBytes.ReadBufferedLong(cursor);
                        cursor += StreamCommunicator.LongSize(sequenceNumber);
                    }
                    else // receiving an RPC
                    {
                        var methodId = PageBytes.ReadBufferedInt(cursor);
                        cursor += StreamCommunicator.IntSize(methodId);
                        var fireAndForget = (PageBytes[cursor] == (byte)1) || (PageBytes[cursor] == (byte)2);
                        cursor++;
                        string senderOfRPC = null;
                        long sequenceNumber = 0;

                        if (!fireAndForget)
                        {
                            // read return address and sequence number
                            var senderOfRPCLength = PageBytes.ReadBufferedInt(cursor);
                            var sizeOfSender = StreamCommunicator.IntSize(senderOfRPCLength);
                            cursor += sizeOfSender;
                            senderOfRPC = Encoding.UTF8.GetString(PageBytes, cursor, senderOfRPCLength);
                            cursor += senderOfRPCLength;
                            sequenceNumber = PageBytes.ReadBufferedLong(cursor);
                            cursor += StreamCommunicator.LongSize(sequenceNumber);
                            //Console.WriteLine("Received RPC call to method with id: {0} and sequence number {1}", methodId, sequenceNumber);
                        }
                        else
                        {

                            //Console.WriteLine("Received fire-and-forget RPC call to method with id: {0}", methodId);
                        }

                        var lengthOfSerializedArguments = endIndexOfCurrentRPC - cursor;
                        cursor += lengthOfSerializedArguments;
                    }
                }
            }
        }

        long _trimLock;
        long _appendLock;

        ElasticCircularBuffer<BufferPage> _bufferQ;

        internal EventBuffer(AmbrosiaRuntime owningRuntime,
                             OutputConnectionRecord owningOutputRecord)
        {
            _bufferQ = new ElasticCircularBuffer<BufferPage>();
            _appendLock = 0;
            _owningRuntime = owningRuntime;
            _curBufPages = 0;
            _owningOutputRecord = owningOutputRecord;
            _trimLock = 0;
        }

        internal void Serialize(LogWriter writeToStream)
        {
            writeToStream.WriteIntFixed(_bufferQ.Count);
            foreach (var currentBuf in _bufferQ)
            {
                writeToStream.WriteIntFixed(currentBuf.PageBytes.Length);
                writeToStream.WriteIntFixed(currentBuf.curLength);
                writeToStream.Write(currentBuf.PageBytes, 0, currentBuf.curLength);
                writeToStream.WriteLongFixed(currentBuf.HighestSeqNo);
                writeToStream.WriteLongFixed(currentBuf.LowestSeqNo);
                writeToStream.WriteLongFixed(currentBuf.UnsentReplayableMessages);
                writeToStream.WriteLongFixed(currentBuf.TotalReplayableMessages);
            }
        }

        internal static EventBuffer Deserialize(LogReader readFromStream,
                                                AmbrosiaRuntime owningRuntime,
                                                OutputConnectionRecord owningOutputRecord)
        {
            var _retVal = new EventBuffer(owningRuntime, owningOutputRecord);
            var bufferCount = readFromStream.ReadIntFixed();
            for (int i = 0; i < bufferCount; i++)
            {
                var pageSize = readFromStream.ReadIntFixed();
                var pageFilled = readFromStream.ReadIntFixed();
                var myBytes = new byte[pageSize];
                readFromStream.Read(myBytes, 0, pageFilled);
                var newBufferPage = new BufferPage(myBytes);
                newBufferPage.curLength = pageFilled;
                newBufferPage.HighestSeqNo = readFromStream.ReadLongFixed();
                newBufferPage.LowestSeqNo = readFromStream.ReadLongFixed();
                newBufferPage.UnsentReplayableMessages = readFromStream.ReadLongFixed();
                newBufferPage.TotalReplayableMessages = readFromStream.ReadLongFixed();
                _retVal._bufferQ.Enqueue(ref newBufferPage);
            }
            return _retVal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AcquireAppendLock(long lockVal = 1)
        {
            while (true)
            {
                var origVal = Interlocked.CompareExchange(ref _appendLock, lockVal, 0);
                if (origVal == 0)
                {
                    // We have the lock
                    break;
                }
            }
        }

        internal long ReadAppendLock()
        {
            return Interlocked.Read(ref _appendLock);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ReleaseAppendLock()
        {
            Interlocked.Exchange(ref _appendLock, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AcquireTrimLock(long lockVal)
        {
            while (true)
            {
                var origVal = Interlocked.CompareExchange(ref _trimLock, lockVal, 0);
                if (origVal == 0)
                {
                    // We have the lock
                    break;
                }
            }
        }

        internal long ReadTrimLock()
        {
            return Interlocked.Read(ref _trimLock);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ReleaseTrimLock()
        {
            Interlocked.Exchange(ref _trimLock, 0);
        }

        internal class BuffersCursor
        {
            public IEnumerator<BufferPage> PageEnumerator { get; set; }
            public int PagePos { get; set; }
            public int RelSeqPos { get; set; }
            public BuffersCursor(IEnumerator<BufferPage> inPageEnumerator,
                                 int inPagePos,
                                 int inRelSeqPos)
            {
                RelSeqPos = inRelSeqPos;
                PageEnumerator = inPageEnumerator;
                PagePos = inPagePos;
            }
        }

        internal async Task<BuffersCursor> SendAsync(Stream outputStream,
                                                     BuffersCursor placeToStart,
                                                     bool reconnecting)
        {
            // If the cursor is invalid because of trimming or reconnecting, create it again
            if (placeToStart.PagePos == -1)
            {
                return await ReplayFromAsync(outputStream, _owningOutputRecord.LastSeqSentToReceiver + 1, reconnecting);

            }
            var nextSeqNo = _owningOutputRecord.LastSeqSentToReceiver + 1;
            var bufferEnumerator = placeToStart.PageEnumerator;
            var posToStart = placeToStart.PagePos;
            var relSeqPos = placeToStart.RelSeqPos;

            // We are guaranteed to have an enumerator and starting point. Must send output.
            AcquireAppendLock(2);
            bool needToUnlockAtEnd = true;
            do
            {
                var curBuffer = bufferEnumerator.Current;
                var pageLength = curBuffer.curLength;
                var morePages = (curBuffer != _bufferQ.Last());
                int numReplayableMessagesToSend;
                if (posToStart == 0)
                {
                    // We are starting to send contents of the page. Send everything
                    numReplayableMessagesToSend = (int)curBuffer.TotalReplayableMessages;
                }
                else
                {
                    // We are in the middle of sending this page. Respect the previously set counter
                    numReplayableMessagesToSend = (int)curBuffer.UnsentReplayableMessages;
                }
                int numRPCs = (int)(curBuffer.HighestSeqNo - curBuffer.LowestSeqNo + 1 - relSeqPos);
                curBuffer.UnsentReplayableMessages = 0;
                ReleaseAppendLock();
                Debug.Assert((nextSeqNo == curBuffer.LowestSeqNo + relSeqPos) && (nextSeqNo >= curBuffer.LowestSeqNo) && ((nextSeqNo + numRPCs - 1) <= curBuffer.HighestSeqNo));
                ReleaseTrimLock();
                // send the buffer
                if (pageLength - posToStart > 0)
                {
                    // We really have output to send. Send it.
                    //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! Uncomment/Comment for testing
                    //Console.WriteLine("Wrote from {0} to {1}, {2}", curBuffer.LowestSeqNo, curBuffer.HighestSeqNo, morePages);
                    int bytesInBatchData = pageLength - posToStart;
                    if (numRPCs > 1)
                    {
                        if (numReplayableMessagesToSend == numRPCs)
                        {
                            // writing a batch
                            outputStream.WriteInt(bytesInBatchData + 1 + StreamCommunicator.IntSize(numRPCs));
                            outputStream.WriteByte(AmbrosiaRuntime.RPCBatchByte);
                            outputStream.WriteInt(numRPCs);
#if DEBUG
                            try
                            {
                                curBuffer.CheckSendBytes(posToStart, numRPCs, pageLength - posToStart);
                            } catch (Exception e)
                            {
                                Console.WriteLine("Error sending partial page, checking page integrity: {0}", e.Message);
                                curBuffer.CheckPageIntegrity();
                                throw e;
                            }
#endif
                            await outputStream.WriteAsync(curBuffer.PageBytes, posToStart, bytesInBatchData);
                            await outputStream.FlushAsync();
                        }
                        else
                        {
                            // writing a mixed batch
                            outputStream.WriteInt(bytesInBatchData + 1 + StreamCommunicator.IntSize(numRPCs) + StreamCommunicator.IntSize(numReplayableMessagesToSend));
                            outputStream.WriteByte(AmbrosiaRuntime.CountReplayableRPCBatchByte);
                            outputStream.WriteInt(numRPCs);
                            outputStream.WriteInt(numReplayableMessagesToSend);
#if DEBUG
                            try
                            {
                                curBuffer.CheckSendBytes(posToStart, numRPCs, pageLength - posToStart);
                            } catch (Exception e)
                            {
                                Console.WriteLine("Error sending partial page, checking page integrity: {0}", e.Message);
                                curBuffer.CheckPageIntegrity();
                                throw e;
                            }
#endif
                            await outputStream.WriteAsync(curBuffer.PageBytes, posToStart, bytesInBatchData);
                            await outputStream.FlushAsync();
                        }
                    }
                    else
                    {
                        // writing individual RPCs
                        await outputStream.WriteAsync(curBuffer.PageBytes, posToStart, bytesInBatchData);
                        await outputStream.FlushAsync();
                    }
                }
                AcquireTrimLock(2);
                _owningOutputRecord.LastSeqSentToReceiver += numRPCs;

                // Must handle cases where trim came in during the actual send and reset or pushed the iterator
                if ((_owningOutputRecord.placeInOutput != null) &&
                    ((_owningOutputRecord.placeInOutput.PageEnumerator != bufferEnumerator) ||
                    _owningOutputRecord.placeInOutput.PagePos == -1))
                {
                    // Trim replaced the enumerator. Must reset
                    if (morePages)
                    {
                        // Not done outputting. Try again
                        if (_owningOutputRecord._sendsEnqueued == 0)
                        {
                            Interlocked.Increment(ref _owningOutputRecord._sendsEnqueued);
                            _owningOutputRecord.DataWorkQ.Enqueue(-1);
                        }
                    }

                    // Done outputting. Just return the enumerator replacement
                    return _owningOutputRecord.placeInOutput;
                }

                // bufferEnumerator is still good. Continue
                Debug.Assert((nextSeqNo == curBuffer.LowestSeqNo + relSeqPos) && (nextSeqNo >= curBuffer.LowestSeqNo) && ((nextSeqNo + numRPCs - 1) <= curBuffer.HighestSeqNo));
                nextSeqNo += numRPCs;
                if (morePages)
                {
                    // More pages to output
                    posToStart = 0;
                    relSeqPos = 0;
                }
                else
                {
                    // Future output may be put on this page
                    posToStart = pageLength;
                    relSeqPos += numRPCs;
                    needToUnlockAtEnd = false;
                    break;
                }
                AcquireAppendLock(2);
            }
            while (bufferEnumerator.MoveNext());
            placeToStart.PageEnumerator = bufferEnumerator;
            placeToStart.PagePos = posToStart;
            placeToStart.RelSeqPos = relSeqPos;
            if (needToUnlockAtEnd)
            {
                ReleaseAppendLock();
            }
            return placeToStart;
        }

        internal async Task<BuffersCursor> ReplayFromAsync(Stream outputStream,
                                                           long firstSeqNo,
                                                           bool reconnecting)
        {
/*            if (reconnecting)
            {
                var bufferE = _bufferQ.GetEnumerator();
                while (bufferE.MoveNext())
                {
                    var curBuffer = bufferE.Current;
                    Debug.Assert(curBuffer.LowestSeqNo <= firstSeqNo);
                    int skipEvents = 0;
                    if (curBuffer.HighestSeqNo >= firstSeqNo)
                    {
                        // We need to send some or all of this buffer
                        skipEvents = (int)(Math.Max(0, firstSeqNo - curBuffer.LowestSeqNo));
                    }
                    else
                    {
                        skipEvents = 0;
                    }
                    int bufferPos = 0;
                    AcquireAppendLock(2);
                    curBuffer.UnsentReplayableMessages = curBuffer.TotalReplayableMessages;
                    for (int i = 0; i < skipEvents; i++)
                    {
                        int eventSize = curBuffer.PageBytes.ReadBufferedInt(bufferPos);
                        var methodID = curBuffer.PageBytes.ReadBufferedInt(bufferPos + StreamCommunicator.IntSize(eventSize) + 2);
                        if (curBuffer.PageBytes[bufferPos + StreamCommunicator.IntSize(eventSize) + 2 + StreamCommunicator.IntSize(methodID)] != (byte)RpcTypes.RpcType.Impulse)
                        {
                            curBuffer.UnsentReplayableMessages--;
                        }
                        bufferPos += eventSize + StreamCommunicator.IntSize(eventSize);
                    }
                    ReleaseAppendLock();
                }
            }*/
            var bufferEnumerator = _bufferQ.GetEnumerator();
            // Scan through pages from head to tail looking for events to output
            while (bufferEnumerator.MoveNext())
            {
                var curBuffer = bufferEnumerator.Current;
                Debug.Assert(curBuffer.LowestSeqNo <= firstSeqNo);
                if (curBuffer.HighestSeqNo >= firstSeqNo)
                {
                    // We need to send some or all of this buffer
                    int skipEvents = (int)(Math.Max(0, firstSeqNo - curBuffer.LowestSeqNo));

                    int bufferPos = 0;
                    if (true) // BUGBUG We are temporarily disabling this optimization which avoids unnecessary locking as reconnecting is not a sufficient criteria: We found a case where input is arriving during reconnection where counting was getting disabled incorrectly. Further investigation is required.
//                    if (reconnecting) // BUGBUG We are temporarily disabling this optimization which avoids unnecessary locking as reconnecting is not a sufficient criteria: We found a case where input is arriving during reconnection where counting was getting disabled incorrectly. Further investigation is required.
                    {
                        // We need to reset how many replayable messages have been sent. We want to minimize the use of
                        // this codepath because of the expensive locking, which can compete with new RPCs getting appended
                        AcquireAppendLock(2);
                        curBuffer.UnsentReplayableMessages = curBuffer.TotalReplayableMessages;
                        for (int i = 0; i < skipEvents; i++)
                        {
                            int eventSize = curBuffer.PageBytes.ReadBufferedInt(bufferPos);
                            var methodID = curBuffer.PageBytes.ReadBufferedInt(bufferPos + StreamCommunicator.IntSize(eventSize) + 2);
                            if (curBuffer.PageBytes[bufferPos + StreamCommunicator.IntSize(eventSize) + 2 + StreamCommunicator.IntSize(methodID)] != (byte)RpcTypes.RpcType.Impulse)
                            {
                                curBuffer.UnsentReplayableMessages--;
                            }
                            bufferPos += eventSize + StreamCommunicator.IntSize(eventSize);
                        }
                        ReleaseAppendLock();
                    }
                    else
                    {
                        // We assume the counter for unsent replayable messages is correct. NO LOCKING NEEDED
                        for (int i = 0; i < skipEvents; i++)
                        {
                            int eventSize = curBuffer.PageBytes.ReadBufferedInt(bufferPos);
                            bufferPos += eventSize + StreamCommunicator.IntSize(eventSize);
                        }

                    }
                    return await SendAsync(outputStream, new BuffersCursor(bufferEnumerator, bufferPos, skipEvents), false);
                }
            }
            // There's no output to replay
            return new BuffersCursor(bufferEnumerator, -1, 0);
        }

        private void addBufferPage(int writeLength,
                                   long firstSeqNo)
        {
            BufferPage bufferPage;
            ReleaseAppendLock();
            while (!_pool.TryDequeue(out bufferPage))
            {
                if (_owningRuntime.Recovering || _owningOutputRecord.ResettingConnection ||
                    _owningRuntime.CheckpointingService || _owningOutputRecord.ConnectingAfterRestart)
                {
                    var newBufferPageBytes = new byte[Math.Max(defaultPageSize, writeLength)];
                    bufferPage = new BufferPage(newBufferPageBytes);
                    _curBufPages++;
                    break;
                }
                Thread.Yield();
            }
            AcquireAppendLock();
            {
                // Grabbed a page from the pool
                if (bufferPage.PageBytes.Length < writeLength)
                {
                    // Page isn't big enough. Throw it away and create a bigger one
                    bufferPage.PageBytes = new byte[writeLength];
                }
            }
            bufferPage.LowestSeqNo = firstSeqNo;
            bufferPage.HighestSeqNo = firstSeqNo;
            bufferPage.UnsentReplayableMessages = 0;
            bufferPage.TotalReplayableMessages = 0;
            bufferPage.curLength = 0;
            _bufferQ.Enqueue(ref bufferPage);
        }

        internal void CreatePool(int numAlreadyAllocated = 0)
        {
            _pool = new ConcurrentQueue<BufferPage>();
            for (int i = 0; i < (NormalMaxBufferPages - numAlreadyAllocated); i++)
            {
                var bufferPageBytes = new byte[defaultPageSize];
                var bufferPage = new BufferPage(bufferPageBytes);
                _pool.Enqueue(bufferPage);
                _curBufPages++;
            }
        }

        // Assumed that the caller releases the lock acquired here
        internal BufferPage GetWritablePage(int writeLength,
                                            long nextSeqNo)
        {
            if (_pool == null)
            {
                CreatePool();
            }
            AcquireAppendLock();
            // Create a new buffer page if there is none, or if we are introducing a sequence number discontinuity
            if (_bufferQ.IsEmpty() || nextSeqNo != (_bufferQ.PeekLast().HighestSeqNo + 1))
            {
                addBufferPage(writeLength, nextSeqNo);
            }
            else
            {
                // There is something already in the buffer. Check it out.
                var outPage = _bufferQ.PeekLast();
                if ((outPage.PageBytes.Length - outPage.curLength) < writeLength)
                {
                    // Not enough space on last page. Add another
                    addBufferPage(writeLength, nextSeqNo);
                }
            }
            var retVal = _bufferQ.PeekLast();
            return retVal;
        }

        internal void Trim(long commitSeqNo,
                           ref BuffersCursor placeToStart)
        {
            // Keep trimming pages until we can't anymore or the Q is empty
            while (!_bufferQ.IsEmpty())
            {
                var currentHead = _bufferQ.PeekFirst();
                bool acquiredLock = false;
                // Acquire the lock to ensure someone isn't adding another output to it.
                AcquireAppendLock(3);
                acquiredLock = true;
                if (currentHead.HighestSeqNo <= commitSeqNo)
                {
                    // Trimming for real
                    // First maintain the placeToStart cursor
                    if ((placeToStart != null) && ((placeToStart.PagePos >= 0) && (placeToStart.PageEnumerator.Current == currentHead)))
                    {
                        // Need to move the enumerator forward. Note that it may be on the last page if all output
                        // buffers can be trimmed
                        if (placeToStart.PageEnumerator.MoveNext())
                        {
                            placeToStart.PagePos = 0;
                        }
                        else
                        {
                            placeToStart.PagePos = -1;
                        }
                    }
                    _bufferQ.Dequeue();
                    if (acquiredLock)
                    {
                        ReleaseAppendLock();
                    }
                    // Return page to pool
                    currentHead.curLength = 0;
                    currentHead.HighestSeqNo = 0;
                    currentHead.UnsentReplayableMessages = 0;
                    currentHead.TotalReplayableMessages = 0;
                    if (_pool == null)
                    {
                        CreatePool(_bufferQ.Count);
                    }
                    if (_owningRuntime.Recovering || _curBufPages <= NormalMaxBufferPages)
                    {
                        _pool.Enqueue(currentHead);
                    }
                    else
                    {
                        _curBufPages--;
                    }
                }
                else
                {
                    // Nothing more to trim
                    if (acquiredLock)
                    {
                        ReleaseAppendLock();
                    }
                    break;
                }
            }
        }

        // Note that this method assumes that the caller has locked this connection record to avoid possible interference. Note that this method
        // assumes no discontinuities in sequence numbers since adjusting can only happen on newly initialized service (no recovery), and since
        // discontinuities can only happen as the result of recovery
        internal long AdjustFirstSeqNoTo(long commitSeqNo)
        {
            var bufferEnumerator = _bufferQ.GetEnumerator();
            // Scan through pages from head to tail looking for events to output
            while (bufferEnumerator.MoveNext())
            {
                var curBuffer = bufferEnumerator.Current;
                var seqNoDiff = curBuffer.HighestSeqNo - curBuffer.LowestSeqNo;
                curBuffer.LowestSeqNo = commitSeqNo;
                curBuffer.HighestSeqNo = commitSeqNo + seqNoDiff;
                commitSeqNo += seqNoDiff + 1;
            }
            return commitSeqNo - 1;
        }

        // Returns the highest sequence number left in the buffers after removing the non-replayable messages, or -1 if the
        // buffers are empty.
        internal long TrimAndUnbufferNonreplayableCalls(long trimSeqNo,
                                                        long matchingReplayableSeqNo)
        {
            if (trimSeqNo < 1)
            {
                return matchingReplayableSeqNo;
            }
            // No locking necessary since this should only get called during recovery before replay and before a checkpooint is sent to service
            // First trim
            long highestTrimmedSeqNo = -1;
            while (!_bufferQ.IsEmpty())
            {
                var currentHead = _bufferQ.PeekFirst();
                if (currentHead.HighestSeqNo <= trimSeqNo)
                {
                    // Must completely trim the page
                    _bufferQ.Dequeue();
                    // Return page to pool
                    highestTrimmedSeqNo = currentHead.HighestSeqNo;
                    currentHead.curLength = 0;
                    currentHead.HighestSeqNo = 0;
                    currentHead.UnsentReplayableMessages = 0;
                    currentHead.TotalReplayableMessages = 0;
                    if (_pool == null)
                    {
                        CreatePool(_bufferQ.Count);
                    }
                    _pool.Enqueue(currentHead);
                }
                else
                {
                    // May need to remove some data from the page
                    int readBufferPos = 0;
                    for (var i = currentHead.LowestSeqNo; i <= trimSeqNo; i++)
                    {
                        int eventSize = currentHead.PageBytes.ReadBufferedInt(readBufferPos);
                        var methodID = currentHead.PageBytes.ReadBufferedInt(readBufferPos + StreamCommunicator.IntSize(eventSize) + 2);
                        if (currentHead.PageBytes[readBufferPos + StreamCommunicator.IntSize(eventSize) + 2 + StreamCommunicator.IntSize(methodID)] != (byte)RpcTypes.RpcType.Impulse)
                        {
                            currentHead.TotalReplayableMessages--;
                        }
                        readBufferPos += eventSize + StreamCommunicator.IntSize(eventSize);
                    }
                    Buffer.BlockCopy(currentHead.PageBytes, readBufferPos, currentHead.PageBytes, 0, currentHead.PageBytes.Length - readBufferPos);
                    currentHead.LowestSeqNo += trimSeqNo - currentHead.LowestSeqNo + 1;
                    currentHead.curLength -= readBufferPos;
                    break;
                }
            }

            var bufferEnumerator = _bufferQ.GetEnumerator();
            long nextReplayableSeqNo = matchingReplayableSeqNo + 1;
            while (bufferEnumerator.MoveNext())
            {
                var curBuffer = bufferEnumerator.Current;
                var numMessagesOnPage = curBuffer.HighestSeqNo - curBuffer.LowestSeqNo + 1;
                curBuffer.LowestSeqNo = nextReplayableSeqNo;
                if (numMessagesOnPage > curBuffer.TotalReplayableMessages)
                {
                    // There are some nonreplayable messsages to remove
                    int readBufferPos = 0;
                    var newPageBytes = new byte[curBuffer.PageBytes.Length];
                    var pageWriteStream = new MemoryStream(newPageBytes);
                    for (int i = 0; i < numMessagesOnPage; i++)
                    {
                        int eventSize = curBuffer.PageBytes.ReadBufferedInt(readBufferPos);
                        var methodID = curBuffer.PageBytes.ReadBufferedInt(readBufferPos + StreamCommunicator.IntSize(eventSize) + 2);
                        if (curBuffer.PageBytes[readBufferPos + StreamCommunicator.IntSize(eventSize) + 2 + StreamCommunicator.IntSize(methodID)] != (byte)RpcTypes.RpcType.Impulse)
                        {
                            // Copy event over to new page bytes
                            pageWriteStream.Write(curBuffer.PageBytes, readBufferPos, eventSize + StreamCommunicator.IntSize(eventSize));
                        }
                        readBufferPos += eventSize + StreamCommunicator.IntSize(eventSize);
                    }
                    curBuffer.curLength = (int)pageWriteStream.Position;
                    curBuffer.HighestSeqNo = curBuffer.LowestSeqNo + curBuffer.TotalReplayableMessages - 1;
                    curBuffer.PageBytes = newPageBytes;
                }
                nextReplayableSeqNo += curBuffer.TotalReplayableMessages;
            }
            return nextReplayableSeqNo - 1;
        }

        internal void RebaseSeqNosInBuffer(long commitSeqNo,
                                           long commitSeqNoReplayable)
        {
            var seqNoDiff = commitSeqNo - commitSeqNoReplayable;
            var bufferEnumerator = _bufferQ.GetEnumerator();
            // Scan through pages from head to tail looking for events to output
            while (bufferEnumerator.MoveNext())
            {
                var curBuffer = bufferEnumerator.Current;
                curBuffer.LowestSeqNo += seqNoDiff;
                curBuffer.HighestSeqNo += seqNoDiff;
            }
        }
    }

    [DataContract]
    internal class InputConnectionRecord
    {
        public NetworkStream DataConnectionStream { get; set; }
        public NetworkStream ControlConnectionStream { get; set; }
        [DataMember]
        public long LastProcessedID { get; set; }
        [DataMember]
        public long LastProcessedReplayableID { get; set; }
        public InputConnectionRecord()
        {
            DataConnectionStream = null;
            LastProcessedID = 0;
            LastProcessedReplayableID = 0;
        }
    }

    internal class OutputConnectionRecord
    {
        // Set on reconnection. Established where to replay from or filter to
        public long ReplayFrom { get; set; }
        // The seq number from the last RPC call copied to the buffer. Not a property so interlocked read can be done
        public long LastSeqNoFromLocalService;
        // RPC output buffers
        public EventBuffer BufferedOutput { get; set; }
        // A cursor which specifies where the last RPC output ended
        public EventBuffer.BuffersCursor placeInOutput;
        // Work Q for output producing work.
        public AsyncQueue<long> DataWorkQ { get; set; }
        // Work Q for sending trim messages and perform local trimming
        public AsyncQueue<long> ControlWorkQ { get; set; }
        // Current sequence number which the output buffer may be trimmed to.
        public long TrimTo { get; set; }
        // Current replayable sequence number which the output buffer may be trimmed to.
        public long ReplayableTrimTo { get; set; }
        // The number of sends which are currently enqueued. Should be updated with interlocked increment and decrement
        public long _sendsEnqueued;
        public AmbrosiaRuntime MyAmbrosia { get; set; }
        public bool WillResetConnection { get; set; }
        public bool ConnectingAfterRestart { get; set; }
        // The latest trim location on the other side. An associated trim message MAY have already been sent
        public long RemoteTrim { get; set; }
        // The latest replayable trim location on the other side. An associated trim message MAY have already been sent
        public long RemoteTrimReplayable { get; set; }
        // The seq no of the last RPC sent to the receiver
        public long LastSeqSentToReceiver;
        internal volatile bool ResettingConnection;
        internal object _trimLock = new object();
        internal object _remoteTrimLock = new object();

        public OutputConnectionRecord(AmbrosiaRuntime inAmbrosia)
        {
            ReplayFrom = 0;
            DataWorkQ = new AsyncQueue<long>();
            ControlWorkQ = new AsyncQueue<long>();
            _sendsEnqueued = 0;
            TrimTo = -1;
            ReplayableTrimTo = -1;
            RemoteTrim = -1;
            RemoteTrimReplayable = -1;
            LastSeqNoFromLocalService = 0;
            MyAmbrosia = inAmbrosia;
            BufferedOutput = new EventBuffer(MyAmbrosia, this);
            ResettingConnection = false;
            ConnectingAfterRestart = false;
            LastSeqSentToReceiver = 0;
            WillResetConnection = inAmbrosia._createService;
            ConnectingAfterRestart = inAmbrosia._restartWithRecovery;
        }
    }

    public class AmbrosiaRuntimeParams
    {
        public int serviceReceiveFromPort;
        public int serviceSendToPort;
        public string serviceName;
        public string AmbrosiaBinariesLocation;
        public string serviceLogPath;
        public bool? createService;
        public bool pauseAtStart;
        public bool persistLogs;
        public bool activeActive;
        public long logTriggerSizeMB;
        public string storageConnectionString;
        public long currentVersion;
        public long upgradeToVersion;
        public long shardID;
    }

    public class AmbrosiaRuntime : VertexBase
    {
#if _WINDOWS
        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern void GetSystemTimePreciseAsFileTime(out long filetime);
#else
        private static void GetSystemTimePreciseAsFileTime(out long filetime)
        {
            filetime = Stopwatch.GetTimestamp();
        }
#endif

        // Util
        // Log metadata information record in _logMetadataTable
        private class serviceInstanceEntity : TableEntity
        {
            public serviceInstanceEntity()
            {
            }

            public serviceInstanceEntity(string key, string inValue)
            {
                this.PartitionKey = "(Default)";
                this.RowKey = key;
                this.value = inValue;

            }

            public string value { get; set; }
        }


        // Create a table with name tableName if it does not exist
        private CloudTable CreateTableIfNotExists(String tableName)
        {
            try
            {
                CloudTable table = _tableClient.GetTableReference(tableName);
                table.CreateIfNotExistsAsync().Wait();
                if (table == null)
                {
                    OnError(AzureOperationError, "Error creating a table in Azure");
                }
                return table;
            }
            catch
            {
                OnError(AzureOperationError, "Error creating a table in Azure");
                return null;
            }
        }


        // Replace info for a key or create a new key. Raises an exception if the operation fails for any reason.
        private void InsertOrReplaceServiceInfoRecord(string infoTitle, string info)
        {
            try
            {
                serviceInstanceEntity ServiceInfoEntity = new serviceInstanceEntity(infoTitle, info);
                TableOperation insertOrReplaceOperation = TableOperation.InsertOrReplace(ServiceInfoEntity);
                var myTask = this._serviceInstanceTable.ExecuteAsync(insertOrReplaceOperation);
                myTask.Wait();
                var retrievedResult = myTask.Result;
                if (retrievedResult.HttpStatusCode < 200 || retrievedResult.HttpStatusCode >= 300)
                {
                    OnError(AzureOperationError, "Error replacing a record in an Azure table");
                }
            }
            catch
            {
                OnError(AzureOperationError, "Error replacing a record in an Azure table");
            }
        }

        // Retrieve info for a given key
        // If no key exists or _logMetadataTable does not exist, raise an exception
        private string RetrieveServiceInfo(string key)
        {
            if (this._serviceInstanceTable != null)
            {
                TableOperation retrieveOperation = TableOperation.Retrieve<serviceInstanceEntity>("(Default)", key);
                var myTask = this._serviceInstanceTable.ExecuteAsync(retrieveOperation);
                myTask.Wait();
                var retrievedResult = myTask.Result;
                if (retrievedResult.Result != null)
                {
                    return ((serviceInstanceEntity)retrievedResult.Result).value;
                }
                else
                {
                    string taskExceptionString = myTask.Exception == null ? "" : " Task exception: " + myTask.Exception;
                    OnError(AzureOperationError, "Error retrieving info from Azure." + taskExceptionString);
                }
            }
            else
            {
                OnError(AzureOperationError, "Error retrieving info from Azure. The reference to the server instance table was not initialized.");
            }
            // Make compiler happy
            return null;
        }

        // Used to hold the bytes which will go in the log. Note that two streams are passed in. The
        // log stream must write to durable storage and be flushable, while the second stream initiates
        // actual action taken after the message has been made durable.
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
            LogWriter _logStream;
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
                             LogReader recoveryStream = null)
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
                    recoveryStream.Read(_buf, 0, bufSize);
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

            internal void Serialize(LogWriter serializeStream)
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
                            Console.WriteLine("Adding output:{0}", kv.Key);
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

            private async Task Commit(byte[] firstBufToCommit,
                                      int length1,
                                      byte[] secondBufToCommit,
                                      int length2,
                                      ConcurrentDictionary<string, LongPair> uncommittedWatermarks,
                                      ConcurrentDictionary<string, long> trimWatermarks,
                                      ConcurrentDictionary<string, OutputConnectionRecord> outputs)
            {
                try
                {
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

                    SendInputWatermarks(uncommittedWatermarks, outputs);
                    _workStream.Write(firstBufToCommit, 0, 4);
                    _workStream.WriteIntFixed(length1 + length2);
                    _workStream.Write(firstBufToCommit, 8, 16);
                    await _workStream.WriteAsync(firstBufToCommit, HeaderSize, length1 - HeaderSize);
                    await _workStream.WriteAsync(secondBufToCommit, 0, length2);
                    // Return the second byte array to the FlexReader pool
                    FlexReadBuffer.ReturnBuffer(secondBufToCommit);
                    var flushtask = _workStream.FlushAsync();
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
                await TryCommitAsync(outputs);
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
            private async Task Commit(byte[] buf,
                                      int length,
                                      ConcurrentDictionary<string, LongPair> uncommittedWatermarks,
                                      ConcurrentDictionary<string, long> trimWatermarks,
                                      ConcurrentDictionary<string, OutputConnectionRecord> outputs)
            {
                try
                {
                    // writes to _logstream - don't want to persist logs when perf testing so this is optional parameter
                    if (_persistLogs)
                    {
                        await _logStream.WriteAsync(buf, 0, length);
                        await writeFullWaterMarksAsync(uncommittedWatermarks);
                        await writeSimpleWaterMarksAsync(trimWatermarks);
                        await _logStream.FlushAsync();
                    }
                    SendInputWatermarks(uncommittedWatermarks, outputs);
                    await _workStream.WriteAsync(buf, 0, length);
                    var flushtask = _workStream.FlushAsync();
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
                await TryCommitAsync(outputs);
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
            public void SwitchLogStreams(LogWriter newLogStream)
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
                                           InputConnectionRecord associatedInputConnectionRecord)
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
                                _lastCommitTask = Commit(_buf, (int)newLength, _uncommittedWatermarks, oldTrimWatermarks, outputs);
                                newLocalStatus = HeaderSize << SealedBits;
                            }
                            else if (length > (_maxBufSize - HeaderSize))
                            {
                                // Steal the byte array in the flex buffer to return it after writing
                                copyFromFlexBuffer.StealBuffer();
                                // write new event as part of commit
                                _uncommittedWatermarks[outputToUpdate] = new LongPair(newSeqNo, newReplayableSeqNo);
                                var commitTask = Commit(_buf, (int)oldBufLength, copyFromBuffer, length, _uncommittedWatermarks, oldTrimWatermarks, outputs);
                                newLocalStatus = HeaderSize << SealedBits;
                            }
                            else
                            {
                                // commit and add new event to new buffer
                                newUncommittedWatermarks[outputToUpdate] = new LongPair(newSeqNo, newReplayableSeqNo);
                                _lastCommitTask = Commit(_buf, (int)oldBufLength, _uncommittedWatermarks, oldTrimWatermarks, outputs);
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
                                    await TryCommitAsync(outputs);
                                }
                                return (long)_logStream.FileSize;
                            }
                        }
                    }
                }
            }

            public async Task TryCommitAsync(ConcurrentDictionary<string, OutputConnectionRecord> outputs)
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
                    _lastCommitTask = Commit(_buf, (int)bufLength, _uncommittedWatermarks, oldTrimWatermarks, outputs);
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


            internal void SendCheckpointToRecoverFrom(byte[] buf, int length, LogReader checkpointStream)
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


        /**
         * This contains information associated with a given machine
         **/
        internal class MachineState
        {
            public long ShardID { get; set; }
            public long LastCommittedCheckpoint { get; set; }
            public long LastLogFile { get; set; }
            public Committer Committer { get; set; }
            public ConcurrentDictionary<string, InputConnectionRecord> Inputs { get; set; }
            public ConcurrentDictionary<string, OutputConnectionRecord> Outputs { get; set; }
        }


        public class AmbrosiaOutput : IAsyncVertexOutputEndpoint
        {
            AmbrosiaRuntime myRuntime;
            string _typeOfEndpoint; // Data or control endpoint

            public AmbrosiaOutput(AmbrosiaRuntime inRuntime,
                                 string typeOfEndpoint) : base()
            {
                myRuntime = inRuntime;
                _typeOfEndpoint = typeOfEndpoint;
            }

            public void Dispose()
            {
            }

            public async Task ToInputAsync(IVertexInputEndpoint p, CancellationToken token)
            {
                await Task.Yield();
                throw new NotImplementedException();
            }

            public async Task ToStreamAsync(Stream stream, string otherProcess, string otherEndpoint, CancellationToken token)
            {
                if (_typeOfEndpoint == "data")
                {
                    await myRuntime.ToDataStreamAsync(stream, otherProcess, token);
                }
                else
                {
                    await myRuntime.ToControlStreamAsync(stream, otherProcess, token);
                }
            }
        }

        public class AmbrosiaInput : IAsyncVertexInputEndpoint
        {
            AmbrosiaRuntime myRuntime;
            string _typeOfEndpoint; // Data or control endpoint

            public AmbrosiaInput(AmbrosiaRuntime inRuntime,
                                 string typeOfEndpoint) : base()
            {
                myRuntime = inRuntime;
                _typeOfEndpoint = typeOfEndpoint;
            }

            public void Dispose()
            {
            }

            public async Task FromOutputAsync(IVertexOutputEndpoint p, CancellationToken token)
            {
                await Task.Yield();
                throw new NotImplementedException();
            }

            public async Task FromStreamAsync(Stream stream, string otherProcess, string otherEndpoint, CancellationToken token)
            {
                if (_typeOfEndpoint == "data")
                {
                    await myRuntime.FromDataStreamAsync(stream, otherProcess, token);
                }
                else
                {
                    await myRuntime.FromControlStreamAsync(stream, otherProcess, token);
                }
            }
        }

        ConcurrentDictionary<string, InputConnectionRecord> _inputs;
        ConcurrentDictionary<string, OutputConnectionRecord> _outputs;
        internal int _localServiceReceiveFromPort;           // specifiable on the command line
        internal int _localServiceSendToPort;                // specifiable on the command line 
        internal string _serviceName;  // specifiable on the command line
        internal string _serviceLogPath;
        internal string _logFileNameBase;
        public const string AmbrosiaDataInputsName = "Ambrosiadatain";
        public const string AmbrosiaControlInputsName = "Ambrosiacontrolin";
        public const string AmbrosiaDataOutputsName = "Ambrosiadataout";
        public const string AmbrosiaControlOutputsName = "Ambrosiacontrolout";
        bool _persistLogs;
        bool _sharded;
        internal bool _createService;
        long _shardID;
        bool _runningRepro;
        long _currentVersion;
        long _upgradeToVersion;
        bool _upgrading;
        internal bool _restartWithRecovery;
        internal bool CheckpointingService { get; set; }

        // Constants for leading byte communicated between services;
        public const byte RPCByte = 0;
        public const byte attachToByte = 1;
        public const byte takeCheckpointByte = 2;
        public const byte CommitByte = 3;
        public const byte replayFromByte = 4;
        public const byte RPCBatchByte = 5;
        public const byte PingByte = 6;
        public const byte PingReturnByte = 7;
        public const byte checkpointByte = 8;
        public const byte InitalMessageByte = 9;
        public const byte upgradeTakeCheckpointByte = 10;
        public const byte takeBecomingPrimaryCheckpointByte = 11;
        public const byte upgradeServiceByte = 12;
        public const byte CountReplayableRPCBatchByte = 13;
        public const byte trimToByte = 14;
        public const byte becomingPrimaryByte = 15;

        CRAClientLibrary _coral;

        // Connection to local service
        NetworkStream _localServiceReceiveFromStream;
        NetworkStream _localServiceSendToStream;

        // Precommit buffers used for writing things to append blobs
        Committer _committer;

        // Azure storage clients
        string _storageConnectionString;
        CloudStorageAccount _storageAccount;
        CloudTableClient _tableClient;

        // Azure table for service instance metadata information
        CloudTable _serviceInstanceTable;
        long _lastCommittedCheckpoint;

        // Azure blob for writing commit log and checkpoint
        LogWriter _checkpointWriter;

        // true when this service is in an active/active configuration. False if set to single node
        bool _activeActive;

        enum AARole { Primary, Secondary, Checkpointer };
        AARole _myRole;
        // Log size at which we start a new log file. This triggers a checkpoint, <= 0 if manual only checkpointing is done
        long _newLogTriggerSize;
        // The numeric suffix of the log file currently being read or written to
        long _lastLogFile;
        // A locking variable (with compare and swap) used to eliminate redundant log moves
        int _movingToNextLog = 0;
        // A handle to a file used for an upgrading secondary to bring down the primary and prevent primary promotion amongst secondaries.
        // As long as the write lock is held, no promotion can happen
        FileStream _killFileHandle = null;



        const int UnexpectedError = 0;
        const int VersionMismatch = 1;
        const int MissingCheckpoint = 2;
        const int MissingLog = 3;
        const int AzureOperationError = 4;
        const int LogWriteError = 5;

        internal void OnError(int ErrNo, string ErrorMessage)
        {
            Console.WriteLine("FATAL ERROR " + ErrNo.ToString() + ": " + ErrorMessage);
            Console.Out.Flush();
            Console.Out.Flush();
            _coral.KillLocalWorker("");
        }

        /// <summary>
        /// Need a manually created backing field so it can be marked volatile.
        /// </summary>
        private volatile FlexReadBuffer backingFieldForLastReceivedCheckpoint;

        internal FlexReadBuffer LastReceivedCheckpoint
        {
            get { return backingFieldForLastReceivedCheckpoint; }
            set
            {
                backingFieldForLastReceivedCheckpoint = value;
            }
        }

        internal long _lastReceivedCheckpointSize;

        bool _recovering;
        internal bool Recovering
        {
            get { return _recovering; }
            set { _recovering = value; }
        }

        /// <summary>
        /// Need a manually created backing field so it can be marked volatile.
        /// </summary>
        private volatile FlexReadBuffer backingFieldForServiceInitializationMessage;

        internal FlexReadBuffer ServiceInitializationMessage
        {
            get { return backingFieldForServiceInitializationMessage; }
            set
            {
                backingFieldForServiceInitializationMessage = value;
            }
        }

        // Hack for enabling fast IP6 loopback in Windows on .NET
        const int SIO_LOOPBACK_FAST_PATH = (-1744830448);

        void SetupLocalServiceStreams()
        {
            // Note that the local service must setup the listener and sender in reverse order or there will be a deadlock
            // First establish receiver - Use fast IP6 loopback
            Byte[] optionBytes = BitConverter.GetBytes(1);
#if _WINDOWS
            Socket mySocket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
            mySocket.IOControl(SIO_LOOPBACK_FAST_PATH, optionBytes, null);
            var ipAddress = IPAddress.IPv6Loopback;
#else
            Socket mySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var ipAddress = IPAddress.Loopback;
#endif

            var myReceiveEP = new IPEndPoint(ipAddress, _localServiceReceiveFromPort);
            mySocket.Bind(myReceiveEP);
            mySocket.Listen(1);
            var socket = mySocket.Accept();
            _localServiceReceiveFromStream = new NetworkStream(socket);

#if _WINDOWS
            // Now establish sender - Also use fast IP6 loopback
            mySocket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
            mySocket.IOControl(SIO_LOOPBACK_FAST_PATH, optionBytes, null);
#else
            mySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
#endif
            while (true)
            {
                try
                {
#if _WINDOWS
                    mySocket.Connect(IPAddress.IPv6Loopback, _localServiceSendToPort);
#else
                    mySocket.Connect(IPAddress.Loopback, _localServiceSendToPort);
#endif
                    break;
                }
                catch { }
            }
            TcpClient tcpSendToClient = new TcpClient();
            tcpSendToClient.Client = mySocket;
            _localServiceSendToStream = tcpSendToClient.GetStream();
        }

        private void SetupAzureConnections()
        {
            try
            {
                _storageAccount = CloudStorageAccount.Parse(_storageConnectionString);
                _tableClient = _storageAccount.CreateCloudTableClient();
                _serviceInstanceTable = _tableClient.GetTableReference(_serviceName);
                if ((_storageAccount == null) || (_tableClient == null) || (_serviceInstanceTable == null))
                {
                    OnError(AzureOperationError, "Error setting up initial connection to Azure");
                }
            }
            catch
            {
                OnError(AzureOperationError, "Error setting up initial connection to Azure");
            }
        }

        private const uint FILE_FLAG_NO_BUFFERING = 0x20000000;

        private void PrepareToRecoverOrStart()
        {
            IPAddress localIPAddress = Dns.GetHostEntry("localhost").AddressList[0];
            LogWriter.CreateDirectoryIfNotExists(LogDirectory(_currentVersion));
            _logFileNameBase = LogFileNameBase(_currentVersion);
            SetupLocalServiceStreams();
            if (!_runningRepro)
            {
                SetupAzureConnections();
            }
            ServiceInitializationMessage = null;
            Thread localListenerThread = new Thread(() => LocalListener()) { IsBackground = true };
            localListenerThread.Start();
        }

        private async Task CheckForUpgradeAsync()
        {
            while (true)
            {
                await Task.Delay(1000);
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        LockKillFile();
                        // If we reach here, we have the lock and definitely don't need to commit suicide
                        ReleaseAndTryCleanupKillFile();
                        break;
                    }
                    catch (Exception e)
                    {
                        // Maybe we are tying to upgrade, but maybe someone else is checking. Try 3 times before committing suicide
                        if (i==2)
                        {
                            // Failed 3 times. Commit suicide
                            OnError(0, "Upgrading. Must commit suicide since I'm the primary");
                        }
                    }
                }
            }
        }

        private async Task RecoverOrStartAsync(long checkpointToLoad = -1,
                                               bool testUpgrade = false)
        {
            CheckpointingService = false;
            Recovering = false;
            PrepareToRecoverOrStart();
            if (!_runningRepro && (_createService || !_sharded))
            {
                RuntimeChecksOnProcessStart();
            }

            // Determine if we are recovering
            if (!_createService)
            {
                Recovering = true;
                _restartWithRecovery = true;
                MachineState state = new MachineState();
                state.ShardID = _shardID;
                await RecoverAsync(state, checkpointToLoad, testUpgrade);
                _lastCommittedCheckpoint = state.LastCommittedCheckpoint;
                _lastLogFile = state.LastLogFile;
                _inputs = state.Inputs;
                _outputs = state.Outputs;
                _committer = state.Committer;
                await PrepareToBecomePrimaryAsync();
                Recovering = false;
            }
            else
            {
                await StartAsync();
            }
        }

        private async Task RecoverAsync(MachineState state, long checkpointToLoad = -1, bool testUpgrade = false)
        {
            if (!_runningRepro)
            {
                // We are recovering - find the last committed checkpoint
                state.LastCommittedCheckpoint = long.Parse(RetrieveServiceInfo(InfoTitle("LastCommittedCheckpoint", state.ShardID)));
            }
            else
            {
                // We are running a repro
                state.LastCommittedCheckpoint = checkpointToLoad;
            }
            // Start from the log file associated with the last committed checkpoint
            state.LastLogFile = state.LastCommittedCheckpoint;
            if (_activeActive)
            {
                if (!_runningRepro)
                {
                    // Determines the role as either secondary or checkpointer. If its a checkpointer, _commitBlobWriter holds the write lock on the last checkpoint
                    _lastCommittedCheckpoint = state.LastCommittedCheckpoint;
                    DetermineRole();
                }
                else
                {
                    // We are running a repro. Act as a secondary
                    _myRole = AARole.Secondary;
                }
            }

            using (LogReader checkpointStream = new LogReader(CheckpointName(state.LastCommittedCheckpoint, -1, state.ShardID)))
            {
                // recover the checkpoint - Note that everything except the replay data must have been written successfully or we
                // won't think we have a valid checkpoint here. Since we can only be the secondary or checkpointer, the committer doesn't write to the replay log
                // Recover committer
                state.Committer = new Committer(_localServiceSendToStream, _persistLogs, this, -1, checkpointStream);
                // Recover input connections
                state.Inputs = state.Inputs.AmbrosiaDeserialize(checkpointStream);
                // Recover output connections
                state.Outputs = state.Outputs.AmbrosiaDeserialize(checkpointStream, this);
                UnbufferNonreplayableCalls(state.Outputs);
                // Restore new service from checkpoint
                var serviceCheckpoint = new FlexReadBuffer();
                FlexReadBuffer.Deserialize(checkpointStream, serviceCheckpoint);
                state.Committer.SendCheckpointToRecoverFrom(serviceCheckpoint.Buffer, serviceCheckpoint.Length, checkpointStream);
            }

            using (LogReader replayStream = new LogReader(LogFileName(state.LastLogFile, -1, state.ShardID)))
            {
                if (_myRole == AARole.Secondary && !_runningRepro)
                {
                    // If this is a secondary, set up the detector to detect when this instance becomes the primary
                    var t = DetectBecomingPrimaryAsync(state);
                }
                if (testUpgrade)
                {
                    // We are actually testing an upgrade. Must upgrade the service before replay
                    state.Committer.SendUpgradeRequest();
                }
                await ReplayAsync(replayStream, state);
            }
        }

        private async Task PrepareToBecomePrimaryAsync()
        {
            var readVersion = long.Parse(RetrieveServiceInfo(InfoTitle("CurrentVersion")));
            if (_currentVersion != readVersion)
            {

                OnError(VersionMismatch, "Version changed during recovery: Expected " + _currentVersion + " was: " + readVersion.ToString());
            }
            if (_upgrading)
            {
                MoveServiceToUpgradeDirectory();
            }
            // Now becoming the primary. Moving to next log file since the current one may have junk at the end.
            bool wasUpgrading = _upgrading;
            var oldFileHandle = await MoveServiceToNextLogFileAsync(false, true);
            if (wasUpgrading)
            {
                // Successfully wrote out our new first checkpoint in the upgraded version, can now officially take the version upgrade
                InsertOrReplaceServiceInfoRecord(InfoTitle("CurrentVersion"), _upgradeToVersion.ToString());
                // We have now completed the upgrade and may release the old file lock.
                oldFileHandle.Dispose();
                // Moving to the next file means the first log file is empty, but it immediately causes failures of all old secondaries.
                await MoveServiceToNextLogFileAsync();
            }

            if (_activeActive)
            {
                // Start task to periodically check if someone's trying to upgrade
                (new Task(() => CheckForUpgradeAsync())).Start();
            }
        }

        private async Task StartAsync()
        {
            // We are starting for the first time. This is the primary
            _restartWithRecovery = false;
            _lastCommittedCheckpoint = 0;
            _lastLogFile = 0;
            _inputs = new ConcurrentDictionary<string, InputConnectionRecord>();
            _outputs = new ConcurrentDictionary<string, OutputConnectionRecord>();
            _serviceInstanceTable.CreateIfNotExistsAsync().Wait();

            _myRole = AARole.Primary;

            _checkpointWriter = null;
            _committer = new Committer(_localServiceSendToStream, _persistLogs, this);
            Connect(_serviceName, AmbrosiaDataOutputsName, _serviceName, AmbrosiaDataInputsName);
            Connect(_serviceName, AmbrosiaControlOutputsName, _serviceName, AmbrosiaControlInputsName);
            await MoveServiceToNextLogFileAsync(true, true);
            InsertOrReplaceServiceInfoRecord(InfoTitle("CurrentVersion"), _currentVersion.ToString());
            if (_activeActive)
            {
                // Start task to periodically check if someone's trying to upgrade
                (new Task(() => CheckForUpgradeAsync())).Start();
            }
        }

        private void UnbufferNonreplayableCalls(ConcurrentDictionary<string, OutputConnectionRecord> outputs)
        {
            foreach (var outputRecord in outputs)
            {
                var newLastSeqNo = outputRecord.Value.BufferedOutput.TrimAndUnbufferNonreplayableCalls(outputRecord.Value.TrimTo, outputRecord.Value.ReplayableTrimTo);
                if (newLastSeqNo != -1)
                {
                    outputRecord.Value.LastSeqNoFromLocalService = newLastSeqNo;
                }
            }
        }

        internal void MoveServiceToUpgradeDirectory()
        {
            LogWriter.CreateDirectoryIfNotExists(_serviceLogPath + ServiceName() + "_" + _upgradeToVersion);
            _logFileNameBase = LogFileNameBase(_upgradeToVersion);
        }

        public CRAErrorCode Connect(string fromProcessName, string fromEndpoint, string toProcessName, string toEndpoint)
        {
            foreach (var conn in _coral.GetConnectionsFromVertex(fromProcessName))
            {
                if (conn.FromEndpoint.Equals(fromEndpoint) && conn.ToVertex.Equals(toProcessName) && conn.ToEndpoint.Equals(toEndpoint))
                    return CRAErrorCode.Success;
            }
            return _coral.Connect(fromProcessName, fromEndpoint, toProcessName, toEndpoint);
        }

        private string ServiceName(long shardID = -1)
        {
            if (_sharded)
            {
                if (shardID == -1)
                {
                    shardID = _shardID;
                }
                return _serviceName + "-" + shardID.ToString();
            }
            return _serviceName;
        }

        private string RootDirectory(long version = -1)
        {
            if (version == -1)
            {
                if (_upgrading)
                {
                    version = _upgradeToVersion;
                }
                else
                {
                    version = _currentVersion;
                }
            }

            return _serviceLogPath + _serviceName + "_" + version;
        }

        private string LogDirectory(long version = -1, long shardID = -1)
        {
            string shard = "";
            if (_sharded)
            {
                if (shardID == -1)
                {
                    shardID = _shardID;
                }
                shard = shardID.ToString();
            }

            return Path.Combine(RootDirectory(version), shard);
        }

        private string LogFileNameBase(long version = -1, long shardID = -1)
        {
            return Path.Combine(LogDirectory(version, shardID), "server");
        }

        private string CheckpointName(long checkpoint, long version = -1, long shardID = -1)
        {
            return LogFileNameBase(version, shardID) + "chkpt" + checkpoint.ToString();
        }

        private string LogFileName(long logFile, long version = -1, long shardID = -1)
        {
            return LogFileNameBase(version, shardID) + "log" + logFile.ToString();
        }

        private LogWriter CreateNextOldVerLogFile()
        {
            if (LogWriter.FileExists(LogFileName(_lastLogFile + 1, _currentVersion)))
            {
                File.Delete(LogFileName(_lastLogFile + 1, _currentVersion));
            }
            LogWriter retVal = null;
            try
            {
                retVal = new LogWriter(LogFileName(_lastLogFile + 1, _currentVersion), 1024 * 1024, 6);
            }
            catch (Exception e)
            {
                OnError(0, "Error opening next log file:" + e.ToString());
            }
            return retVal;
        }

        // Used to create a kill file meant to being down primaries and prevent promotion. Promotion prevention
        // lasts until the returned file handle is released.
        private void LockKillFile()
        {
            _killFileHandle = new FileStream(_logFileNameBase + "killFile", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read & ~FileShare.Inheritable);
        }

        private void ReleaseAndTryCleanupKillFile()
        {
            _killFileHandle.Dispose();
            _killFileHandle = null;
            try
            {
                // Try to delete the file. Someone may beat us to it.
                File.Delete(_logFileNameBase + "killFile");
            }
            catch (Exception e)
            {
            }
        }

        private LogWriter CreateNextLogFile()
        {
            if (LogWriter.FileExists(LogFileName(_lastLogFile + 1)))
            {
                File.Delete(LogFileName(_lastLogFile + 1));
            }
            LogWriter retVal = null;
            try
            {
                retVal = new LogWriter(LogFileName(_lastLogFile + 1), 1024 * 1024, 6);
            }
            catch (Exception e)
            {
                OnError(0, "Error opening next log file:" + e.ToString());
            }
            return retVal;
        }

        private string InfoTitle(string prefix, long shardID = -1)
        {
            var file = prefix;
            if (_sharded)
            {
                if (shardID == -1)
                {
                    shardID = _shardID;
                }
                file += shardID.ToString();
            }
            return file;
        }

        // Closes out the old log file and starts a new one. Takes checkpoints if this instance should
        private async Task<LogWriter> MoveServiceToNextLogFileAsync(bool firstStart = false, bool becomingPrimary = false)
        {
            // Move to the next log file. By doing this before checkpointing, we may end up skipping a checkpoint file (failure during recovery). 
            // This is ok since we recover from the first committed checkpoint and will just skip empty log files during replay. 
            // This also protects us from a failed upgrade, which is why the file is created in both directories on upgrade, and why the lock on upgrade is held until successful upgrade or failure.
            await _committer.SleepAsync();
            var nextLogHandle = CreateNextLogFile();
            LogWriter oldVerLogHandle = null;
            if (_upgrading)
            {
                oldVerLogHandle = CreateNextOldVerLogFile();
            }
            _lastLogFile++;
            InsertOrReplaceServiceInfoRecord(InfoTitle("LastLogFile"), _lastLogFile.ToString());
            _committer.SwitchLogStreams(nextLogHandle);
            if (!firstStart && _activeActive && !_upgrading && becomingPrimary)
            {
                // In this case, we want the local service to become primary without taking a checkpoint
                _committer.SendBecomePrimaryRequest();
            }
            else if (firstStart || !_activeActive || _upgrading)
            {
                // take the checkpoint associated with the beginning of the new log and let go of the log file lock
                _committer.QuiesceServiceWithSendCheckpointRequest(_upgrading, becomingPrimary);
                _upgrading = false;
                if (firstStart)
                {
                    while (ServiceInitializationMessage == null) { await Task.Yield(); };
                    await _committer.AddInitialRowAsync(ServiceInitializationMessage);
                }
                await CheckpointAsync();
                _checkpointWriter.Dispose();
                _checkpointWriter = null;
            }
            await _committer.WakeupAsync();
            // This is a safe place to try to commit, because if this is called during recovery,
            // it's after replace and moving to the next log file. Note that this will also have the effect
            // of shaking loose the initialization message, ensuring liveliness.
            await _committer.TryCommitAsync(_outputs);
            return oldVerLogHandle;
        }

        //==============================================================================================================
        // Insance compete over write permission for LOG file & CheckPoint file
        private void DetermineRole()
        {
            if (_upgrading)
            {
                _myRole = AARole.Secondary;
                return;
            }
            try
            {
                // Compete for Checkpoint Write Permission
                _checkpointWriter = new LogWriter(CheckpointName(_lastCommittedCheckpoint), 1024 * 1024, 6, true);
                _myRole = AARole.Checkpointer; // I'm a checkpointing secondary
                var oldCheckpoint = _lastCommittedCheckpoint;
                _lastCommittedCheckpoint = long.Parse(RetrieveServiceInfo(InfoTitle("LastCommittedCheckpoint")));
                if (oldCheckpoint != _lastCommittedCheckpoint)
                {
                    _checkpointWriter.Dispose();
                    throw new Exception("We got a handle on an old checkpoint. The checkpointer was alive when this instance started");
                }
            }
            catch
            {
                _checkpointWriter = null;
                _myRole = AARole.Secondary; // I'm a secondary
            }
        }

        internal async Task DetectBecomingPrimaryAsync(MachineState state)
        {
            // keep trying to take the write permission on LOG file 
            // LOG write permission acquired only in case primary failed (is down)
            while (true)
            {
                try
                {
                    var oldLastLogFile = state.LastLogFile;
                    // Compete for log write permission - non destructive open for write - open for append
                    var lastLogFileStream = new LogWriter(LogFileName(oldLastLogFile), 1024 * 1024, 6, true);
                    if (long.Parse(RetrieveServiceInfo(InfoTitle("LastLogFile"))) != oldLastLogFile)
                    {
                        // We got an old log. Try again
                        lastLogFileStream.Dispose();
                        throw new Exception();
                    }
                    // We got the lock! Set things up so we let go of the lock at the right moment
                    // But first check if we got the lock because the version changed, in which case, we should commit suicide
                    var readVersion = long.Parse(RetrieveServiceInfo(InfoTitle("CurrentVersion")));
                    if (_currentVersion != readVersion)
                    {

                        OnError(VersionMismatch, "Version changed during recovery: Expected " + _currentVersion + " was: " + readVersion.ToString());
                    }

                    // Before allowing the node to become primary in active/active, if we are not an upgrader, see if we are prevented by a kill file.
                    if (_activeActive && !_upgrading)
                    {
                        LockKillFile();
                        // If we reach here, we have the lock and can promote, otherwise an exception was thrown and we can't promote
                        ReleaseAndTryCleanupKillFile();
                    }

                    // Now we can really promote!
                    await state.Committer.SleepAsync();
                    state.Committer.SwitchLogStreams(lastLogFileStream);
                    await state.Committer.WakeupAsync();

                    _myRole = AARole.Primary;  // this will stop  and break the loop in the function  replayInput_Sec() 
                    Console.WriteLine("\n\nNOW I'm Primary\n\n");
                    // if we are an upgrader : Time to release the kill file lock and cleanup. Note that since we have the log lock
                    // everyone is prevented from promotion until we succeed or fail.
                    if (_upgrading && _activeActive)
                    {
                        Debug.Assert(_killFileHandle != null);
                        ReleaseAndTryCleanupKillFile();
                    }
                    return;
                }
                catch
                {
                    // Check if the version changed, in which case, we should commit suicide
                    var readVersion = long.Parse(RetrieveServiceInfo(InfoTitle("CurrentVersion")));
                    if (_currentVersion != readVersion)
                    {

                        OnError(VersionMismatch, "Version changed during recovery: Expected " + _currentVersion + " was: " + readVersion.ToString());
                    }
                    await Task.Delay(1000);
                }
            }
        }

        private async Task ReplayAsync(LogReader replayStream, MachineState state)
        {
            var tempBuf = new byte[100];
            var tempBuf2 = new byte[100];
            var headerBuf = new byte[Committer.HeaderSize];
            var headerBufStream = new MemoryStream(headerBuf);
            var committedInputDict = new Dictionary<string, LongPair>();
            var trimDict = new Dictionary<string, long>();
            var detectedEOF = false;
            var detectedEOL = false;
            var clearedCommitterWrite = false;
            // Keep replaying commits until we run out of replay data
            while (true)
            {
                long logRecordPos = replayStream.Position;
                int commitSize;
                try
                {
                    // First get commit ID and check for integrity
                    replayStream.ReadAllRequiredBytes(headerBuf, 0, Committer.HeaderSize);
                    headerBufStream.Position = 0;
                    var commitID = headerBufStream.ReadIntFixed();
                    if (commitID != state.Committer.CommitID)
                    {
                        throw new Exception("Committer didn't match. Must be incomplete record");
                    }
                    // Get commit page length
                    commitSize = headerBufStream.ReadIntFixed();
                    var checkBytes = headerBufStream.ReadLongFixed();
                    var writeSeqID = headerBufStream.ReadLongFixed();
                    if (writeSeqID != state.Committer._nextWriteID)
                    {
                        throw new Exception("Out of order page. Must be incomplete record");
                    }
                    // Remove header
                    commitSize -= Committer.HeaderSize;
                    if (commitSize > tempBuf.Length)
                    {
                        tempBuf = new byte[commitSize];
                    }
                    replayStream.Read(tempBuf, 0, commitSize);
                    // Perform integrity check
                    long checkBytesCalc = state.Committer.CheckBytes(tempBuf, 0, commitSize);
                    if (checkBytesCalc != checkBytes)
                    {
                        throw new Exception("Integrity check failed for page. Must be incomplete record");
                    }

                    // Read changes in input consumption progress to reflect in _inputs
                    var watermarksToRead = replayStream.ReadInt();
                    committedInputDict.Clear();
                    for (int i = 0; i < watermarksToRead; i++)
                    {
                        var inputNameSize = replayStream.ReadInt();
                        if (inputNameSize > tempBuf2.Length)
                        {
                            tempBuf2 = new byte[inputNameSize];
                        }
                        replayStream.Read(tempBuf2, 0, inputNameSize);
                        var inputName = Encoding.UTF8.GetString(tempBuf2, 0, inputNameSize);
                        var newLongPair = new LongPair();
                        newLongPair.First = replayStream.ReadLongFixed();
                        newLongPair.Second = replayStream.ReadLongFixed();
                        committedInputDict[inputName] = newLongPair;
                    }
                    // Read changes in trim to perform and reflect in _outputs
                    watermarksToRead = replayStream.ReadInt();
                    trimDict.Clear();
                    for (int i = 0; i < watermarksToRead; i++)
                    {
                        var inputNameSize = replayStream.ReadInt();
                        if (inputNameSize > tempBuf2.Length)
                        {
                            tempBuf2 = new byte[inputNameSize];
                        }
                        replayStream.Read(tempBuf2, 0, inputNameSize);
                        var inputName = Encoding.UTF8.GetString(tempBuf2, 0, inputNameSize);
                        long seqNo = replayStream.ReadLongFixed();
                        trimDict[inputName] = seqNo;
                    }
                }
                catch
                {
                    // Couldn't recover replay segment. Could be for a number of reasons.
                    if (!_activeActive || detectedEOL)
                    {
                        // Leave replay and continue recovery.
                        break;
                    }
                    if (detectedEOF)
                    {
                        // Move to the next log file for reading only. We may need to take a checkpoint
                        state.LastLogFile++;
                        replayStream.Dispose();
                        if (!LogWriter.FileExists(LogFileName(state.LastLogFile, -1, state.ShardID)))
                        {
                            OnError(MissingLog, "Missing log in replay " + state.LastLogFile.ToString());
                        }
                        replayStream = new LogReader(LogFileName(state.LastLogFile, -1, state.ShardID));
                        if (_myRole == AARole.Checkpointer)
                        {
                            // take the checkpoint associated with the beginning of the new log
                            await state.Committer.SleepAsync();
                            state.Committer.QuiesceServiceWithSendCheckpointRequest();
                            await CheckpointAsync();
                            await state.Committer.WakeupAsync();
                        }
                        detectedEOF = false;
                        continue;
                    }
                    var myRoleBeforeEOLChecking = _myRole;
                    replayStream.Position = logRecordPos;
                    var newLastLogFile = state.LastLogFile;
                    if (_runningRepro)
                    {
                        if (LogWriter.FileExists(LogFileName(state.LastLogFile + 1, -1, state.ShardID)))
                        {
                            // If there is a next file, then move to it
                            newLastLogFile = state.LastLogFile + 1;
                        }
                    }
                    else
                    {
                        newLastLogFile = long.Parse(RetrieveServiceInfo(InfoTitle("LastLogFile", state.ShardID)));
                    }
                    if (newLastLogFile > state.LastLogFile) // a new log file has been written
                    {
                        // Someone started a new log. Try to read the last record again and then move to next file
                        detectedEOF = true;
                        continue;
                    }
                    if (myRoleBeforeEOLChecking == AARole.Primary)
                    {
                        // Became the primary and the current file is the end of the log. Make sure we read the whole file.
                        detectedEOL = true;
                        continue;
                    }
                    // The remaining case is that we hit the end of log, but someone is still writing to this file. Wait and try to read again, or kill the primary if we are trying to upgrade in an active/active scenario
                    if (_upgrading && _activeActive && _killFileHandle == null)
                    {
                        // We need to write and hold the lock on the kill file. Recovery will continue until the primary dies and we have
                        // fully processed the log.
                        while (true)
                        {
                            try
                            {
                                LockKillFile();
                                break;
                            }
                            catch (Exception e)
                            {
                                // Someone may be checking promotability. Keep trying until successful
                            }
                        }
                    }
                    await Task.Delay(1000);
                    continue;
                }
                // Successfully read an entire replay segment. Go ahead and process for recovery
                foreach (var kv in committedInputDict)
                {
                    InputConnectionRecord inputConnectionRecord;
                    if (!state.Inputs.TryGetValue(kv.Key, out inputConnectionRecord))
                    {
                        // Create input record and add it to the dictionary
                        inputConnectionRecord = new InputConnectionRecord();
                        state.Inputs[kv.Key] = inputConnectionRecord;
                    }
                    inputConnectionRecord.LastProcessedID = kv.Value.First;
                    inputConnectionRecord.LastProcessedReplayableID = kv.Value.Second;
                    OutputConnectionRecord outputConnectionRecord;
                    // this lock prevents conflict with output arriving from the local service during replay
                    lock (state.Outputs)
                    {
                        if (!state.Outputs.TryGetValue(kv.Key, out outputConnectionRecord))
                        {
                            outputConnectionRecord = new OutputConnectionRecord(this);
                            state.Outputs[kv.Key] = outputConnectionRecord;
                        }
                    }
                    // this lock prevents conflict with output arriving from the local service during replay and ensures maximal cleaning
                    lock (outputConnectionRecord)
                    {
                        outputConnectionRecord.RemoteTrim = Math.Max(kv.Value.First, outputConnectionRecord.RemoteTrim);
                        outputConnectionRecord.RemoteTrimReplayable = Math.Max(kv.Value.Second, outputConnectionRecord.RemoteTrimReplayable);
                        if (outputConnectionRecord.ControlWorkQ.IsEmpty)
                        {
                            outputConnectionRecord.ControlWorkQ.Enqueue(-2);
                        }
                    }
                }
                // Do the actual work on the local service
                _localServiceSendToStream.Write(headerBuf, 0, Committer.HeaderSize);
                _localServiceSendToStream.Write(tempBuf, 0, commitSize);
                // Trim the outputs. Should clean as aggressively as during normal operation
                foreach (var kv in trimDict)
                {
                    OutputConnectionRecord outputConnectionRecord;
                    // this lock prevents conflict with output arriving from the local service during replay
                    lock (state.Outputs)
                    {
                        if (!state.Outputs.TryGetValue(kv.Key, out outputConnectionRecord))
                        {
                            outputConnectionRecord = new OutputConnectionRecord(this);
                            state.Outputs[kv.Key] = outputConnectionRecord;
                        }
                    }
                    // this lock prevents conflict with output arriving from the local service during replay and ensures maximal cleaning
                    lock (outputConnectionRecord)
                    {
                        outputConnectionRecord.TrimTo = kv.Value;
                        outputConnectionRecord.ReplayableTrimTo = kv.Value;
                        outputConnectionRecord.BufferedOutput.Trim(kv.Value, ref outputConnectionRecord.placeInOutput);
                    }
                }
                // If this is the first replay segment, it invalidates the contents of the committer, which must be cleared.
                if (!clearedCommitterWrite)
                {
                    state.Committer.ClearNextWrite();
                    clearedCommitterWrite = true;
                }
                // bump up the write ID in the committer in preparation for reading or writing the next page
                state.Committer._nextWriteID++;
            }
        }

        // Thread for listening to the local service
        private void LocalListener()
        {
            try
            {
                var localServiceBuffer = new FlexReadBuffer();
                var batchServiceBuffer = new FlexReadBuffer();
                var bufferSize = 128 * 1024;
                byte[] bytes = new byte[bufferSize];
                byte[] bytesBak = new byte[bufferSize];
                while (_outputs == null) { Thread.Yield(); }
                while (true)
                {
                    // Do an async message read. Note that the async aspect of this is slow.
                    FlexReadBuffer.Deserialize(_localServiceReceiveFromStream, localServiceBuffer);
                    ProcessSyncLocalMessage(ref localServiceBuffer, batchServiceBuffer);
                    /* Disabling because of BUGBUG. Eats checkpoint bytes in some circumstances before checkpointer can deal with it.
                                        // Process more messages from the local service if available before going async again, doing this here because
                                        // not all language shims will be good citizens here, and we may need to process small messages to avoid inefficiencies
                                        // in LAR.
                                        int curPosInBuffer = 0;
                                        int readBytes = 0;
                                        while (readBytes != 0 || _localServiceReceiveFromStream.DataAvailable)
                                        {
                                            // Read data into buffer to avoid lock contention of reading directly from the stream
                                            while ((_localServiceReceiveFromStream.DataAvailable && readBytes < bufferSize) || !bytes.EnoughBytesForReadBufferedInt(0, readBytes))
                                            {
                                                readBytes += _localServiceReceiveFromStream.Read(bytes, readBytes, bufferSize - readBytes);
                                            }
                                            // Continue loop as long as we can meaningfully read a message length
                                            var memStream = new MemoryStream(bytes, 0, readBytes);
                                            while (bytes.EnoughBytesForReadBufferedInt(curPosInBuffer, readBytes - curPosInBuffer))
                                            {
                                                // Read the length of the next message
                                                var messageSize = memStream.ReadInt();
                                                var messageSizeSize = StreamCommunicator.IntSize(messageSize);
                                                memStream.Position -= messageSizeSize;
                                                if (curPosInBuffer + messageSizeSize + messageSize > readBytes)
                                                {
                                                    // didn't read the full message into the buffer. It must be torn
                                                    if (messageSize + messageSizeSize > bufferSize)
                                                    {
                                                        // Buffer isn't big enough to hold the whole torn event even if empty. Increase the buffer size so the message can fit.
                                                        bufferSize = messageSize + messageSizeSize;
                                                        var newBytes = new byte[bufferSize];
                                                        Buffer.BlockCopy(bytes, curPosInBuffer, newBytes, 0, readBytes - curPosInBuffer);
                                                        bytes = newBytes;
                                                        bytesBak = new byte[bufferSize];
                                                        readBytes -= curPosInBuffer;
                                                        curPosInBuffer = 0;
                                                    }
                                                    break;
                                                }
                                                else
                                                {
                                                    // Count this message since it is fully in the buffer
                                                    FlexReadBuffer.Deserialize(memStream, localServiceBuffer);
                                                    ProcessSyncLocalMessage(ref localServiceBuffer, batchServiceBuffer);
                                                    curPosInBuffer += messageSizeSize + messageSize;
                                                }
                                            }
                                            memStream.Dispose();
                                            // Shift torn message to the beginning unless it is the first one
                                            if (curPosInBuffer > 0)
                                            {
                                                Buffer.BlockCopy(bytes, curPosInBuffer, bytesBak, 0, readBytes - curPosInBuffer);
                                                var tempBytes = bytes;
                                                bytes = bytesBak;
                                                bytesBak = tempBytes;
                                                readBytes -= curPosInBuffer;
                                                curPosInBuffer = 0;
                                            }
                                        }  */
                }
            }
            catch (Exception e)
            {
                OnError(AzureOperationError, "Error in local listener data stream:" + e.ToString());
                return;
            }
        }

        private void MoveServiceToNextLogFileSimple()
        {
            MoveServiceToNextLogFileAsync().Wait();
        }

        private void ProcessSyncLocalMessage(ref FlexReadBuffer localServiceBuffer, FlexReadBuffer batchServiceBuffer)
        {
            var sizeBytes = localServiceBuffer.LengthLength;
            Task createCheckpointTask = null;
            // Process the Async message
            switch (localServiceBuffer.Buffer[sizeBytes])
            {
                case takeCheckpointByte:
                    // Handle take checkpoint messages - This is here for testing
                    createCheckpointTask = new Task(new Action(MoveServiceToNextLogFileSimple));
                    createCheckpointTask.Start();
                    localServiceBuffer.ResetBuffer();
                    break;

                case checkpointByte:
                    _lastReceivedCheckpointSize = StreamCommunicator.ReadBufferedLong(localServiceBuffer.Buffer, sizeBytes + 1);
                    Console.WriteLine("Reading a checkpoint {0} bytes", _lastReceivedCheckpointSize);
                    LastReceivedCheckpoint = localServiceBuffer;
                    // Block this thread until checkpointing is complete
                    while (LastReceivedCheckpoint != null) { Thread.Yield(); };
                    break;

                case attachToByte:
                    // Get dest string
                    var destination = Encoding.UTF8.GetString(localServiceBuffer.Buffer, sizeBytes + 1, localServiceBuffer.Length - sizeBytes - 1);
                    localServiceBuffer.ResetBuffer();

                    if (!_runningRepro)
                    {
                        Console.WriteLine("Attaching to {0}", destination);
                        var connectionResult1 = Connect(ServiceName(), AmbrosiaDataOutputsName, destination, AmbrosiaDataInputsName);
                        var connectionResult2 = Connect(ServiceName(), AmbrosiaControlOutputsName, destination, AmbrosiaControlInputsName);
                        var connectionResult3 = Connect(destination, AmbrosiaDataOutputsName, ServiceName(), AmbrosiaDataInputsName);
                        var connectionResult4 = Connect(destination, AmbrosiaControlOutputsName, ServiceName(), AmbrosiaControlInputsName);
                        if ((connectionResult1 != CRAErrorCode.Success) || (connectionResult2 != CRAErrorCode.Success) ||
                            (connectionResult3 != CRAErrorCode.Success) || (connectionResult4 != CRAErrorCode.Success))
                        {
                            Console.WriteLine("Error attaching {0} to {1}", ServiceName(), destination);
                        }
                    }
                    break;

                case RPCBatchByte:
                    var restOfBatchOffset = sizeBytes + 1;
                    var memStream = new MemoryStream(localServiceBuffer.Buffer, restOfBatchOffset, localServiceBuffer.Length - restOfBatchOffset);
                    var numRPCs = memStream.ReadInt();
                    for (int i = 0; i < numRPCs; i++)
                    {
                        FlexReadBuffer.Deserialize(memStream, batchServiceBuffer);
                        ProcessRPC(batchServiceBuffer);
                    }
                    memStream.Dispose();
                    localServiceBuffer.ResetBuffer();
                    break;

                case InitalMessageByte:
                    // Process the Async RPC request
                    if (ServiceInitializationMessage != null)
                    {
                        OnError(0, "Getting second initialization message");
                    }
                    ServiceInitializationMessage = localServiceBuffer;
                    localServiceBuffer = new FlexReadBuffer();
                    break;

                case RPCByte:
                    ProcessRPC(localServiceBuffer);
                    // Now process any pending RPC requests from the local service before going async again
                    break;

                case PingByte:
                    // Write time into correct place in message
                    int destBytesSize = localServiceBuffer.Buffer.ReadBufferedInt(sizeBytes + 1);
                    memStream = new MemoryStream(localServiceBuffer.Buffer, localServiceBuffer.Length - 5 * sizeof(long), sizeof(long));
                    long time;
                    GetSystemTimePreciseAsFileTime(out time);
                    memStream.WriteLongFixed(time);
                    // Treat as RPC
                    ProcessRPC(localServiceBuffer);
                    memStream.Dispose();
                    break;

                case PingReturnByte:
                    // Write time into correct place in message
                    destBytesSize = localServiceBuffer.Buffer.ReadBufferedInt(sizeBytes + 1);
                    memStream = new MemoryStream(localServiceBuffer.Buffer, localServiceBuffer.Length - 2 * sizeof(long), sizeof(long));
                    GetSystemTimePreciseAsFileTime(out time);
                    memStream.WriteLongFixed(time);
                    // Treat as RPC
                    ProcessRPC(localServiceBuffer);
                    memStream.Dispose();
                    break;

                default:
                    // This one really should terminate the process; no recovery allowed.
                    OnError(0, "Illegal leading byte in local message");
                    break;
            }
        }

        int _lastShuffleDestSize = -1; // must be negative because self-messages are encoded with a destination size of 0
        byte[] _lastShuffleDest = new byte[20];
        OutputConnectionRecord _shuffleOutputRecord = null;

        bool EqualBytes(byte[] data1, int data1offset, byte[] data2, int elemsCompared)
        {
            for (int i = 0; i < elemsCompared; i++)
            {
                if (data1[i + data1offset] != data2[i])
                {
                    return false;
                }
            }
            return true;
        }

        private void ProcessRPC(FlexReadBuffer RpcBuffer)
        {
            var sizeBytes = RpcBuffer.LengthLength;
            int destBytesSize = RpcBuffer.Buffer.ReadBufferedInt(sizeBytes + 1);
            var destOffset = sizeBytes + 1 + StreamCommunicator.IntSize(destBytesSize);
            // Check to see if the _lastShuffleDest is the same as the one to process. Caching here avoids significant overhead.
            if (_lastShuffleDest == null || (_lastShuffleDestSize != destBytesSize) || !EqualBytes(RpcBuffer.Buffer, destOffset, _lastShuffleDest, destBytesSize))
            {
                // Find the appropriate connection record
                string destination;
                if (_lastShuffleDest.Length < destBytesSize)
                {
                    _lastShuffleDest = new byte[destBytesSize];
                }
                Buffer.BlockCopy(RpcBuffer.Buffer, destOffset, _lastShuffleDest, 0, destBytesSize);
                _lastShuffleDestSize = destBytesSize;
                destination = Encoding.UTF8.GetString(RpcBuffer.Buffer, destOffset, destBytesSize);
                // locking to avoid conflict with stream reconnection immediately after replay and trim during replay
                lock (_outputs)
                {
                    // During replay, the output connection won't exist if this is the first message ever and no trim record has been processed yet.
                    if (!_outputs.TryGetValue(destination, out _shuffleOutputRecord))
                    {
                        _shuffleOutputRecord = new OutputConnectionRecord(this);
                        _outputs[destination] = _shuffleOutputRecord;
                    }
                }
            }

            int restOfRPCOffset = destOffset + destBytesSize;
            int restOfRPCMessageSize = RpcBuffer.Length - restOfRPCOffset;
            var totalSize = StreamCommunicator.IntSize(1 + restOfRPCMessageSize) +
                            1 + restOfRPCMessageSize;

            // lock to avoid conflict and ensure maximum memory cleaning during replay. No possible conflict during primary operation
            lock (_shuffleOutputRecord)
            {
                // Buffer the output if it is at or beyond the replay or trim point (during recovery).
                if ((_shuffleOutputRecord.LastSeqNoFromLocalService + 1 >= _shuffleOutputRecord.ReplayFrom) &&
                    (_shuffleOutputRecord.LastSeqNoFromLocalService + 1 >= _shuffleOutputRecord.ReplayableTrimTo))
                {
                    var writablePage = _shuffleOutputRecord.BufferedOutput.GetWritablePage(totalSize, _shuffleOutputRecord.LastSeqNoFromLocalService + 1);
                    writablePage.HighestSeqNo = _shuffleOutputRecord.LastSeqNoFromLocalService + 1;

                    var methodID = RpcBuffer.Buffer.ReadBufferedInt(restOfRPCOffset + 1);
                    if (RpcBuffer.Buffer[restOfRPCOffset + 1 + StreamCommunicator.IntSize(methodID)] != (byte)RpcTypes.RpcType.Impulse)
                    {
                        writablePage.UnsentReplayableMessages++;
                        writablePage.TotalReplayableMessages++;
                    }

                    // Write the bytes into the page
                    writablePage.curLength += writablePage.PageBytes.WriteInt(writablePage.curLength, 1 + restOfRPCMessageSize);
                    writablePage.PageBytes[writablePage.curLength] = RpcBuffer.Buffer[sizeBytes];
                    writablePage.curLength++;
                    Buffer.BlockCopy(RpcBuffer.Buffer, restOfRPCOffset, writablePage.PageBytes, writablePage.curLength, restOfRPCMessageSize);
                    writablePage.curLength += restOfRPCMessageSize;

                    // Done making modifications to the output buffer and grabbed important state. Can execute the rest concurrently. Release the lock
                    _shuffleOutputRecord.BufferedOutput.ReleaseAppendLock();
                    RpcBuffer.ResetBuffer();

                    // Make sure there is a send enqueued in the work Q.
                    if (_shuffleOutputRecord._sendsEnqueued == 0)
                    {
                        _shuffleOutputRecord.DataWorkQ.Enqueue(-1);
                        Interlocked.Increment(ref _shuffleOutputRecord._sendsEnqueued);
                    }
                }
                else
                {
                    RpcBuffer.ResetBuffer();
                }
                _shuffleOutputRecord.LastSeqNoFromLocalService++;
            }
        }

        private async Task ToDataStreamAsync(Stream writeToStream,
                                             string destString,
                                             CancellationToken ct)

        {
            OutputConnectionRecord outputConnectionRecord;
            if (destString.Equals(_serviceName))
            {
                destString = "";
            }
            lock (_outputs)
            {
                if (!_outputs.TryGetValue(destString, out outputConnectionRecord))
                {
                    // Set up the output record for the first time and add it to the dictionary
                    outputConnectionRecord = new OutputConnectionRecord(this);
                    _outputs[destString] = outputConnectionRecord;
                    Console.WriteLine("Adding output:{0}", destString);
                }
                else
                {
                    Console.WriteLine("restoring output:{0}", destString);
                }
            }
            try
            {
                // Reset the output cursor if it exists
                outputConnectionRecord.BufferedOutput.AcquireTrimLock(2);
                outputConnectionRecord.placeInOutput = new EventBuffer.BuffersCursor(null, -1, 0);
                outputConnectionRecord.BufferedOutput.ReleaseTrimLock();
                // Process replay message
                var inputFlexBuffer = new FlexReadBuffer();
                await FlexReadBuffer.DeserializeAsync(writeToStream, inputFlexBuffer, ct);
                var sizeBytes = inputFlexBuffer.LengthLength;
                // Get the seqNo of the replay/filter point
                var commitSeqNo = StreamCommunicator.ReadBufferedLong(inputFlexBuffer.Buffer, sizeBytes + 1);
                var commitSeqNoReplayable = StreamCommunicator.ReadBufferedLong(inputFlexBuffer.Buffer, sizeBytes + 1 + StreamCommunicator.LongSize(commitSeqNo));
                inputFlexBuffer.ResetBuffer();
                if (outputConnectionRecord.ConnectingAfterRestart)
                {
                    // We've been through recovery (at least partially), and have scrubbed all ephemeral calls. Must now rebase
                    // seq nos using the markers which were sent by the listener. Must first take locks to ensure no interference
                    lock (outputConnectionRecord)
                    {
                        // Don't think I actually need this lock, but can't hurt and shouldn't affect perf.
                        outputConnectionRecord.BufferedOutput.AcquireTrimLock(2);
                        outputConnectionRecord.BufferedOutput.RebaseSeqNosInBuffer(commitSeqNo, commitSeqNoReplayable);
                        outputConnectionRecord.LastSeqNoFromLocalService += commitSeqNo - commitSeqNoReplayable;
                        outputConnectionRecord.ConnectingAfterRestart = false;
                        outputConnectionRecord.BufferedOutput.ReleaseTrimLock();
                    }
                }

                // If recovering, make sure event replay will be filtered out
                outputConnectionRecord.ReplayFrom = commitSeqNo;

                if (outputConnectionRecord.WillResetConnection)
                {
                    // Register our immediate intent to set the connection. This unblocks output writers
                    outputConnectionRecord.ResettingConnection = true;
                    // This lock avoids interference with buffering RPCs
                    lock (outputConnectionRecord)
                    {
                        // If first reconnect/connect after reset, simply adjust the seq no for the first sent message to the received commit seq no
                        outputConnectionRecord.ResettingConnection = false;
                        outputConnectionRecord.LastSeqNoFromLocalService = outputConnectionRecord.BufferedOutput.AdjustFirstSeqNoTo(commitSeqNo);
                        outputConnectionRecord.WillResetConnection = false;
                    }
                }
                outputConnectionRecord.LastSeqSentToReceiver = commitSeqNo - 1;

                // Enqueue a replay send
                if (outputConnectionRecord._sendsEnqueued == 0)
                {

                    Interlocked.Increment(ref outputConnectionRecord._sendsEnqueued);
                    outputConnectionRecord.DataWorkQ.Enqueue(-1);
                }

                // Make sure enough recovery output has been produced before we allow output to start being sent, which means that the next
                // message has to be the first for replay.
                while (Interlocked.Read(ref outputConnectionRecord.LastSeqNoFromLocalService) <
                       Interlocked.Read(ref outputConnectionRecord.LastSeqSentToReceiver)) { await Task.Yield(); };
                bool reconnecting = true;
                while (true)
                {
                    var nextEntry = await outputConnectionRecord.DataWorkQ.DequeueAsync(ct);
                    if (nextEntry == -1)
                    {
                        // This is a send output
                        Interlocked.Decrement(ref outputConnectionRecord._sendsEnqueued);

                        // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!Code to manually trim for performance testing
                        //                    int placeToTrimTo = outputConnectionRecord.LastSeqNoFromLocalService;
                        // Console.WriteLine("send to {0}", outputConnectionRecord.LastSeqNoFromLocalService);
                        outputConnectionRecord.BufferedOutput.AcquireTrimLock(2);
                        var placeAtCall = outputConnectionRecord.LastSeqSentToReceiver;
                        outputConnectionRecord.placeInOutput =
                                await outputConnectionRecord.BufferedOutput.SendAsync(writeToStream, outputConnectionRecord.placeInOutput, reconnecting);
                        reconnecting = false;
                        outputConnectionRecord.BufferedOutput.ReleaseTrimLock();
                        // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!Code to manually trim for performance testing
                        //                    outputConnectionRecord.TrimTo = placeToTrimTo;
                    }
                }
            }
            catch (Exception e)
            {
                // Cleanup held locks if necessary
                await Task.Yield();
                var lockVal = outputConnectionRecord.BufferedOutput.ReadTrimLock();
                if (lockVal == 1 || lockVal == 2)
                {
                    outputConnectionRecord.BufferedOutput.ReleaseTrimLock();
                }
                var bufferLockVal = outputConnectionRecord.BufferedOutput.ReadAppendLock();
                if (bufferLockVal == 2)
                {
                    outputConnectionRecord.BufferedOutput.ReleaseAppendLock();
                }
                throw e;
            }
        }

        private async Task ToControlStreamAsync(Stream writeToStream,
                                                string destString,
                                                CancellationToken ct)

        {
            OutputConnectionRecord outputConnectionRecord;
            if (destString.Equals(_serviceName))
            {
                destString = "";
            }
            lock (_outputs)
            {
                if (!_outputs.TryGetValue(destString, out outputConnectionRecord))
                {
                    // Set up the output record for the first time and add it to the dictionary
                    outputConnectionRecord = new OutputConnectionRecord(this);
                    _outputs[destString] = outputConnectionRecord;
                    Console.WriteLine("Adding output:{0}", destString);
                }
                else
                {
                    Console.WriteLine("restoring output:{0}", destString);
                }
            }
            // Process remote trim message
            var inputFlexBuffer = new FlexReadBuffer();
            await FlexReadBuffer.DeserializeAsync(writeToStream, inputFlexBuffer, ct);
            var sizeBytes = inputFlexBuffer.LengthLength;
            // Get the seqNo of the replay/filter point
            var lastRemoteTrim = StreamCommunicator.ReadBufferedLong(inputFlexBuffer.Buffer, sizeBytes + 1);
            long lastRemoteTrimReplayable;

            // This code dequeues output producing tasks and runs them
            long currentTrim = -1;
            int maxSizeOfWatermark = sizeof(int) + 4 + 2 * sizeof(long);
            var watermarkArr = new byte[maxSizeOfWatermark];
            var watermarkStream = new MemoryStream(watermarkArr);
            try
            {
                while (true)
                {
                    // Always try to trim output buffers if possible to free up resources
                    if (outputConnectionRecord.TrimTo > currentTrim)
                    {
                        currentTrim = outputConnectionRecord.TrimTo;
                        outputConnectionRecord.BufferedOutput.AcquireTrimLock(3);
                        outputConnectionRecord.BufferedOutput.Trim(currentTrim, ref outputConnectionRecord.placeInOutput);
                        outputConnectionRecord.BufferedOutput.ReleaseTrimLock();
                    }
                    var nextEntry = await outputConnectionRecord.ControlWorkQ.DequeueAsync(ct);
                    if (lastRemoteTrim < outputConnectionRecord.RemoteTrim)
                    {
                        // This is a send watermark
                        // Must lock to atomically read due to races with CheckpointAsync and SendInputWatermarks
                        lock (outputConnectionRecord._remoteTrimLock)
                        {

                            lastRemoteTrim = outputConnectionRecord.RemoteTrim;
                            lastRemoteTrimReplayable = outputConnectionRecord.RemoteTrimReplayable;
                        }
                        watermarkStream.Position = 0;
                        var watermarkLength = 1 + StreamCommunicator.LongSize(lastRemoteTrim) + StreamCommunicator.LongSize(lastRemoteTrimReplayable);
                        watermarkStream.WriteInt(watermarkLength);
                        watermarkStream.WriteByte(AmbrosiaRuntime.CommitByte);
                        watermarkStream.WriteLong(lastRemoteTrim);
                        watermarkStream.WriteLong(lastRemoteTrimReplayable);
                        await writeToStream.WriteAsync(watermarkArr, 0, watermarkLength + StreamCommunicator.IntSize(watermarkLength));
                        var flushTask = writeToStream.FlushAsync();
                    }
                }
            }
            catch (Exception e)
            {
                // Cleanup held locks if necessary
                await Task.Yield();
                var lockVal = outputConnectionRecord.BufferedOutput.ReadTrimLock();
                if (lockVal == 3)
                {
                    outputConnectionRecord.BufferedOutput.ReleaseTrimLock();
                }
                var bufferLockVal = outputConnectionRecord.BufferedOutput.ReadAppendLock();
                if (bufferLockVal == 3)
                {
                    outputConnectionRecord.BufferedOutput.ReleaseAppendLock();
                }
                throw e;
            }
        }

        private async Task SendReplayMessageAsync(Stream sendToStream,
                                                  long lastProcessedID,
                                                  long lastProcessedReplayableID,
                                                  CancellationToken ct)
        {
            // Send FilterTo message to the destination command stream
            // Write message size
            sendToStream.WriteInt(1 + StreamCommunicator.LongSize(lastProcessedID) + StreamCommunicator.LongSize(lastProcessedReplayableID));
            // Write message type
            sendToStream.WriteByte(replayFromByte);
            // Write the output filter seqNo for the other side
            sendToStream.WriteLong(lastProcessedID);
            sendToStream.WriteLong(lastProcessedReplayableID);
            await sendToStream.FlushAsync(ct);
        }


        private async Task SendTrimStateMessageAsync(Stream sendToStream,
                                                     long trimTo,
                                                     CancellationToken ct)
        {
            // Send FilterTo message to the destination command stream
            // Write message size
            sendToStream.WriteInt(1 + StreamCommunicator.LongSize(trimTo));
            // Write message type
            sendToStream.WriteByte(trimToByte);
            // Write the output filter seqNo for the other side
            sendToStream.WriteLong(trimTo);
            await sendToStream.FlushAsync(ct);
        }

        private async Task FromDataStreamAsync(Stream readFromStream,
                                               string sourceString,
                                               CancellationToken ct)
        {
            InputConnectionRecord inputConnectionRecord;
            if (sourceString.Equals(_serviceName))
            {
                sourceString = "";
            }
            if (!_inputs.TryGetValue(sourceString, out inputConnectionRecord))
            {
                // Create input record and add it to the dictionary
                inputConnectionRecord = new InputConnectionRecord();
                _inputs[sourceString] = inputConnectionRecord;
                Console.WriteLine("Adding input:{0}", sourceString);
            }
            else
            {
                Console.WriteLine("restoring input:{0}", sourceString);
            }
            inputConnectionRecord.DataConnectionStream = (NetworkStream)readFromStream;
            await SendReplayMessageAsync(readFromStream, inputConnectionRecord.LastProcessedID + 1, inputConnectionRecord.LastProcessedReplayableID + 1, ct);
            // Create new input task for monitoring new input
            Task inputTask;
            inputTask = InputDataListenerAsync(inputConnectionRecord, sourceString, ct);
            await inputTask;
        }

        private async Task FromControlStreamAsync(Stream readFromStream,
                                                  string sourceString,
                                                  CancellationToken ct)
        {
            InputConnectionRecord inputConnectionRecord;
            if (sourceString.Equals(_serviceName))
            {
                sourceString = "";
            }
            if (!_inputs.TryGetValue(sourceString, out inputConnectionRecord))
            {
                // Create input record and add it to the dictionary
                inputConnectionRecord = new InputConnectionRecord();
                _inputs[sourceString] = inputConnectionRecord;
                Console.WriteLine("Adding input:{0}", sourceString);
            }
            else
            {
                Console.WriteLine("restoring input:{0}", sourceString);
            }
            inputConnectionRecord.ControlConnectionStream = (NetworkStream)readFromStream;
            OutputConnectionRecord outputConnectionRecord;
            long outputTrim = -1;
            lock (_outputs)
            {
                if (_outputs.TryGetValue(sourceString, out outputConnectionRecord))
                {
                    outputTrim = outputConnectionRecord.TrimTo;
                }
            }
            await SendTrimStateMessageAsync(readFromStream, outputTrim, ct);
            // Create new input task for monitoring new input
            Task inputTask;
            inputTask = InputControlListenerAsync(inputConnectionRecord, sourceString, ct);
            await inputTask;
        }


        private async Task InputDataListenerAsync(InputConnectionRecord inputRecord,
                                                  string inputName,
                                                  CancellationToken ct)
        {
            var inputFlexBuffer = new FlexReadBuffer();
            var bufferSize = 128 * 1024;
            byte[] bytes = new byte[bufferSize];
            byte[] bytesBak = new byte[bufferSize];
            while (true)
            {
                await FlexReadBuffer.DeserializeAsync(inputRecord.DataConnectionStream, inputFlexBuffer, ct);
                await ProcessInputMessageAsync(inputRecord, inputName, inputFlexBuffer);
            }
        }

        private async Task InputControlListenerAsync(InputConnectionRecord inputRecord,
                                                     string inputName,
                                                     CancellationToken ct)
        {
            var inputFlexBuffer = new FlexReadBuffer();
            var myBytes = new byte[20];
            var bufferSize = 128 * 1024;
            byte[] bytes = new byte[bufferSize];
            byte[] bytesBak = new byte[bufferSize];
            while (true)
            {
                await FlexReadBuffer.DeserializeAsync(inputRecord.ControlConnectionStream, inputFlexBuffer, ct);
                var sizeBytes = inputFlexBuffer.LengthLength;
                switch (inputFlexBuffer.Buffer[sizeBytes])
                {
                    case CommitByte:
                        long commitSeqNo = StreamCommunicator.ReadBufferedLong(inputFlexBuffer.Buffer, sizeBytes + 1);
                        long replayableCommitSeqNo = StreamCommunicator.ReadBufferedLong(inputFlexBuffer.Buffer, sizeBytes + 1 + StreamCommunicator.LongSize(commitSeqNo));
                        inputFlexBuffer.ResetBuffer();

                        // Find the appropriate connection record
                        var outputConnectionRecord = _outputs[inputName];
                        // Check to make sure this is progress, otherwise, can ignore
                        if (commitSeqNo > outputConnectionRecord.TrimTo && !outputConnectionRecord.WillResetConnection && !outputConnectionRecord.ConnectingAfterRestart)
                        {
                            // Lock to ensure atomic update of both variables due to race in AmbrosiaSerialize
                            lock (outputConnectionRecord._trimLock)
                            {
                                outputConnectionRecord.TrimTo = Math.Max(outputConnectionRecord.TrimTo, commitSeqNo);
                                outputConnectionRecord.ReplayableTrimTo = Math.Max(outputConnectionRecord.ReplayableTrimTo, replayableCommitSeqNo);
                            }
                            if (outputConnectionRecord.ControlWorkQ.IsEmpty)
                            {
                                outputConnectionRecord.ControlWorkQ.Enqueue(-2);
                            }
                            lock (_committer._trimWatermarks)
                            {
                                _committer._trimWatermarks[inputName] = replayableCommitSeqNo;
                            }
                        }
                        break;
                    default:
                        // Bubble the exception up to CRA
                        throw new Exception("Illegal leading byte in input control message");
                        break;
                }
            }
        }

        private async Task ProcessInputMessageAsync(InputConnectionRecord inputRecord,
                                                    string inputName,
                                                    FlexReadBuffer inputFlexBuffer)
        {
            var sizeBytes = inputFlexBuffer.LengthLength;
            switch (inputFlexBuffer.Buffer[sizeBytes])
            {
                case RPCByte:
                    var methodID = inputFlexBuffer.Buffer.ReadBufferedInt(sizeBytes + 2);
                    long newFileSize;
                    if (inputFlexBuffer.Buffer[sizeBytes + 2 + StreamCommunicator.IntSize(methodID)] != (byte)RpcTypes.RpcType.Impulse)
                    {
                        newFileSize = await _committer.AddRow(inputFlexBuffer, inputName, inputRecord.LastProcessedID + 1, inputRecord.LastProcessedReplayableID + 1, _outputs, inputRecord);
                    }
                    else
                    {
                        newFileSize = await _committer.AddRow(inputFlexBuffer, inputName, inputRecord.LastProcessedID + 1, inputRecord.LastProcessedReplayableID, _outputs, inputRecord);
                    }
                    inputFlexBuffer.ResetBuffer();
                    if (_newLogTriggerSize > 0 && newFileSize >= _newLogTriggerSize)
                    {
                        // Make sure only one input thread is moving to the next log file. Won't break the system if we don't do this, but could result in
                        // empty log files
                        if (Interlocked.CompareExchange(ref _movingToNextLog, 1, 0) == 0)
                        {
                            await MoveServiceToNextLogFileAsync();
                            _movingToNextLog = 0;
                        }
                    }
                    break;

                case CountReplayableRPCBatchByte:
                    var restOfBatchOffset = inputFlexBuffer.LengthLength + 1;
                    var memStream = new MemoryStream(inputFlexBuffer.Buffer, restOfBatchOffset, inputFlexBuffer.Length - restOfBatchOffset);
                    var numRPCs = memStream.ReadInt();
                    var numReplayableRPCs = memStream.ReadInt();
                    newFileSize = await _committer.AddRow(inputFlexBuffer, inputName, inputRecord.LastProcessedID + numRPCs, inputRecord.LastProcessedReplayableID + numReplayableRPCs, _outputs, inputRecord);
                    inputFlexBuffer.ResetBuffer();
                    memStream.Dispose();
                    if (_newLogTriggerSize > 0 && newFileSize >= _newLogTriggerSize)
                    {
                        // Make sure only one input thread is moving to the next log file. Won't break the system if we don't do this, but could result in
                        // empty log files
                        if (Interlocked.CompareExchange(ref _movingToNextLog, 1, 0) == 0)
                        {
                            await MoveServiceToNextLogFileAsync();
                            _movingToNextLog = 0;
                        }
                    }
                    break;

                case RPCBatchByte:
                    restOfBatchOffset = inputFlexBuffer.LengthLength + 1;
                    memStream = new MemoryStream(inputFlexBuffer.Buffer, restOfBatchOffset, inputFlexBuffer.Length - restOfBatchOffset);
                    numRPCs = memStream.ReadInt();
                    newFileSize = await _committer.AddRow(inputFlexBuffer, inputName, inputRecord.LastProcessedID + numRPCs, inputRecord.LastProcessedReplayableID + numRPCs, _outputs, inputRecord);
                    inputFlexBuffer.ResetBuffer();
                    memStream.Dispose();
                    if (_newLogTriggerSize > 0 && newFileSize >= _newLogTriggerSize)
                    {
                        // Make sure only one input thread is moving to the next log file. Won't break the system if we don't do this, but could result in
                        // empty log files
                        if (Interlocked.CompareExchange(ref _movingToNextLog, 1, 0) == 0)
                        {
                            await MoveServiceToNextLogFileAsync();
                            _movingToNextLog = 0;
                        }
                    }
                    break;

                case PingByte:
                    // Write time into correct place in message
                    memStream = new MemoryStream(inputFlexBuffer.Buffer, inputFlexBuffer.Length - 4 * sizeof(long), sizeof(long));
                    long time;
                    GetSystemTimePreciseAsFileTime(out time);
                    memStream.WriteLongFixed(time);
                    // Treat as RPC
                    await _committer.AddRow(inputFlexBuffer, inputName, inputRecord.LastProcessedID + 1, inputRecord.LastProcessedReplayableID + 1, _outputs, inputRecord);
                    inputFlexBuffer.ResetBuffer();
                    memStream.Dispose();
                    break;

                case PingReturnByte:
                    // Write time into correct place in message
                    memStream = new MemoryStream(inputFlexBuffer.Buffer, inputFlexBuffer.Length - 1 * sizeof(long), sizeof(long));
                    GetSystemTimePreciseAsFileTime(out time);
                    memStream.WriteLongFixed(time);
                    // Treat as RPC
                    await _committer.AddRow(inputFlexBuffer, inputName, inputRecord.LastProcessedID + 1, inputRecord.LastProcessedReplayableID + 1, _outputs, inputRecord);
                    inputFlexBuffer.ResetBuffer();
                    memStream.Dispose();
                    break;

                default:
                    // Bubble the exception up to CRA
                    throw new Exception("Illegal leading byte in input data message");
            }
        }

        private LogWriter OpenNextCheckpointFile()
        {
            if (LogWriter.FileExists(CheckpointName(_lastCommittedCheckpoint + 1)))
            {
                File.Delete(CheckpointName(_lastCommittedCheckpoint + 1));
            }
            LogWriter retVal = null;
            try
            {
                retVal = new LogWriter(CheckpointName(_lastCommittedCheckpoint + 1), 1024 * 1024, 6);
            }
            catch (Exception e)
            {
                OnError(0, "Error opening next checkpoint file" + e.ToString());
            }
            return retVal;
        }

        private void CleanupOldCheckpoint()
        {
            var fileNameToDelete = _logFileNameBase + (_lastCommittedCheckpoint - 1).ToString();
            if (LogWriter.FileExists(fileNameToDelete))
            {
                File.Delete(fileNameToDelete);
            }
        }

        // This method takes a checkpoint and bumps the counter. It DOES NOT quiesce anything
        public async Task CheckpointAsync()
        {
            var oldCheckpointWriter = _checkpointWriter;
            // Take lock on new checkpoint file
            _checkpointWriter = OpenNextCheckpointFile();
            // Make sure the service is quiesced before continuing
            CheckpointingService = true;
            while (LastReceivedCheckpoint == null) { await Task.Yield(); }
            // Now that the service has sent us its checkpoint, we need to quiesce the output connections, which may be sending
            foreach (var outputRecord in _outputs)
            {
                outputRecord.Value.BufferedOutput.AcquireAppendLock();
            }

            CheckpointingService = false;
            // Serialize committer
            _committer.Serialize(_checkpointWriter);
            // Serialize input connections
            _inputs.AmbrosiaSerialize(_checkpointWriter);
            // Serialize output connections
            _outputs.AmbrosiaSerialize(_checkpointWriter);
            foreach (var outputRecord in _outputs)
            {
                outputRecord.Value.BufferedOutput.ReleaseAppendLock();
            }

            // Serialize the service note that the local listener task is blocked after reading the checkpoint until the end of this method
            _checkpointWriter.Write(LastReceivedCheckpoint.Buffer, 0, LastReceivedCheckpoint.Length);
            _checkpointWriter.Write(_localServiceReceiveFromStream, _lastReceivedCheckpointSize);
            _checkpointWriter.Flush();
            _lastCommittedCheckpoint++;
            InsertOrReplaceServiceInfoRecord(InfoTitle("LastCommittedCheckpoint"), _lastCommittedCheckpoint.ToString());

            // Trim output buffers of inputs, since the inputs are now part of the checkpoint and can't be lost. Must do this after the checkpoint has been
            // successfully written
            foreach (var kv in _inputs)
            {
                OutputConnectionRecord outputConnectionRecord;
                if (!_outputs.TryGetValue(kv.Key, out outputConnectionRecord))
                {
                    outputConnectionRecord = new OutputConnectionRecord(this);
                    _outputs[kv.Key] = outputConnectionRecord;
                }
                // Must lock to atomically update due to race with ToControlStreamAsync
                lock (outputConnectionRecord._remoteTrimLock)
                {
                    outputConnectionRecord.RemoteTrim = Math.Max(kv.Value.LastProcessedID, outputConnectionRecord.RemoteTrim);
                    outputConnectionRecord.RemoteTrimReplayable = Math.Max(kv.Value.LastProcessedReplayableID, outputConnectionRecord.RemoteTrimReplayable);
                }
                if (outputConnectionRecord.ControlWorkQ.IsEmpty)
                {
                    outputConnectionRecord.ControlWorkQ.Enqueue(-2);
                }
            }

            if (oldCheckpointWriter != null)
            {
                // Release lock on previous checkpoint file
                oldCheckpointWriter.Dispose();
            }

            // Unblock the local input processing task
            LastReceivedCheckpoint.ThrowAwayBuffer();
            LastReceivedCheckpoint = null;
        }

        public AmbrosiaRuntime() : base()
        {
        }

        public override void Initialize(object param)
        {
            // Workaround because of parameter type limitation in CRA
            AmbrosiaRuntimeParams p = new AmbrosiaRuntimeParams();
            XmlSerializer xmlSerializer = new XmlSerializer(p.GetType());
            using (StringReader textReader = new StringReader((string)param))
            {
                p = (AmbrosiaRuntimeParams)xmlSerializer.Deserialize(textReader);
            }

            bool runningRepro = false;
            bool sharded = p.shardID > 0;
            _shardID = p.shardID;

            Initialize(
                p.serviceReceiveFromPort,
                p.serviceSendToPort,
                p.serviceName,
                p.serviceLogPath,
                p.createService,
                p.pauseAtStart,
                p.persistLogs,
                p.activeActive,
                p.logTriggerSizeMB,
                p.storageConnectionString,
                p.currentVersion,
                p.upgradeToVersion,
                runningRepro,
                sharded
            );
        }

        internal void RuntimeChecksOnProcessStart(long shardID = -1)
        {
            if (!_createService)
            {
                long readVersion = -1;
                try
                {
                    readVersion = long.Parse(RetrieveServiceInfo(InfoTitle("CurrentVersion", shardID)));
                }
                catch
                {
                    OnError(VersionMismatch, "Version mismatch on process start: Expected " + _currentVersion + " was: " + RetrieveServiceInfo(InfoTitle("CurrentVersion", shardID)));
                }
                if (_currentVersion != readVersion)
                {
                    OnError(VersionMismatch, "Version mismatch on process start: Expected " + _currentVersion + " was: " + readVersion.ToString());
                }
                if (!_runningRepro)
                {
                    if (long.Parse(RetrieveServiceInfo(InfoTitle("LastCommittedCheckpoint", shardID))) < 1)
                    {
                        OnError(MissingCheckpoint, "No checkpoint in metadata");

                    }
                }
                if (!LogWriter.DirectoryExists(LogDirectory(_currentVersion)))
                {
                    OnError(MissingCheckpoint, "No checkpoint/logs directory");
                }
                var lastCommittedCheckpoint = long.Parse(RetrieveServiceInfo(InfoTitle("LastCommittedCheckpoint", shardID)));
                if (!LogWriter.FileExists(CheckpointName(lastCommittedCheckpoint, -1, shardID)))
                {
                    OnError(MissingCheckpoint, "Missing checkpoint " + lastCommittedCheckpoint.ToString());
                }
                if (!LogWriter.FileExists(LogFileName(lastCommittedCheckpoint, -1, shardID)))
                {
                    OnError(MissingLog, "Missing log " + lastCommittedCheckpoint.ToString());
                }
            }
        }

        public void Initialize(int serviceReceiveFromPort,
                       int serviceSendToPort,
                       string serviceName,
                       string serviceLogPath,
                       bool? createService,
                       bool pauseAtStart,
                       bool persistLogs,
                       bool activeActive,
                       long logTriggerSizeMB,
                       string storageConnectionString,
                       long currentVersion,
                       long upgradeToVersion,
                       bool runningRepro,
                       bool sharded
                       )
        {
            _runningRepro = runningRepro;
            _currentVersion = currentVersion;
            _upgradeToVersion = upgradeToVersion;
            _upgrading = (_currentVersion < _upgradeToVersion);
            if (pauseAtStart == true)
            {
                Console.WriteLine("Hit Enter to continue:");
                Console.ReadLine();
            }
            else
            {
                Console.WriteLine("Ready ...");
            }
            _persistLogs = persistLogs;
            _activeActive = activeActive;
            _newLogTriggerSize = logTriggerSizeMB * 1000000;
            _serviceLogPath = serviceLogPath;
            _localServiceReceiveFromPort = serviceReceiveFromPort;
            _localServiceSendToPort = serviceSendToPort;
            _serviceName = serviceName;
            _storageConnectionString = storageConnectionString;
            _sharded = sharded;
            if (_sharded)
            {
                Console.WriteLine("Running instance with shard ID " + _shardID.ToString());
            }
            _coral = ClientLibrary;

            Console.WriteLine("Logs directory: {0}", _serviceLogPath);

            if (createService == null)
            {
                if (LogWriter.DirectoryExists(RootDirectory()))
                {
                    createService = false;
                }
                else
                {
                    createService = true;
                }
            }
            AddAsyncInputEndpoint(AmbrosiaDataInputsName, new AmbrosiaInput(this, "data"));
            AddAsyncInputEndpoint(AmbrosiaControlInputsName, new AmbrosiaInput(this, "control"));
            AddAsyncOutputEndpoint(AmbrosiaDataOutputsName, new AmbrosiaOutput(this, "data"));
            AddAsyncOutputEndpoint(AmbrosiaControlOutputsName, new AmbrosiaOutput(this, "control"));
            _createService = createService.Value;
            RecoverOrStartAsync().Wait();
        }

        internal void InitializeRepro(string serviceName,
                                      string serviceLogPath,
                                      long checkpointToLoad,
                                      int version,
                                      bool testUpgrade,
                                      int serviceReceiveFromPort,
                                      int serviceSendToPort)
        {
            _localServiceReceiveFromPort = serviceReceiveFromPort;
            _localServiceSendToPort = serviceSendToPort;
            _currentVersion = version;
            _runningRepro = true;
            _persistLogs = false;
            _activeActive = true;
            _serviceLogPath = serviceLogPath;
            _serviceName = serviceName;
            _sharded = false;
            _createService = false;
            RecoverOrStartAsync(checkpointToLoad, testUpgrade).Wait();
        }
    }

    class Program
    {
        private static LocalAmbrosiaRuntimeModes _runtimeMode;
        private static string _instanceName = null;
        private static int _replicaNumber = 0;
        private static int _serviceReceiveFromPort = -1;
        private static int _serviceSendToPort = -1;
        private static string _serviceLogPath = Path.Combine(Path.GetPathRoot(Path.GetFullPath(".")), "AmbrosiaLogs") + Path.DirectorySeparatorChar;
        private static string _binariesLocation = "AmbrosiaBinaries";
        private static long _checkpointToLoad = 0;
        private static bool _isTestingUpgrade = false;
        private static AmbrosiaRecoveryModes _recoveryMode = AmbrosiaRecoveryModes.A;
        private static bool _isActiveActive = false;
        private static bool _isPauseAtStart = false;
        private static bool _isPersistLogs = true;
        private static long _logTriggerSizeMB = 1000;
        private static int _currentVersion = 0;
        private static long _upgradeVersion = -1;
        private static long _shardID = -1;

        static void Main(string[] args)
        {
            ParseAndValidateOptions(args);

            switch (_runtimeMode)
            {
                case LocalAmbrosiaRuntimeModes.DebugInstance:
                    var myRuntime = new AmbrosiaRuntime();
                    myRuntime.InitializeRepro(_instanceName, _serviceLogPath, _checkpointToLoad, _currentVersion,
                        _isTestingUpgrade, _serviceReceiveFromPort, _serviceSendToPort);
                    return;
                case LocalAmbrosiaRuntimeModes.AddReplica:
                case LocalAmbrosiaRuntimeModes.RegisterInstance:
                    if (_runtimeMode == LocalAmbrosiaRuntimeModes.AddReplica)
                    {
                        _isActiveActive = true;
                    }
                    var client = new CRAClientLibrary(Environment.GetEnvironmentVariable("AZURE_STORAGE_CONN_STRING"));
                    client.DisableArtifactUploading();

                    var replicaName = $"{_instanceName}{_replicaNumber}";
                    AmbrosiaRuntimeParams param = new AmbrosiaRuntimeParams();
                    param.createService = _recoveryMode == AmbrosiaRecoveryModes.A
                        ? (bool?)null
                        : (_recoveryMode != AmbrosiaRecoveryModes.N);
                    param.pauseAtStart = _isPauseAtStart;
                    param.persistLogs = _isPersistLogs;
                    param.logTriggerSizeMB = _logTriggerSizeMB;
                    param.activeActive = _isActiveActive;
                    param.upgradeToVersion = _upgradeVersion;
                    param.currentVersion = _currentVersion;
                    param.serviceReceiveFromPort = _serviceReceiveFromPort;
                    param.serviceSendToPort = _serviceSendToPort;
                    param.serviceName = _instanceName;
                    param.serviceLogPath = _serviceLogPath;
                    param.AmbrosiaBinariesLocation = _binariesLocation;
                    param.storageConnectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONN_STRING");
                    param.shardID = _shardID;

                    try
                    {
                        if (client.DefineVertex(param.AmbrosiaBinariesLocation, () => new AmbrosiaRuntime()) != CRAErrorCode.Success)
                        {
                            throw new Exception();
                        }

                        // Workaround because of limitation in parameter serialization in CRA
                        XmlSerializer xmlSerializer = new XmlSerializer(param.GetType());
                        string serializedParams;
                        using (StringWriter textWriter = new StringWriter())
                        {
                            xmlSerializer.Serialize(textWriter, param);
                            serializedParams = textWriter.ToString();
                        }

                        if (client.InstantiateVertex(replicaName, param.serviceName, param.AmbrosiaBinariesLocation, serializedParams) != CRAErrorCode.Success)
                        {
                            throw new Exception();
                        }
                        client.AddEndpoint(param.serviceName, AmbrosiaRuntime.AmbrosiaDataInputsName, true, true);
                        client.AddEndpoint(param.serviceName, AmbrosiaRuntime.AmbrosiaDataOutputsName, false, true);
                        client.AddEndpoint(param.serviceName, AmbrosiaRuntime.AmbrosiaControlInputsName, true, true);
                        client.AddEndpoint(param.serviceName, AmbrosiaRuntime.AmbrosiaControlOutputsName, false, true);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error trying to upload service. Exception: " + e.Message);
                    }

                    return;
                default:
                    throw new NotSupportedException($"Runtime mode: {_runtimeMode} not supported.");
            }
        }

        private static void ParseAndValidateOptions(string[] args)
        {
            var options = ParseOptions(args, out var shouldShowHelp);
            ValidateOptions(options, shouldShowHelp);
        }

        private static OptionSet ParseOptions(string[] args, out bool shouldShowHelp)
        {
            var showHelp = false;

            var basicOptions = new OptionSet
            {
                { "i|instanceName=", "The instance name [REQUIRED].", i => _instanceName = i },
                { "rp|receivePort=", "The service receive from port [REQUIRED].", rp => _serviceReceiveFromPort = int.Parse(rp) },
                { "sp|sendPort=", "The service send to port. [REQUIRED]", sp => _serviceSendToPort = int.Parse(sp) },
                { "l|log=", "The service log path.", l => _serviceLogPath = l },
            };

            var helpOption = new OptionSet
            {
                {"h|help", "show this message and exit", h => showHelp = h != null},
            };

            var registerInstanceOptionSet = basicOptions.AddMany(new OptionSet
            {
                {
                    "cs|createService=",
                    $"[{string.Join(" | ", GetModesDescriptions().Select(md => $"{md.Item1} - {md.Item2}"))}].",
                    cs => _recoveryMode = (AmbrosiaRecoveryModes) Enum.Parse(typeof(AmbrosiaRecoveryModes), cs, true)
                },
                {"ps|pauseAtStart", "Is pause at start enabled.", ps => _isPauseAtStart = true},
                {"npl|noPersistLogs", "Is persistent logging disabled.", ps => _isPersistLogs = false},
                {"lts|logTriggerSize=", "Log trigger size (in MBs).", lts => _logTriggerSizeMB = long.Parse(lts)},
                {"aa|activeActive", "Is active-active enabled.", aa => _isActiveActive = true},
                {"cv|currentVersion=", "The current version #.", cv => _currentVersion = int.Parse(cv)},
                {"uv|upgradeVersion=", "The upgrade version #.", uv => _upgradeVersion = int.Parse(uv)},
                {"si|shardID=", "The shard ID of the instance", si => _shardID = long.Parse(si) }
            });

            var addReplicaOptionSet = new OptionSet {
                { "r|replicaNum=", "The replica # [REQUIRED].", r => _replicaNumber = int.Parse(r) },
            }.AddMany(registerInstanceOptionSet);

            var debugInstanceOptionSet = basicOptions.AddMany(new OptionSet {

                { "c|checkpoint=", "The checkpoint # to load.", c => _checkpointToLoad = long.Parse(c) },
                { "cv|currentVersion=", "The version # to debug.", cv => _currentVersion = int.Parse(cv) },
                { "tu|testingUpgrade", "Is testing upgrade.", u => _isTestingUpgrade = true },
            });

            registerInstanceOptionSet = registerInstanceOptionSet.AddMany(helpOption);
            addReplicaOptionSet = addReplicaOptionSet.AddMany(helpOption);
            debugInstanceOptionSet = debugInstanceOptionSet.AddMany(helpOption);


            var runtimeModeToOptionSet = new Dictionary<LocalAmbrosiaRuntimeModes, OptionSet>
            {
                { LocalAmbrosiaRuntimeModes.RegisterInstance, registerInstanceOptionSet},
                { LocalAmbrosiaRuntimeModes.AddReplica, addReplicaOptionSet},
                { LocalAmbrosiaRuntimeModes.DebugInstance, debugInstanceOptionSet},
            };

            _runtimeMode = default(LocalAmbrosiaRuntimeModes);
            if (args.Length < 1 || !Enum.TryParse(args[0], true, out _runtimeMode))
            {
                Console.WriteLine("Missing or illegal runtime mode.");
                ShowHelp(runtimeModeToOptionSet);
                Environment.Exit(1);
            }

            var options = runtimeModeToOptionSet[_runtimeMode];
            try
            {
                options.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine("Invalid arguments: " + e.Message);
                ShowHelp(options, _runtimeMode);
                Environment.Exit(1);
            }

            shouldShowHelp = showHelp;

            return options;
        }

        public enum LocalAmbrosiaRuntimeModes
        {
            AddReplica,
            RegisterInstance,
            DebugInstance,
        }

        public enum AmbrosiaRecoveryModes
        {
            [Description("AutoRecovery")]
            A,
            [Description("NoRecovery")]
            N,
            [Description("AlwaysRecover")]
            Y,
        }

        private static IEnumerable<Tuple<string, string>> GetModesDescriptions()
        {
            foreach (var mode in Enum.GetValues(typeof(AmbrosiaRecoveryModes)))
            {
                yield return new Tuple<string, string>(mode.ToString(), ((Enum)mode).GetDescription());
            }
        }

        private static void ValidateOptions(OptionSet options, bool shouldShowHelp)
        {
            var errorMessage = string.Empty;
            if (_instanceName == null) errorMessage += "Instance name is required.\n";
            if (_serviceReceiveFromPort == -1) errorMessage += "Receive port is required.\n";
            if (_serviceSendToPort == -1) errorMessage += "Send port is required.\n";
            if (_runtimeMode == LocalAmbrosiaRuntimeModes.AddReplica)
            {
                if (_replicaNumber == 0)
                {
                    errorMessage += "Replica number is required.\n";
                }
            }

            // handles the case when an upgradeversion is not specified
            if (_upgradeVersion == -1)
            {
                _upgradeVersion = _currentVersion;
            }


            if (_currentVersion > _upgradeVersion)
            {
                errorMessage += "Current version # exceeds upgrade version #.\n";
            }

            if (errorMessage != string.Empty)
            {
                Console.WriteLine(errorMessage);
                ShowHelp(options, _runtimeMode);
                Environment.Exit(1);
            }


            if (shouldShowHelp)
            {
                ShowHelp(options, _runtimeMode);
                Environment.Exit(0);
            }
        }

        private static void ShowHelp(OptionSet options, LocalAmbrosiaRuntimeModes mode)
        {
            var name = typeof(Program).Assembly.GetName().Name;
#if NETCORE
            Console.WriteLine($"Usage: dotnet {name}.dll {mode} [OPTIONS]\nOptions:");
#else
            Console.WriteLine($"Usage: {name}.exe {mode} [OPTIONS]\nOptions:");
#endif
            options.WriteOptionDescriptions(Console.Out);
        }

        private static void ShowHelp(Dictionary<LocalAmbrosiaRuntimeModes, OptionSet> modeToOptions)
        {
            foreach (var modeToOption in modeToOptions)
            {
                ShowHelp(modeToOption.Value, modeToOption.Key);
            }
        }
    }

    public static class OptionSetExtensions
    {
        public static OptionSet AddMany(this OptionSet thisOptionSet, OptionSet otherOptionSet)
        {
            var newOptionSet = new OptionSet();
            foreach (var option in thisOptionSet)
            {
                newOptionSet.Add(option);
            }
            foreach (var option in otherOptionSet)
            {
                newOptionSet.Add(option);
            }

            return newOptionSet;
        }

        public static string GetDescription(this Enum value)
        {
            Type type = value.GetType();
            string enumName = Enum.GetName(type, value);
            if (enumName == null)
            {
                return null; // or return string.Empty;
            }
            var typeField = type.GetField(enumName);
            if (typeField == null)
            {
                return null; // or return string.Empty;
            }
            var attribute = Attribute.GetCustomAttribute(typeField, typeof(DescriptionAttribute));
            return (attribute as DescriptionAttribute)?.Description; // ?? string.Empty maybe added
        }
    }
}
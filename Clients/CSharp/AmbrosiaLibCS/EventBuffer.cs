using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Ambrosia.StreamCommunicator;

namespace Ambrosia
{
    [DataContract]
    public class EventBuffer
    {
        const int defaultPageSize = 1024 * 1024;
        private int MaxBufferPages = 30;
        static ConcurrentQueue<BufferPage> _pool = null;

        [DataContract]
        public class BufferPage
        {
            [DataMember]
            public byte[] PageBytes { get; set; }
            [DataMember]
            public int curLength { get; set; }
            [DataMember]
            public int NumMessages { get; set; }
            public BufferPage(byte[] pageBytes)
            {
                PageBytes = pageBytes;
                curLength = 0;
                NumMessages = 0;
            }
        }

        [DataMember]
        ElasticCircularBuffer<BufferPage> _bufferQ;
        long _bufferedOutputLock;
        internal byte[] TempArrForWritingBatchBytes { get; set; }
        internal MemoryStream MemStreamForWritingBatchBytes { get; set; }


        internal EventBuffer()
        {
            _bufferQ = new ElasticCircularBuffer<BufferPage>();
            _bufferedOutputLock = 0;
            TempArrForWritingBatchBytes = new byte[sizeof(int) + sizeof(int) + 3];
            MemStreamForWritingBatchBytes = new MemoryStream(TempArrForWritingBatchBytes);
        }

        internal void Serialize(Stream writeToStream)
        {
            writeToStream.WriteIntFixed(_bufferQ.Count);
            foreach (var currentBuf in _bufferQ)
            {
                writeToStream.WriteIntFixed(currentBuf.PageBytes.Length);
                writeToStream.WriteIntFixed(currentBuf.curLength);
                writeToStream.Write(currentBuf.PageBytes, 0, currentBuf.curLength);
                writeToStream.WriteIntFixed(currentBuf.NumMessages);
            }
        }

        internal static EventBuffer Deserialize(Stream readFromStream)
        {
            var _retVal = new EventBuffer();
            var bufferCount = readFromStream.ReadIntFixed();
            for (int i = 0; i < bufferCount; i++)
            {
                var pageSize = readFromStream.ReadIntFixed();
                var pageFilled = readFromStream.ReadIntFixed();
                var myBytes = new byte[pageSize];
                readFromStream.Read(myBytes, 0, pageFilled);
                var newBufferPage = new BufferPage(myBytes);
                newBufferPage.curLength = pageFilled;
                newBufferPage.NumMessages = readFromStream.ReadIntFixed();
                _retVal._bufferQ.Enqueue(ref newBufferPage);
            }
            return _retVal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void LockOutputBuffer()
        {
            while (true)
            {
                var origVal = Interlocked.CompareExchange(ref _bufferedOutputLock, 1, 0);
                if (origVal == 0)
                {
                    // We have the lock
                    break;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UnlockOutputBuffer()
        {
            _bufferedOutputLock = 0;
        }

        public class BuffersCursor
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
                                                     BuffersCursor placeToStart)
        {
            var bufferEnumerator = placeToStart.PageEnumerator;
            var posToStart = placeToStart.PagePos;
            var relSeqPos = placeToStart.RelSeqPos;
            if (placeToStart.PagePos == -1)
            {
                // A previous trim removed all output, maybe more got added since
                LockOutputBuffer();
                bufferEnumerator = _bufferQ.GetEnumerator();
                var dataToWrite = bufferEnumerator.MoveNext();
                UnlockOutputBuffer();
                if (dataToWrite)
                {
                    // Output was added since trim
                    posToStart = 0;
                    relSeqPos = 0;
                }
                else
                {
                    // Still no output pages
                    return placeToStart;
                }
            }
            // We are guaranteed to have an enumerator and starting point. Must send output.
            LockOutputBuffer();
            do
            {
                var curBuffer = bufferEnumerator.Current;
                var pageLength = curBuffer.curLength;
                var morePages = (curBuffer != _bufferQ.Last());
                int numRPCs = curBuffer.NumMessages - relSeqPos;
                UnlockOutputBuffer();
                // send the buffer
                if (pageLength - posToStart > 0)
                {
                    // We really have output to send. Send it.
                    //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! Uncomment/Comment for testing
                    //Console.WriteLine("Wrote from {0} to {1}, {2}", curBuffer.LowestSeqNo, curBuffer.HighestSeqNo, morePages);

                    int bytesInBatchData = pageLength - posToStart;
                    if (numRPCs > 1)
                    {
                        // writing a batch
                        MemStreamForWritingBatchBytes.Position = 0;
                        int numBytes = IntSize(bytesInBatchData + 1 + IntSize(numRPCs)) + 1 +
                                       IntSize(numRPCs);
                        MemStreamForWritingBatchBytes.WriteInt(bytesInBatchData + 1 + IntSize(numRPCs));
                        MemStreamForWritingBatchBytes.WriteByte(Ambrosia.AmbrosiaRuntime.RPCBatchByte);
                        MemStreamForWritingBatchBytes.WriteInt(numRPCs);
                        await outputStream.WriteAsync(TempArrForWritingBatchBytes, 0, numBytes);
                        await outputStream.WriteAsync(curBuffer.PageBytes, posToStart, bytesInBatchData);
                        await outputStream.FlushAsync();
                    }
                    else
                    {
                        // writing individual RPCs
                        await outputStream.WriteAsync(curBuffer.PageBytes, posToStart, bytesInBatchData);
                        await outputStream.FlushAsync();
                    }
                }
                if (morePages)
                {
                    // There were more pages to output at start of iteration. Move to next page and continue.
                    posToStart = 0;
                    relSeqPos = 0;
                    LockOutputBuffer();
                }
                else
                {
                    // More output MAY have been put on this page
                    LockOutputBuffer();
                    if (curBuffer.curLength == pageLength)
                    {
                        // The page didn't change. Get ready to clean it up
                        if (curBuffer == _bufferQ.Last())
                        {
                            // Still the last page. Reset the cursor
                            posToStart = -1;
                            relSeqPos = 0;
                        }
                        else
                        {
                            // No longer the last page. Keep going
                            posToStart = 0;
                            relSeqPos = 0;
                        }
                    }
                    else
                    {
                        // The page changed. Don't clean it up.
                        posToStart = pageLength;
                        relSeqPos += numRPCs;
                        break;
                    }
                }
                // Return the page to the pool
                _bufferQ.Dequeue();
                // Return page to pool
                curBuffer.curLength = 0;
                curBuffer.NumMessages = 0;
                _pool.Enqueue(curBuffer);
                if (posToStart == -1)
                {
                    // Removed the last page. We are resetting the cursor and stopping iteration.
                    break;
                }
            }
            while (bufferEnumerator.MoveNext());
            placeToStart.PageEnumerator = bufferEnumerator;
            placeToStart.PagePos = posToStart;
            placeToStart.RelSeqPos = relSeqPos;
            UnlockOutputBuffer();
            return placeToStart;
        }

        private void addBufferPage(int writeLength)
        {
            BufferPage bufferPage;
            UnlockOutputBuffer();
            while (!_pool.TryDequeue(out bufferPage)) ;
            LockOutputBuffer();
            {
                // Grabbed a page from the pool
                if (bufferPage.PageBytes.Length < writeLength)
                {
                    // Page isn't big enough. Throw it away and create a bigger one
                    bufferPage.PageBytes = new byte[writeLength];
                }
            }
            bufferPage.NumMessages = 0;
            bufferPage.curLength = 0;
            _bufferQ.Enqueue(ref bufferPage);
        }

        // Assumed that the caller releases the lock acquired here
        internal BufferPage getWritablePage(int writeLength)
        {
            if (_pool == null)
            {
                _pool = new ConcurrentQueue<BufferPage>();
                for (int i = 0; i < MaxBufferPages; i++)
                {
                    var bufferPageBytes = new byte[Math.Max(defaultPageSize, writeLength)];
                    var bufferPage = new BufferPage(bufferPageBytes);
                    _pool.Enqueue(bufferPage);
                }
            }
            LockOutputBuffer();
            if (_bufferQ.IsEmpty())
            {
                // Q is empty, must add an empty page
                addBufferPage(writeLength);
            }
            else
            {
                // There is something already in the buffer. Check it out.
                var outPage = _bufferQ.PeekLast();
                if ((outPage.PageBytes.Length - outPage.curLength) < writeLength)
                {
                    // Not enough space on last page. Add another
                    addBufferPage(writeLength);
                }
            }
            var retVal = _bufferQ.PeekLast();
            return retVal;
        }
    }
}

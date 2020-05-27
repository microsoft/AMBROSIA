using System;
using Microsoft.VisualStudio.Threading;
using Microsoft.Win32.SafeHandles;
using FASTER.core;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CRA.ClientLibrary;

namespace Ambrosia
{
    /// <summary>
    /// Internal class, wraps Overlapped structure, completion port callback and IAsyncResult
    /// </summary>
    sealed class AsyncJob : IAsyncResult, IDisposable
    {
        #region privates
        private readonly object _eventHandle = new object();
        private bool _completedSynchronously = false;
        private bool _completed = false;
        private uint _errorCode = 0;
        #endregion

        public void SetEventHandle()
        {
            lock (_eventHandle)
            {
                _completed = true;
                Monitor.Pulse(_eventHandle);
            }
        }

        public AsyncJob()
        {
        }

        #region IDisposable

        bool _disposed = false;
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            GC.SuppressFinalize(this);
        }

        #endregion

        public void CompleteSynchronously()
        {
            _completedSynchronously = true;
        }

        public void WaitForCompletion()
        {
            lock (_eventHandle)
            {
                while (!_completed)
                    Monitor.Wait(_eventHandle);
            }
        }

        public uint ErrorCode { get { return _errorCode; } }

        #region IAsyncResult Members

        public object AsyncState
        {
            get { return null; }
        }

        public WaitHandle AsyncWaitHandle
        {
            get { return null; }
        }

        public bool CompletedSynchronously
        {
            get { return _completedSynchronously; }
        }

        public bool IsCompleted
        {
            get { return _completed; }
        }
        #endregion
    }

    internal class LocalStorageDevice : IDisposable
    {
        /// <summary>
        /// File information
        /// </summary>
        private readonly string filename;
        private readonly SafeFileHandle logHandle;
        private readonly bool enablePrivileges;
        private readonly bool useIoCompletionPort;

        /// <summary>
        /// Device Information obtained from Native32 methods
        /// </summary>
        private uint lpSectorsPerCluster;
        private uint lpBytesPerSector;
        private uint lpNumberOfFreeClusters;
        private uint lpTotalNumberOfClusters;
        private IntPtr ioCompletionPort;

        public LocalStorageDevice(string filename, bool enablePrivileges = false,
            bool useIoCompletionPort = false, bool unbuffered = false, bool deleteOnClose = false)
        {
            this.filename = filename;
            this.enablePrivileges = enablePrivileges;
            this.useIoCompletionPort = useIoCompletionPort;

            if (enablePrivileges)
            {
                Native32.EnableProcessPrivileges();
            }

            Native32.GetDiskFreeSpace(filename.Substring(0, 3),
                                        out lpSectorsPerCluster,
                                        out lpBytesPerSector,
                                        out lpNumberOfFreeClusters,
                                        out lpTotalNumberOfClusters);

            uint fileAccess = Native32.GENERIC_READ | Native32.GENERIC_WRITE;
            //            uint fileShare = unchecked(((uint)FileShare.ReadWrite & ~(uint)FileShare.Inheritable));
            uint fileShare = unchecked(((uint)FileShare.Read & ~(uint)FileShare.Inheritable));
            uint fileCreation = unchecked((uint)FileMode.OpenOrCreate);
            uint fileFlags = Native32.FILE_FLAG_OVERLAPPED;

            if (unbuffered)
                fileFlags = fileFlags | Native32.FILE_FLAG_NO_BUFFERING;

            if (deleteOnClose)
                fileFlags = fileFlags | Native32.FILE_FLAG_DELETE_ON_CLOSE;

            logHandle = Native32.CreateFileW(filename,
                                             fileAccess,
                                             fileShare,
                                             IntPtr.Zero,
                                             fileCreation,
                                             fileFlags,
                                             IntPtr.Zero);

            if (enablePrivileges)
            {
                Native32.EnableVolumePrivileges(filename, logHandle);
            }

            if (useIoCompletionPort)
            {
                ioCompletionPort = Native32.CreateIoCompletionPort(
                    logHandle,
                    IntPtr.Zero,
                    (uint)logHandle.DangerousGetHandle().ToInt64(),
                    0);
            }
            ThreadPool.BindHandle(logHandle);
        }

        public void Dispose()
        {
            logHandle.Close();
        }

        /// <summary>
        /// Sets file size to the specified value -- DOES NOT reset file seek pointer to original location
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public bool SetFileSize(long size)
        {
            if (enablePrivileges)
                return Native32.SetFileSize(logHandle, size);
            else
            {
                int lodist = (int)size;
                int hidist = (int)(size >> 32);
                Native32.SetFilePointer(logHandle, lodist, ref hidist, Native32.EMoveMethod.Begin);
                if (!Native32.SetEndOfFile(logHandle)) return false;
                return true;
            }
        }

        public uint GetSectorSize()
        {
            return lpBytesPerSector;
        }

        public unsafe void AsyncRead(ulong sourceAddress,
                                     IntPtr destinationAddress,
                                     uint readLength,
                                     IAsyncResult asyncResult)
        {
            Overlapped ov = new Overlapped
            {
                AsyncResult = asyncResult,
                OffsetLow = unchecked((int)(sourceAddress & 0xFFFFFFFF)),
                OffsetHigh = unchecked((int)((sourceAddress >> 32) & 0xFFFFFFFF))
            };

            NativeOverlapped* ovNative = ov.UnsafePack(null, IntPtr.Zero);

            /* Invoking the Native method ReadFile provided by Kernel32.dll
             * library. Returns false, if request failed or accepted for async 
             * operation. Returns true, if success synchronously.
             */
            bool result = Native32.ReadFile(logHandle,
                                destinationAddress,
                                readLength,
                                out uint bytesRead,
                                ovNative);

            if (!result)
            {
                int error = Marshal.GetLastWin32Error();

                /* Just handle the case when it is not ERROR_IO_PENDING
                 * If ERROR_IO_PENDING, then it is accepted for async execution
                 */
                if (error != Native32.ERROR_IO_PENDING)
                {
                    Overlapped.Unpack(ovNative);
                    Overlapped.Free(ovNative);
                    throw new Exception("Error reading from log file: " + error);
                }
            }
            else
            {
                //executed synchronously, so process callback
                //callback(0, bytesRead, ovNative);
            }
        }

        public unsafe void AsyncRead(ulong sourceAddress,
                                     IntPtr destinationAddress,
                                     uint readLength,
                                     IOCompletionCallback callback,
                                     IAsyncResult asyncResult)
        {
            Overlapped ov = new Overlapped(0, 0, IntPtr.Zero, asyncResult);
            NativeOverlapped* ovNative = ov.UnsafePack(callback, IntPtr.Zero);
            ovNative->OffsetLow = unchecked((int)((ulong)sourceAddress & 0xFFFFFFFF));
            ovNative->OffsetHigh = unchecked((int)(((ulong)sourceAddress >> 32) & 0xFFFFFFFF));

            uint bytesRead = default(uint);
            bool result = Native32.ReadFile(logHandle,
                                            destinationAddress,
                                            readLength,
                                            out bytesRead,
                                            ovNative);

            if (!result)
            {
                int error = Marshal.GetLastWin32Error();
                if (error != Native32.ERROR_IO_PENDING)
                {
                    Overlapped.Unpack(ovNative);
                    Overlapped.Free(ovNative);

                    // NOTE: alignedDestinationAddress needs to be freed by whoever catches the exception
                    throw new Exception("Error reading from log file: " + error);
                }
            }
            else
            {
                // On synchronous completion, issue callback directly
                callback(0, bytesRead, ovNative);
            }
        }

        public unsafe void AsyncWrite(IntPtr sourceAddress,
                                      ulong destinationAddress,
                                      uint numBytesToWrite,
                                      IOCompletionCallback callback,
                                      NativeOverlapped* ovNative)
        //                                      IAsyncResult asyncResult)
        {
            //            Overlapped ov = new Overlapped(0, 0, IntPtr.Zero, asyncResult);
            //            NativeOverlapped* ovNative = ov.UnsafePack(callback, IntPtr.Zero);
            ovNative->OffsetLow = unchecked((int)(destinationAddress & 0xFFFFFFFF));
            ovNative->OffsetHigh = unchecked((int)((destinationAddress >> 32) & 0xFFFFFFFF));


            /* Invoking the Native method WriteFile provided by Kernel32.dll
            * library. Returns false, if request failed or accepted for async 
            * operation. Returns true, if success synchronously.
            */
            bool result = Native32.WriteFile(logHandle,
                                    sourceAddress,
                                    numBytesToWrite,
                                    out uint bytesWritten,
                                    ovNative);

            if (!result)
            {
                int error = Marshal.GetLastWin32Error();
                /* Just handle the case when it is not ERROR_IO_PENDING
                 * If ERROR_IO_PENDING, then it is accepted for async execution
                 */
                if (error != Native32.ERROR_IO_PENDING)
                {
                    Overlapped.Unpack(ovNative);
                    Overlapped.Free(ovNative);
                    throw new Exception("Error writing to log file: " + error);
                }
            }
            else
            {
                //executed synchronously, so process callback
                callback(0, bytesWritten, ovNative);
            }
        }
    }

    internal class LogWriterWindows : IDisposable, ILogWriter
    {
        unsafe struct BytePtrWrapper
        {
            public GCHandle _handle;
            public byte* _ptr;
        }

        unsafe struct IOThreadState
        {
            public LocalStorageDevice filePointer;
            public NativeOverlapped* ov_native;
        }

        public const int sectorSize = 4096;

        BytePtrWrapper _buf;
        IOThreadState[] _IOThreadInfo;
        IOThreadState[] _IOThreadInfoAsync;
        ulong _bufBytesOccupied;
        uint _chunkSize;
        uint _maxChunksPerWrite;
        ulong _filePos;
        long _outstandingWrites;
        public AsyncQueue<int> _writesFinishedQ;
        long _writesFinished;
        uint _bufSize;
        ulong _fileSize;
        uint _allocationUnit;
        uint _allocations;
        uint _lastError;

        public unsafe LogWriterWindows(string fileName,
                                       uint chunkSize,
                                       uint maxChunksPerWrite,
                                       bool appendOpen = false)
        {
            //Console.WriteLine("64-bitness: " + Environment.Is64BitProcess);
            _lastError = 0;
            _allocationUnit = 1024 * 1024 * 512;
            _allocations = 0;
            _writesFinished = 1;
            _writesFinishedQ = new AsyncQueue<int>();
            _bufSize = maxChunksPerWrite * chunkSize + sectorSize;
            _buf = new BytePtrWrapper();
            var tempBuf = new byte[_bufSize];
            _buf._handle = GCHandle.Alloc(tempBuf, GCHandleType.Pinned);
            _buf._ptr = (byte*)_buf._handle.AddrOfPinnedObject();

            _IOThreadInfo = new IOThreadState[maxChunksPerWrite];
            _IOThreadInfoAsync = new IOThreadState[maxChunksPerWrite];
            var filePointer = new LocalStorageDevice(fileName, true, false, true);
            for (int i = 0; i < maxChunksPerWrite; i++)
            {
                var job = new AsyncJob();
                var jobAsync = new AsyncJob();
                var ov = new Overlapped(0, 0, IntPtr.Zero, job);
                var ovAsync = new Overlapped(0, 0, IntPtr.Zero, jobAsync);
                NativeOverlapped* ov_native = ov.UnsafePack(FlushCallback, IntPtr.Zero);
                NativeOverlapped* ov_nativeAsync = ovAsync.UnsafePack(FlushAsyncCallBack, IntPtr.Zero);
                Thread.Sleep(10);
                var myIOThreadState = new IOThreadState();
                myIOThreadState.filePointer = filePointer;
                myIOThreadState.ov_native = ov_native;
                _IOThreadInfo[i] = myIOThreadState;
                var myIOThreadStateAsync = new IOThreadState();
                myIOThreadStateAsync.filePointer = filePointer;
                myIOThreadStateAsync.ov_native = ov_nativeAsync;
                _IOThreadInfoAsync[i] = myIOThreadStateAsync;
            }
            _bufBytesOccupied = 0;
            _chunkSize = chunkSize;
            _maxChunksPerWrite = maxChunksPerWrite;
            if (!appendOpen)
            {
                _fileSize = 0;
                _filePos = 0;
            }
            else
            {
                // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!inefficient workaround until Badrish adds a filesize call
                using (var fileReader = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    _fileSize = _filePos = (ulong)fileReader.Length;
                }
            }
            _outstandingWrites = 0;
        }

        public ulong FileSize { get { return _fileSize; } }

        public void Dispose()
        {
            _IOThreadInfoAsync[0].filePointer.SetFileSize((long)(((_fileSize - 1) / sectorSize + 1) * sectorSize));
            _buf._handle.Free();
            _IOThreadInfoAsync[0].filePointer.Dispose();
        }

        [DllImport("msvcrt.dll", SetLastError = false)]
        static extern IntPtr memcpy(IntPtr dest, IntPtr src, int count);
        [DllImport("msvcrt.dll", SetLastError = false)]
        static extern IntPtr memmove(IntPtr dest, IntPtr src, int count);

        internal unsafe void CopyBufferIntoUnmanaged(byte[] buffer,
                                              ulong bufoffset,
                                              ulong offset,
                                              ulong blength)
        {
            fixed (byte* p = buffer)
            {
                memcpy((IntPtr)(_buf._ptr + offset), (IntPtr)(p + bufoffset), (int)blength);
            }
        }

        internal unsafe void CopyToBeginningUnmanaged(ulong fromOffset,
                                                      ulong length)
        {
            memmove((IntPtr)(_buf._ptr), (IntPtr)(_buf._ptr + fromOffset), (int)length);
        }

        public unsafe void FlushCallback(uint errorCode, uint numBytes, NativeOverlapped* pOVERLAP)
        {
            if (errorCode != 0)
            {
                _lastError = errorCode;
            }
            var result = Interlocked.Decrement(ref _outstandingWrites);
            if (result == 0)
            {
                _writesFinished = 1;
            }
        }

        public unsafe void FlushAsyncCallBack(uint errorCode, uint numBytes, NativeOverlapped* pOVERLAP)
        {
            if (errorCode != 0)
            {
                _lastError = errorCode;
            }
            var result = Interlocked.Decrement(ref _outstandingWrites);
            if (result == 0)
            {
                _writesFinishedQ.Enqueue(0);
            }
        }

        public void WaitOnWritesFinished()
        {
            while (Interlocked.Read(ref _writesFinished) == 0) { Thread.Yield(); }
            if (_lastError > 0)
            {
                throw new Exception("Error " + _lastError.ToString() + " writing data to log");
            }
        }

        private unsafe void DoWrite(ulong writePosInBuffer,
                                    ulong filePos,
                                    ulong chunkNum,
                                    uint length)
        {
            _IOThreadInfo[chunkNum].filePointer.AsyncWrite((IntPtr)(_buf._ptr + writePosInBuffer), filePos, length, FlushCallback, _IOThreadInfo[chunkNum].ov_native);
        }

        private unsafe void DoWriteForAsyncWrites(ulong writePosInBuffer,
                                                  ulong filePos,
                                                  ulong chunkNum,
                                                  uint length)
        {
            _IOThreadInfoAsync[chunkNum].filePointer.AsyncWrite((IntPtr)(_buf._ptr + writePosInBuffer), filePos, length, FlushAsyncCallBack, _IOThreadInfoAsync[chunkNum].ov_native);
        }

        public void Flush()
        {
            long newAllocations = ((long)_fileSize - 1) / _allocationUnit + 1;
            if (_allocations < newAllocations)
            {
                _IOThreadInfoAsync[0].filePointer.SetFileSize(newAllocations * _allocationUnit);
                _allocations = (uint)newAllocations;
            }
            ulong numFullChunksToWrite = _bufBytesOccupied / _chunkSize;
            uint finalChunkSize = (uint)(_bufBytesOccupied % _chunkSize);
            if (numFullChunksToWrite == _maxChunksPerWrite)
            {
                numFullChunksToWrite--;
                finalChunkSize += _chunkSize;
            }
            ulong curWritePos = 0;
            if (finalChunkSize == 0)
            {
                _outstandingWrites = (long)numFullChunksToWrite;
            }
            else
            {
                _outstandingWrites = (long)numFullChunksToWrite + 1;
            }
            _writesFinished = 0;
            for (ulong i = 0; i < numFullChunksToWrite; i++)
            {
                DoWrite(curWritePos, _filePos + curWritePos, i, _chunkSize);
                curWritePos += _chunkSize;
            }
            _bufBytesOccupied = finalChunkSize % sectorSize;
            if (_bufBytesOccupied != 0)
            {
                uint finalWriteSize = (uint)((finalChunkSize - 1) / sectorSize + 1) * sectorSize;
                DoWrite(curWritePos, _filePos + curWritePos, numFullChunksToWrite, finalWriteSize);
                curWritePos += finalWriteSize;
                WaitOnWritesFinished();
                _filePos = _filePos + curWritePos - sectorSize;
                CopyToBeginningUnmanaged(curWritePos - sectorSize, _bufBytesOccupied);
            }
            else
            {
                if (finalChunkSize != 0)
                {
                    DoWrite(curWritePos, _filePos + curWritePos, numFullChunksToWrite, finalChunkSize);
                    WaitOnWritesFinished();
                }
                else
                {
                    WaitOnWritesFinished();
                }
                _filePos += curWritePos + finalChunkSize;
            }
        }

        public async Task FlushAsync()
        {
            long newAllocations = ((long)_fileSize - 1) / _allocationUnit + 1;
            if (_allocations < newAllocations)
            {
                _IOThreadInfoAsync[0].filePointer.SetFileSize(newAllocations * _allocationUnit);
                _allocations = (uint)newAllocations;
            }
            ulong numFullChunksToWrite = _bufBytesOccupied / _chunkSize;
            uint finalChunkSize = (uint)(_bufBytesOccupied % _chunkSize);
            if (numFullChunksToWrite == _maxChunksPerWrite)
            {
                numFullChunksToWrite--;
                finalChunkSize += _chunkSize;
            }
            ulong curWritePos = 0;
            if (finalChunkSize == 0)
            {
                _outstandingWrites = (long)numFullChunksToWrite;
            }
            else
            {
                _outstandingWrites = (long)numFullChunksToWrite + 1;
            }
            for (ulong i = 0; i < numFullChunksToWrite; i++)
            {
                DoWriteForAsyncWrites(curWritePos, _filePos + curWritePos, i, _chunkSize);
                curWritePos += _chunkSize;
            }
            _bufBytesOccupied = finalChunkSize % sectorSize;
            if (_bufBytesOccupied != 0)
            {
                uint finalWriteSize = (uint)((finalChunkSize - 1) / sectorSize + 1) * sectorSize;
                DoWriteForAsyncWrites(curWritePos, _filePos + curWritePos, numFullChunksToWrite, finalWriteSize);
                curWritePos += finalWriteSize;
                await _writesFinishedQ.DequeueAsync();
                _filePos = _filePos + curWritePos - sectorSize;
                CopyToBeginningUnmanaged(curWritePos - sectorSize, _bufBytesOccupied);
            }
            else
            {
                if (finalChunkSize != 0)
                {
                    DoWriteForAsyncWrites(curWritePos, _filePos + curWritePos, numFullChunksToWrite, finalChunkSize);
                    await _writesFinishedQ.DequeueAsync();
                }
                else
                {
                    await _writesFinishedQ.DequeueAsync();
                }
                _filePos += curWritePos + finalChunkSize;
            }
            if (_lastError > 0)
            {
                throw new Exception("Error " + _lastError.ToString() + " writing data to log");
            }
        }

        public void Write(byte[] buffer,
                          ulong offset,
                          ulong length)
        {
            _fileSize += length;
            while (length + _bufBytesOccupied > _bufSize)
            {
                ulong bufferToWrite = _bufSize - _bufBytesOccupied;
                CopyBufferIntoUnmanaged(buffer, offset, _bufBytesOccupied, bufferToWrite);
                length -= bufferToWrite;
                offset += bufferToWrite;
                _bufBytesOccupied = _bufSize;
                Flush();
            }
            CopyBufferIntoUnmanaged(buffer, offset, _bufBytesOccupied, length);
            _bufBytesOccupied += length;
        }

        public void Write(byte[] buffer,
                          ulong offset,
                          int ilength)
        {
            ulong length = (ulong)ilength;
            _fileSize += length;
            while (length + _bufBytesOccupied > _bufSize)
            {
                ulong bufferToWrite = _bufSize - _bufBytesOccupied;
                CopyBufferIntoUnmanaged(buffer, offset, _bufBytesOccupied, bufferToWrite);
                length -= bufferToWrite;
                offset += bufferToWrite;
                _bufBytesOccupied = _bufSize;
                Flush();
            }
            CopyBufferIntoUnmanaged(buffer, offset, _bufBytesOccupied, length);
            _bufBytesOccupied += length;
        }

        public void Write(byte[] buffer,
                          int offset,
                          int ilength)
        {
            ulong length = (ulong)ilength;
            _fileSize += length;
            while (length + _bufBytesOccupied > _bufSize)
            {
                ulong bufferToWrite = _bufSize - _bufBytesOccupied;
                CopyBufferIntoUnmanaged(buffer, (ulong)offset, _bufBytesOccupied, bufferToWrite);
                length -= bufferToWrite;
                offset += (int)bufferToWrite;
                _bufBytesOccupied = _bufSize;
                Flush();
            }
            CopyBufferIntoUnmanaged(buffer, (ulong)offset, _bufBytesOccupied, length);
            _bufBytesOccupied += length;
        }

        public async Task WriteAsync(byte[] buffer,
                                     ulong offset,
                                     ulong length)
        {
            _fileSize += length;
            while (length + _bufBytesOccupied > _bufSize)
            {
                ulong bufferToWrite = _bufSize - _bufBytesOccupied;
                CopyBufferIntoUnmanaged(buffer, offset, _bufBytesOccupied, bufferToWrite);
                length -= bufferToWrite;
                offset += bufferToWrite;
                _bufBytesOccupied = _bufSize;
                await FlushAsync();
            }
            CopyBufferIntoUnmanaged(buffer, offset, _bufBytesOccupied, length);
            _bufBytesOccupied += length;
        }
        public async Task WriteAsync(byte[] buffer,
                                     ulong offset,
                                     int iLength)
        {
            ulong length = (ulong)iLength;
            _fileSize += length;
            while (length + _bufBytesOccupied > _bufSize)
            {
                ulong bufferToWrite = _bufSize - _bufBytesOccupied;
                CopyBufferIntoUnmanaged(buffer, offset, _bufBytesOccupied, bufferToWrite);
                length -= bufferToWrite;
                offset += bufferToWrite;
                _bufBytesOccupied = _bufSize;
                await FlushAsync();
            }
            CopyBufferIntoUnmanaged(buffer, offset, _bufBytesOccupied, length);
            _bufBytesOccupied += length;
        }

        public async Task WriteAsync(byte[] buffer,
                                     int offset,
                                     int iLength)
        {
            ulong length = (ulong)iLength;
            _fileSize += length;
            while (length + _bufBytesOccupied > _bufSize)
            {
                ulong bufferToWrite = _bufSize - _bufBytesOccupied;
                CopyBufferIntoUnmanaged(buffer, (ulong)offset, _bufBytesOccupied, bufferToWrite);
                length -= bufferToWrite;
                offset += (int)bufferToWrite;
                _bufBytesOccupied = _bufSize;
                await FlushAsync();
            }
            CopyBufferIntoUnmanaged(buffer, (ulong)offset, _bufBytesOccupied, length);
            _bufBytesOccupied += length;
        }

        public unsafe void WriteByte(byte val)
        {
            _fileSize++;
            _buf._ptr[_bufBytesOccupied] = val;
            _bufBytesOccupied++;
            if (_bufBytesOccupied == _bufSize)
            {
                Flush();
            }
        }
        public unsafe void WriteInt(int value)
        {
            var zigZagEncoded = unchecked((uint)((value << 1) ^ (value >> 31)));
            while ((zigZagEncoded & ~0x7F) != 0)
            {
                WriteByte((byte)((zigZagEncoded | 0x80) & 0xFF));
                zigZagEncoded >>= 7;
            }
            WriteByte((byte)zigZagEncoded);
        }

        public void WriteIntFixed(int value)
        {
            WriteByte((byte)(value & 0xFF));
            WriteByte((byte)((value >> 0x8) & 0xFF));
            WriteByte((byte)((value >> 0x10) & 0xFF));
            WriteByte((byte)((value >> 0x18) & 0xFF));
        }

        public void WriteLongFixed(long value)
        {
            WriteByte((byte)(value & 0xFF));
            WriteByte((byte)((value >> 0x8) & 0xFF));
            WriteByte((byte)((value >> 0x10) & 0xFF));
            WriteByte((byte)((value >> 0x18) & 0xFF));
            WriteByte((byte)((value >> 0x20) & 0xFF));
            WriteByte((byte)((value >> 0x28) & 0xFF));
            WriteByte((byte)((value >> 0x30) & 0xFF));
            WriteByte((byte)((value >> 0x38) & 0xFF));
        }
    }

    internal class LogWriterStaticsWindows : ILogWriterStatic
    {
        public void CreateDirectoryIfNotExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public void DeleteFile(string path)
        {
            File.Delete(path);
        }

        public ILogWriter Generate(string fileName,
                                   uint chunkSize,
                                   uint maxChunksPerWrite,
                                   bool appendOpen = false)
        {
            return new LogWriterWindows(fileName, chunkSize, maxChunksPerWrite, appendOpen);
        }
    }

    internal class LogWriterGeneric : IDisposable, ILogWriter
    {
        FileStream _logStream;
        public unsafe LogWriterGeneric(string fileName,
                                       bool appendOpen = false)
        {
            _logStream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read & ~FileShare.Inheritable);
            if (appendOpen)
            {
                _logStream.Position = _logStream.Length;
            }
        }
        public ulong FileSize { get { return (ulong)_logStream.Length; } }

        public void Dispose()
        {
            _logStream.Close();
        }
        public void Flush()
        {
            _logStream.Flush();
        }

        public async Task FlushAsync()
        {
            await _logStream.FlushAsync();
        }

        public unsafe void WriteInt(int value)
        {
            _logStream.WriteInt(value);
        }
        public void WriteIntFixed(int value)
        {
            _logStream.WriteIntFixed(value);
        }

        public void WriteLongFixed(long value)
        {
            _logStream.WriteLongFixed(value);
        }
        public void Write(byte[] buffer,
                          int offset,
                          int length)
        {
            _logStream.Write(buffer, offset, length);
        }

        public async Task WriteAsync(byte[] buffer,
                                     int offset,
                                     int length)
        {
            await _logStream.WriteAsync(buffer, offset, length);
        }
    }

    internal class LogWriterStaticsGeneric : ILogWriterStatic
    {
        public void CreateDirectoryIfNotExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public void DeleteFile(string path)
        {
            File.Delete(path);
        }

        public ILogWriter Generate(string fileName,
                                   uint chunkSize,
                                   uint maxChunksPerWrite,
                                   bool appendOpen = false)
        {
            return new LogWriterGeneric(fileName, appendOpen);
        }
    }

    public class GenericFileLogReader : ILogReader
    {
        Stream stream;

        public long Position
        {
            get { return stream.Position; }
            set { stream.Position = value; }
        }

        public GenericFileLogReader(string fileName)
        {
            stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        public async Task<Tuple<int, int>> ReadIntAsync(byte[] buffer)
        {
            return await stream.ReadIntAsync(buffer);
        }

        public async Task<Tuple<int, int>> ReadIntAsync(byte[] buffer, CancellationToken ct)
        {
            return await stream.ReadIntAsync(buffer, ct);
        }

        public Tuple<int, int> ReadInt(byte[] buffer)
        {
            return stream.ReadInt(buffer);
        }

        public int ReadInt()
        {
            return stream.ReadInt();
        }

        public async Task<int> ReadAllRequiredBytesAsync(byte[] buffer,
                                                        int offset,
                                                        int count,
                                                        CancellationToken ct)
        {
            return await stream.ReadAllRequiredBytesAsync(buffer, offset, count, ct);
        }

        public async Task<int> ReadAllRequiredBytesAsync(byte[] buffer,
                                                int offset,
                                                int count)
        {
            return await stream.ReadAllRequiredBytesAsync(buffer, offset, count);
        }

        public int ReadAllRequiredBytes(byte[] buffer,
                                       int offset,
                                       int count)
        {
            return stream.ReadAllRequiredBytes(buffer, offset, count);
        }

        public long ReadLongFixed()
        {
            return stream.ReadLongFixed();
        }

        public int ReadIntFixed()
        {
            return stream.ReadIntFixed();
        }

        public byte[] ReadByteArray()
        {
            return stream.ReadByteArray();
        }

        public int ReadByte()
        {
            return stream.ReadByte();
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            return stream.Read(buffer, offset, count);
        }

        public void Dispose()
        {
            stream.Dispose();
        }
    }

    internal class GenericFileLogReaderStatics : ILogReaderStatic
    {
        public ILogReader Generate(string fileName)
        {
            return new GenericFileLogReader(fileName);
        }
    }


    public static class GenericLogsInterface
    {
        public static void SetToGenericLogs()
        {
            LogReaderStaticPicker.curStatic = new GenericFileLogReaderStatics();
#if NETFRAMEWORK
            LogWriterStaticPicker.curStatic = new LogWriterStaticsWindows();
#endif
#if NETSTANDARD
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                LogWriterStaticPicker.curStatic = new LogWriterStaticsWindows();
            }
            else
            {
                LogWriterStaticPicker.curStatic = new LogWriterStaticsGeneric();
            }
#endif
        }
    }
}

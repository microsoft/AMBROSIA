using System;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace Ambrosia
{
    public static class StartupParamOverrides
    {
        public static int receivePort = -1;
        public static int sendPort = -1;
        public static TextWriter OutputStream = Console.Out;
        public static string ICReceivePipeName;
        public static string ICSendPipeName;
        public static string ICLogLocation = null;
    }

    // Constants for leading byte communicated between services;
    public static class AmbrosiaRuntimeLBConstants
    {
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
    }

    public static class RpcTypes
    {
        public enum RpcType : byte
        {
            ReturnValue = 0,
            FireAndForget = 1,
            Impulse = 2,
        }

        public static bool IsFireAndForget(this RpcType rpcType)
        {
            return rpcType == RpcType.FireAndForget || rpcType == RpcType.Impulse;
        }
    }
    public enum ReturnValueTypes
    {
        None = 0,
        ReturnValue = 1,
        EmptyReturnValue = 2,
        Exception = 3,
    }
}

namespace Ambrosia
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

    public interface ILogWriterStatic
    {
        void CreateDirectoryIfNotExists(string path);

        bool DirectoryExists(string path);

        bool FileExists(string path);

        void DeleteFile(string path);

        ILogWriter Generate(string fileName,
                            uint chunkSize,
                            uint maxChunksPerWrite,
                            bool appendOpen = false);
    }

    public static class LogWriterStaticPicker
    {
        public static ILogWriterStatic curStatic { get; set; }
    }

    public interface ILogReaderStatic
    {
        ILogReader Generate(string fileName);
    }

    public static class LogReaderStaticPicker
    {
        public static ILogReaderStatic curStatic { get; set; }
    }

    public interface ILogReader : IDisposable
    {
        long Position { get; set; }
        Task<Tuple<int, int>> ReadIntAsync(byte[] buffer);
        Task<Tuple<int, int>> ReadIntAsync(byte[] buffer, CancellationToken ct);
        Tuple<int, int> ReadInt(byte[] buffer);
        int ReadInt();
        Task<int> ReadAllRequiredBytesAsync(byte[] buffer,
                                            int offset,
                                            int count,
                                            CancellationToken ct);
        Task<int> ReadAllRequiredBytesAsync(byte[] buffer,
                                            int offset,
                                            int count);
        int ReadAllRequiredBytes(byte[] buffer,
                                       int offset,
                                       int count);
        long ReadLongFixed();
        int ReadIntFixed();
        byte[] ReadByteArray();
        int ReadByte();
        int Read(byte[] buffer, int offset, int count);
    }

    // Flexible size byte array which data can be deserialized into. The size of the buffer adjusts to the largest deserialize request
    // made so far.
    // Needs to be public so it is accessible to generated code
    public class FlexReadBuffer
    {
        static Queue<byte[]> _bufferPool = new Queue<byte[]>();
        byte[] _buffer;
        int _curSize;
        protected int _curSizeSize;
        byte[] _sizeBuf;

        // Gets a buffer from the buffer pool. Replaces an existing buffer from the pool if the one in
        // the pool isn't big enough. The buffer should be returned to the pool when not needed. This
        // allows many tasks to own FlexReadBuffers without actually taking up significant buffer space.
        private void GetBuffer()
        {
            lock (_bufferPool)
            {
                if (_bufferPool.Count > 0)
                {
                    _buffer = _bufferPool.Dequeue();
                }
            }
            if (_buffer == null || _buffer.Length < _curSize)
            {
                _buffer = new byte[_curSize];
            }
            System.Buffer.BlockCopy(_sizeBuf, 0, _buffer, 0, _curSizeSize);
        }

        public FlexReadBuffer()
        {
            _sizeBuf = new byte[5];
            _buffer = null;
            _curSize = 0;
            _curSizeSize = 0;
        }

        // Returns the current contents of the buffer
        public byte[] Buffer { get { return _buffer; } }

        // Returns the number of bytes copied in the last deserialize call
        public int Length { get { return _curSize; } }

        // Returns the number of bytes used at the beginning of the buffer to represent length
        public int LengthLength { get { return _curSizeSize; } }

        public void ThrowAwayBuffer()
        {
            _buffer = null;
            _curSize = 0;
            _curSizeSize = 0;
        }

        public void ResetBuffer()
        {
            if (_buffer != null)
            {
                lock (_bufferPool)
                {
                    _bufferPool.Enqueue(_buffer);
                }
                _buffer = null;
                _curSize = 0;
                _curSizeSize = 0;
            }
        }

        public void StealBuffer()
        {
            if (_buffer != null)
            {
                _buffer = null;
                _curSize = 0;
                _curSizeSize = 0;
            }
        }

        public static void ReturnBuffer(byte[] returnedBuffer)
        {
            lock (_bufferPool)
            {
                _bufferPool.Enqueue(returnedBuffer);
            }
        }


        // Deserializes a byte array from a .net stream assuming that the first 4 bytes contains the length of the byte array to be
        // subsequently copied. If the specified number of bytes can't be read before hitting end of stream, an exception is thrown.
        static public async Task<FlexReadBuffer> DeserializeAsync(Stream S,
                                                                  FlexReadBuffer flexBuf,
                                                                  CancellationToken ct)
        {
            var intReaderTask = S.ReadIntAsync(flexBuf._sizeBuf, ct);
            var messageSize = await intReaderTask;
            flexBuf._curSize = messageSize.Item1 + messageSize.Item2;
            flexBuf._curSizeSize = messageSize.Item2;
            if (flexBuf.Buffer != null)
            {
                throw new Exception("Flexbuffer should have been null in Deserialize");
            }
            flexBuf.GetBuffer();
            var bytesReaderTask = S.ReadAllRequiredBytesAsync(flexBuf._buffer, messageSize.Item2, messageSize.Item1, ct);
            var bytesRead = await bytesReaderTask;
            if (bytesRead < messageSize.Item1)
                throw new Exception("Error deserializing buffer in stream");
            return flexBuf;
        }


        // Deserializes a byte array from a .net stream assuming that the first 4 bytes contains the length of the byte array to be
        // subsequently copied. If the specified number of bytes can't be read before hitting end of stream, an exception is thrown.
        static public async Task<FlexReadBuffer> DeserializeAsync(Stream S,
                                                                  FlexReadBuffer flexBuf)
        {
            var intReaderTask = S.ReadIntAsync(flexBuf._sizeBuf);
            var messageSize = await intReaderTask;
            flexBuf._curSize = messageSize.Item1 + messageSize.Item2;
            flexBuf._curSizeSize = messageSize.Item2;
            if (flexBuf.Buffer != null)
            {
                throw new Exception("Flexbuffer should have been null in Deserialize");
            }
            flexBuf.GetBuffer();
            var bytesReaderTask = S.ReadAllRequiredBytesAsync(flexBuf._buffer, messageSize.Item2, messageSize.Item1);
            var bytesRead = await bytesReaderTask;
            if (bytesRead < messageSize.Item1)
                throw new Exception("Error deserializing buffer in stream");
            return flexBuf;
        }

        static public FlexReadBuffer Deserialize(Stream S,
                                         FlexReadBuffer flexBuf)
        {
            var messageSize = S.ReadInt(flexBuf._sizeBuf);
            flexBuf._curSize = messageSize.Item1 + messageSize.Item2;
            flexBuf._curSizeSize = messageSize.Item2;
            if (flexBuf.Buffer != null)
            {
                throw new Exception("Flexbuffer should have been null in Deserialize");
            }
            flexBuf.GetBuffer();
            var bytesRead = S.ReadAllRequiredBytes(flexBuf._buffer, messageSize.Item2, messageSize.Item1);
            if (bytesRead < messageSize.Item1)
                throw new Exception("Error deserializing buffer in stream");
            return flexBuf;
        }

        // Copies of the above three methods that take a LogReader instead of a Stream
        static public async Task<FlexReadBuffer> DeserializeAsync(ILogReader S,
                                                                  FlexReadBuffer flexBuf,
                                                                  CancellationToken ct)
        {
            var intReaderTask = S.ReadIntAsync(flexBuf._sizeBuf, ct);
            var messageSize = await intReaderTask;
            flexBuf._curSize = messageSize.Item1 + messageSize.Item2;
            flexBuf._curSizeSize = messageSize.Item2;
            if (flexBuf.Buffer != null)
            {
                throw new Exception("Flexbuffer should have been null in Deserialize");
            }
            flexBuf.GetBuffer();
            var bytesReaderTask = S.ReadAllRequiredBytesAsync(flexBuf._buffer, messageSize.Item2, messageSize.Item1, ct);
            var bytesRead = await bytesReaderTask;
            if (bytesRead < messageSize.Item1)
                throw new Exception("Error deserializing buffer in stream");
            return flexBuf;
        }

        static public async Task<FlexReadBuffer> DeserializeAsync(ILogReader S,
                                                                  FlexReadBuffer flexBuf)
        {
            var intReaderTask = S.ReadIntAsync(flexBuf._sizeBuf);
            var messageSize = await intReaderTask;
            flexBuf._curSize = messageSize.Item1 + messageSize.Item2;
            flexBuf._curSizeSize = messageSize.Item2;
            if (flexBuf.Buffer != null)
            {
                throw new Exception("Flexbuffer should have been null in Deserialize");
            }
            flexBuf.GetBuffer();
            var bytesReaderTask = S.ReadAllRequiredBytesAsync(flexBuf._buffer, messageSize.Item2, messageSize.Item1);
            var bytesRead = await bytesReaderTask;
            if (bytesRead < messageSize.Item1)
                throw new Exception("Error deserializing buffer in stream");
            return flexBuf;
        }

        static public FlexReadBuffer Deserialize(ILogReader S,
                                         FlexReadBuffer flexBuf)
        {
            var messageSize = S.ReadInt(flexBuf._sizeBuf);
            flexBuf._curSize = messageSize.Item1 + messageSize.Item2;
            flexBuf._curSizeSize = messageSize.Item2;
            if (flexBuf.Buffer != null)
            {
                throw new Exception("Flexbuffer should have been null in Deserialize");
            }
            flexBuf.GetBuffer();
            var bytesRead = S.ReadAllRequiredBytes(flexBuf._buffer, messageSize.Item2, messageSize.Item1);
            if (bytesRead < messageSize.Item1)
                throw new Exception("Error deserializing buffer in stream");
            return flexBuf;
        }

    }

    public static class StreamCommunicator
    {
        public static void ReadBig(this ILogReader reader,
                             Stream writeToStream,
                             long checkpointSize)
        {
            var blockSize = 1024 * 1024;
            var buffer = new byte[blockSize];
            while (checkpointSize > 0)
            {
                int bytesRead;
                if (checkpointSize >= blockSize)
                {
                    bytesRead = reader.Read(buffer, 0, blockSize);
                }
                else
                {
                    bytesRead = reader.Read(buffer, 0, (int)checkpointSize);
                }
                writeToStream.Write(buffer, 0, bytesRead);
                checkpointSize -= bytesRead;
            }
        }

        public static void Write(this ILogWriter writer,
                           Stream readStream,
                           long checkpointSize)
        {
            var blockSize = 1024 * 1024;
            var buffer = new byte[blockSize];
            while (checkpointSize > 0)
            {
                int bytesRead;
                if (checkpointSize >= blockSize)
                {
                    bytesRead = readStream.Read(buffer, 0, blockSize);
                }
                else
                {
                    bytesRead = readStream.Read(buffer, 0, (int)checkpointSize);
                }
                writer.Write(buffer, 0, bytesRead);
                checkpointSize -= bytesRead;
            }
        }

        public static int ReadIntFixed(this Stream stream)
        {
            var value = new byte[4];
            stream.ReadAllRequiredBytes(value, 0, value.Length);
            int intValue = value[0]
                | (int)value[1] << 0x8
                | (int)value[2] << 0x10
                | (int)value[3] << 0x18;
            return intValue;
        }

        public static long ReadLongFixed(this Stream stream)
        {
            var value = new byte[8];
            stream.ReadAllRequiredBytes(value, 0, value.Length);
            long intValue = value[0]
                | (long)value[1] << 0x8
                | (long)value[2] << 0x10
                | (long)value[3] << 0x18
                | (long)value[4] << 0x20
                | (long)value[5] << 0x28
                | (long)value[6] << 0x30
                | (long)value[7] << 0x38;
            return intValue;
        }

        public static int ReadBufferedIntFixed(this byte[] buf,
                                                    int offset)
        {
            int intValue = buf[0 + offset]
                | (int)buf[1 + offset] << 0x8
                | (int)buf[2 + offset] << 0x10
                | (int)buf[3 + offset] << 0x18;
            return intValue;
        }


        public static async Task<int> ReadIntFixedAsync(this Stream stream, CancellationToken ct)
        {
            var value = new byte[4];
            await stream.ReadAllRequiredBytesAsync(value, 0, value.Length, ct);
            int intValue = value[0]
                           | (int)value[1] << 0x8
                           | (int)value[2] << 0x10
                           | (int)value[3] << 0x18;
            return intValue;
        }

        public static async Task<long> ReadLongFixedAsync(this Stream stream, CancellationToken ct)
        {
            var value = new byte[8];
            await stream.ReadAllRequiredBytesAsync(value, 0, value.Length, ct);
            long intValue = value[0]
                            | (long)value[1] << 0x8
                            | (long)value[2] << 0x10
                            | (long)value[3] << 0x18
                            | (long)value[4] << 0x20
                            | (long)value[5] << 0x28
                            | (long)value[6] << 0x30
                            | (long)value[7] << 0x38;
            return intValue;
        }


        public static void WriteIntFixed(this Stream stream, int value)
        {
            stream.WriteByte((byte)(value & 0xFF));
            stream.WriteByte((byte)((value >> 0x8) & 0xFF));
            stream.WriteByte((byte)((value >> 0x10) & 0xFF));
            stream.WriteByte((byte)((value >> 0x18) & 0xFF));
        }

        public static void WriteLongFixed(this Stream stream, long value)
        {
            stream.WriteByte((byte)(value & 0xFF));
            stream.WriteByte((byte)((value >> 0x8) & 0xFF));
            stream.WriteByte((byte)((value >> 0x10) & 0xFF));
            stream.WriteByte((byte)((value >> 0x18) & 0xFF));
            stream.WriteByte((byte)((value >> 0x20) & 0xFF));
            stream.WriteByte((byte)((value >> 0x28) & 0xFF));
            stream.WriteByte((byte)((value >> 0x30) & 0xFF));
            stream.WriteByte((byte)((value >> 0x38) & 0xFF));
        }

        public static int ReadBufferedInt(this byte[] buf,
                                          int offset)
        {
            var currentByte = (uint)buf[offset];
            byte read = 1;
            uint result = currentByte & 0x7FU;
            int shift = 7;
            while ((currentByte & 0x80) != 0)
            {
                currentByte = (uint)buf[offset + read];
                read++;
                result |= (currentByte & 0x7FU) << shift;
                shift += 7;
                if (read > 5)
                {
                    throw new Exception("Invalid integer value in the input stream.");
                }
            }
            return (int)((-(result & 1)) ^ ((result >> 1) & 0x7FFFFFFFU));
        }

        public static bool EnoughBytesForReadBufferedInt(this byte[] buf,
                                                 int offset,
                                                 int bytes)
        {
            if (bytes >= 5)
            {
                return true;
            }
            for (int i = 0; i < bytes; i++)
            {
                if ((buf[offset + i] & 0x80) == 0)
                {
                    return true;
                }
            }
            return false;
        }

        public static int ReadInt(this Stream stream)
        {
            var currentByte = (uint)stream.ReadByte();
            byte read = 1;
            uint result = currentByte & 0x7FU;
            int shift = 7;
            while ((currentByte & 0x80) != 0)
            {
                currentByte = (uint)stream.ReadByte();
                read++;
                result |= (currentByte & 0x7FU) << shift;
                shift += 7;
                if (read > 5)
                {
                    throw new Exception("Invalid integer value in the input stream.");
                }
            }
            return (int)((-(result & 1)) ^ ((result >> 1) & 0x7FFFFFFFU));
        }

        public static async Task<Tuple<int, int>> ReadIntAsync(this Stream stream,
                                                               byte[] buffer,
                                                               CancellationToken ct)
        {
            buffer[0] = await stream.ReadByteAsync(ct);
            var currentByte = (uint)buffer[0];
            byte read = 1;
            uint result = currentByte & 0x7FU;
            int shift = 7;
            while ((currentByte & 0x80) != 0)
            {
                buffer[read] = await stream.ReadByteAsync(ct);
                currentByte = (uint)buffer[read];
                read++;
                result |= (currentByte & 0x7FU) << shift;
                shift += 7;
                if (read > 5)
                {
                    throw new Exception("Invalid integer value in the input stream.");
                }
            }
            return new Tuple<int, int>((int)((-(result & 1)) ^ ((result >> 1) & 0x7FFFFFFFU)), read);
        }

        public static async Task<Tuple<int, int>> ReadIntAsync(this Stream stream,
                                                               byte[] buffer)
        {
            buffer[0] = await stream.ReadByteAsync();
            var currentByte = (uint)buffer[0];
            byte read = 1;
            uint result = currentByte & 0x7FU;
            int shift = 7;
            while ((currentByte & 0x80) != 0)
            {
                buffer[read] = await stream.ReadByteAsync();
                currentByte = (uint)buffer[read];
                read++;
                result |= (currentByte & 0x7FU) << shift;
                shift += 7;
                if (read > 5)
                {
                    throw new Exception("Invalid integer value in the input stream.");
                }
            }
            return new Tuple<int, int>((int)((-(result & 1)) ^ ((result >> 1) & 0x7FFFFFFFU)), read);
        }

        public static Tuple<int, int> ReadInt(this Stream stream,
                                      byte[] buffer)
        {
            buffer[0] = (byte)stream.ReadByte();
            var currentByte = (uint)buffer[0];
            byte read = 1;
            uint result = currentByte & 0x7FU;
            int shift = 7;
            while ((currentByte & 0x80) != 0)
            {
                buffer[read] = (byte)stream.ReadByte();
                currentByte = (uint)buffer[read];
                read++;
                result |= (currentByte & 0x7FU) << shift;
                shift += 7;
                if (read > 5)
                {
                    throw new Exception("Invalid integer value in the input stream.");
                }
            }
            return new Tuple<int, int>((int)((-(result & 1)) ^ ((result >> 1) & 0x7FFFFFFFU)), read);
        }


        public static async Task<byte> ReadByteAsync(this Stream stream)
        {
            byte[] buffer = new byte[1];
            await stream.ReadAsync(buffer, 0, 1);
            return buffer[0];
        }

        public static async Task<byte> ReadByteAsync(this Stream stream,
                                                     CancellationToken ct)
        {
            byte[] buffer = new byte[1];
            await stream.ReadAsync(buffer, 0, 1, ct);
            return buffer[0];
        }

        public static void WriteInt(this Stream stream, int value)
        {
            var zigZagEncoded = unchecked((uint)((value << 1) ^ (value >> 31)));
            while ((zigZagEncoded & ~0x7F) != 0)
            {
                stream.WriteByte((byte)((zigZagEncoded | 0x80) & 0xFF));
                zigZagEncoded >>= 7;
            }
            stream.WriteByte((byte)zigZagEncoded);
        }

        public static int WriteInt(this byte[] buffer,
                                   int offset,
                                   int value)
        {
            int retVal = 0;
            var zigZagEncoded = unchecked((uint)((value << 1) ^ (value >> 31)));
            while ((zigZagEncoded & ~0x7F) != 0)
            {
                buffer[offset] = (byte)((zigZagEncoded | 0x80) & 0xFF);
                offset++;
                retVal++;
                zigZagEncoded >>= 7;
            }
            buffer[offset] = (byte)zigZagEncoded;
            retVal++;
            return retVal;
        }

        public static int IntSize(int value)
        {
            int retVal = 0;
            var zigZagEncoded = unchecked((uint)((value << 1) ^ (value >> 31)));
            while ((zigZagEncoded & ~0x7F) != 0)
            {
                retVal++;
                zigZagEncoded >>= 7;
            }
            retVal++;
            return retVal;
        }

        public static long ReadBufferedLong(this byte[] buf,
                                  int offset)
        {
            var currentByte = (uint)buf[offset];
            byte read = 1;
            ulong result = currentByte & 0x7FUL;
            int shift = 7;
            while ((currentByte & 0x80) != 0)
            {
                currentByte = (uint)buf[offset + read];
                read++;
                result |= (currentByte & 0x7FUL) << shift;
                shift += 7;
                if (read > 10)
                {
                    throw new Exception("Invalid long value in the input stream.");
                }
            }
            var tmp = unchecked((long)result);
            return (-(tmp & 0x1L)) ^ ((tmp >> 1) & 0x7FFFFFFFFFFFFFFFL);
        }

        public static long ReadLong(this Stream stream)
        {
            var value = (uint)stream.ReadByte();
            byte read = 1;
            ulong result = value & 0x7FUL;
            int shift = 7;
            while ((value & 0x80) != 0)
            {
                value = (uint)stream.ReadByte();
                read++;
                result |= (value & 0x7FUL) << shift;
                shift += 7;
                if (read > 10)
                {
                    throw new Exception("Invalid integer long in the input stream.");
                }
            }
            var tmp = unchecked((long)result);
            return (-(tmp & 0x1L)) ^ ((tmp >> 1) & 0x7FFFFFFFFFFFFFFFL);
        }

        public static void WriteLong(this Stream stream, long value)
        {
            var zigZagEncoded = unchecked((ulong)((value << 1) ^ (value >> 63)));
            while ((zigZagEncoded & ~0x7FUL) != 0)
            {
                stream.WriteByte((byte)((zigZagEncoded | 0x80) & 0xFF));
                zigZagEncoded >>= 7;
            }
            stream.WriteByte((byte)zigZagEncoded);
        }

        public static int WriteLong(this byte[] buffer, int offset, long value)
        {
            int retVal = 0;
            var zigZagEncoded = unchecked((ulong)((value << 1) ^ (value >> 63)));
            while ((zigZagEncoded & ~0x7FUL) != 0)
            {
                buffer[offset] = (byte)((zigZagEncoded | 0x80) & 0xFF);
                offset++;
                retVal++;
                zigZagEncoded >>= 7;
            }
            buffer[offset] = (byte)zigZagEncoded;
            retVal++;
            return retVal;
        }
        public static int LongSize(long value)
        {
            int retVal = 0;
            var zigZagEncoded = unchecked((ulong)((value << 1) ^ (value >> 63)));
            while ((zigZagEncoded & ~0x7FUL) != 0)
            {
                retVal++;
                zigZagEncoded >>= 7;
            }
            retVal++;
            return retVal;
        }

        public static int ReadAllRequiredBytes(this Stream stream,
                                               byte[] buffer,
                                               int offset,
                                               int count)
        {
            int toRead = count;
            int currentOffset = offset;
            int currentRead;
            do
            {
                currentRead = stream.Read(buffer, currentOffset, toRead);
                currentOffset += currentRead;
                toRead -= currentRead;
            }
            while (toRead > 0 && currentRead != 0);
            return currentOffset - offset;
        }

        public static async Task<int> ReadAllRequiredBytesAsync(this Stream stream,
                                                                byte[] buffer,
                                                                int offset,
                                                                int count,
                                                                CancellationToken ct)
        {
            int toRead = count;
            int currentOffset = offset;
            int currentRead;
            do
            {
                var readTask = stream.ReadAsync(buffer, currentOffset, toRead, ct);
                currentRead = await readTask;
                currentOffset += currentRead;
                toRead -= currentRead;
            }
            while (toRead > 0 && currentRead != 0);
            return currentOffset - offset;
        }

        public static async Task<int> ReadAllRequiredBytesAsync(this Stream stream,
                                                                byte[] buffer,
                                                                int offset,
                                                                int count)
        {
            int toRead = count;
            int currentOffset = offset;
            int currentRead;
            do
            {
                var readTask = stream.ReadAsync(buffer, currentOffset, toRead);
                currentRead = await readTask;
                currentOffset += currentRead;
                toRead -= currentRead;
            }
            while (toRead > 0 && currentRead != 0);
            return currentOffset - offset;
        }
    }

    // This class eats the stream and just counts the number of bytes. 
    public class CountStream : Stream
    {
        long _count = 0;
        public CountStream()
        {
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _count += count;
        }

        public override bool CanRead
        {
            get { return false; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override long Length
        {
            get { return _count; }
        }

        public override bool CanTimeout
        {
            get { return false; }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Position
        {
            get { return _count; }
            set { throw new NotImplementedException(); }

        }

        public override void SetLength(long value)
        {
            _count = value;
        }

        public override void Flush()
        {
        }
    }

    // Pass through write stream.
    // Note that all writes get converted into one type of write, which 
    // hides bugs in NetworkStream
    public class PassThruWriteStream : Stream
    {
        long _count = 0;
        Stream _writeToStream;
        public PassThruWriteStream(Stream writeToStream)
        {
            _writeToStream = writeToStream;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _count += count;
            _writeToStream.Write(buffer, offset, count);
        }

        public override bool CanRead
        {
            get { return false; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override long Length
        {
            get { return _count; }
        }

        public override bool CanTimeout
        {
            get { return false; }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Position
        {
            get { return _count; }
            set { throw new NotImplementedException(); }

        }

        public override void SetLength(long value)
        {
            _count = value;
            _writeToStream.SetLength(value);
        }

        public override void Flush()
        {
            _writeToStream.Flush();
        }
    }

    // Pass through read stream.
    // Note that all reads get converted into one type of read, which 
    // may hide bugs in NetworkStream. Also, introduces an end of stream
    // response to ReadByte based on the number of bytes passed into the
    // constructor, which is necessary for correct behavior of XMLDictionarySerializer
    public class PassThruReadStream : Stream
    {
        long _count = 0;
        Stream _readFromStream;
        long _maxRead = 0;
        public PassThruReadStream(Stream readFromStream, long maxRead)
        {
            _readFromStream = readFromStream;
            _maxRead = maxRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Length
        {
            get { throw new NotImplementedException(); }
        }

        public override bool CanTimeout
        {
            get { return false; }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_count + count > _maxRead)
            {
                throw new Exception();
            }
            _readFromStream.ReadAllRequiredBytes(buffer, offset, count);
            _count += count;
            return count;
        }

        public override int ReadByte()
        {
            if (_count >= _maxRead)
            {
                return -1;
            }
            else
            {
                _count++;
                return _readFromStream.ReadByte();
            }
        }

        public override long Position
        {
            get { return _count; }
            set { throw new NotImplementedException(); }

        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }
    }
}

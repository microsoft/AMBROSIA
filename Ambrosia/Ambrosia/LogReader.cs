using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CRA.ClientLibrary;
#if WINDOWS_UWP
using Windows.Storage;
using Windows.Storage.Streams;
#endif

namespace Ambrosia
{
    internal static class LogReaderUtils
    {
        internal static void ReadBig(this LogReader reader,
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
    }
    /*
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
    }*/

#if NETFRAMEWORK || NETCORE || NETSTANDARD
    public class LogReader : ILogReader
    {
        Stream stream;

        public long Position
        {
            get { return stream.Position;  }
            set { stream.Position = value; }
        }

        public LogReader(string fileName)
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
#endif
#if WINDOWS_UWP
    // TODO: Figure out if there is a better way to implement Read().
    //
    // TODO: Figure out if there's a way to avoid having so much duplicated code
    // from the various versions of StreamCommunicator.
    //
    // TODO: Figure out proper way to handle async LogReader methods when the
    // underlying UWP class only provides a synchronous implementation.
    //
    // TODO: Figure out if waiting on a Task in the Position setter will cause
    // problems.
    public class LogReader : ILogReader
    {
        StorageFile _file;
        IRandomAccessStream _stream;
        IInputStream _inputStream;
        DataReader _dataReader;

        public long Position
        {
            get { return (long)_stream.Position; }

            set
            {
                _dataReader.Dispose();
                _inputStream.Dispose();
                _inputStream = _stream.GetInputStreamAt((ulong)value);
                _dataReader = new DataReader(_inputStream);
                _dataReader.LoadAsync((uint)_stream.Size).AsTask().Wait();
            }
        }

        public LogReader(string fileName)
        {
            InitializeAsync(fileName).Wait();
        }

        public async Task InitializeAsync(string fileName)
        {
            _file = await StorageFile.GetFileFromPathAsync(fileName);
            _stream = await _file.OpenAsync(FileAccessMode.Read);
            _inputStream = _stream.GetInputStreamAt(0);
            _dataReader = new DataReader(_inputStream);
            await _dataReader.LoadAsync((uint)_stream.Size);
        }

        public async Task<Tuple<int, int>> ReadIntAsync(byte[] buffer,
                                                        CancellationToken ct)
        {
            return ReadInt(buffer);
        }

        public async Task<Tuple<int, int>> ReadIntAsync(byte[] buffer)
        {
            return ReadInt(buffer);
        }

        // Copied from StreamCommunicator
        public Tuple<int, int> ReadInt(byte[] buffer)
        {
            buffer[0] = (byte)ReadByte();
            var currentByte = (uint)buffer[0];
            byte read = 1;
            uint result = currentByte & 0x7FU;
            int shift = 7;
            while ((currentByte & 0x80) != 0)
            {
                buffer[read] = (byte)ReadByte();
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

        // Copied from StreamCommunicator
        public int ReadInt()
        {
            var currentByte = (uint)ReadByte();
            byte read = 1;
            uint result = currentByte & 0x7FU;
            int shift = 7;
            while ((currentByte & 0x80) != 0)
            {
                currentByte = (uint)ReadByte();
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

        public async Task<int> ReadAllRequiredBytesAsync(byte[] buffer,
                                                        int offset,
                                                        int count,
                                                        CancellationToken ct)
        {
            return ReadAllRequiredBytes(buffer, offset, count);
        }

        public async Task<int> ReadAllRequiredBytesAsync(byte[] buffer,
                                                        int offset,
                                                        int count)
        {
            return ReadAllRequiredBytes(buffer, offset, count);
        }

        // Copied from StreamCommunicator
        public int ReadAllRequiredBytes(byte[] buffer,
                                       int offset,
                                       int count)
        {
            int toRead = count;
            int currentOffset = offset;
            int currentRead;
            do
            {
                currentRead = Read(buffer, currentOffset, toRead);
                currentOffset += currentRead;
                toRead -= currentRead;
            }
            while (toRead > 0 && currentRead != 0);
            return currentOffset - offset;
        }

        // Copied from StreamCommunicator
        public long ReadLongFixed()
        {
            var value = new byte[8];
            ReadAllRequiredBytes(value, 0, value.Length);
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

        // Copied from StreamCommunicator
        public int ReadIntFixed()
        {
            var value = new byte[4];
            ReadAllRequiredBytes(value, 0, value.Length);
            int intValue = value[0]
                | (int)value[1] << 0x8
                | (int)value[2] << 0x10
                | (int)value[3] << 0x18;
            return intValue;
        }

        // Copied from CRA version of StreamCommunicator
        public byte[] ReadByteArray()
        {
            int arraySize = ReadInt32();
            var array = new byte[arraySize];
            if (arraySize > 0)
            {
                ReadAllRequiredBytes(array, 0, array.Length);
            }
            return array;
        }

        // Copied from CRA version of StreamCommunicator
        private int ReadInt32()
        {
            var currentByte = (uint)ReadByte();
            byte read = 1;
            uint result = currentByte & 0x7FU;
            int shift = 7;
            while ((currentByte & 0x80) != 0)
            {
                currentByte = (uint)ReadByte();
                read++;
                result |= (currentByte & 0x7FU) << shift;
                shift += 7;
                if (read > 5)
                {
                    throw new InvalidOperationException("Invalid integer value in the input stream.");
                }
            }
            return (int)((-(result & 1)) ^ ((result >> 1) & 0x7FFFFFFFU));
        }

        public int ReadByte()
        {
            return _dataReader.ReadByte();
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = 0;
            for (int i = 0; i < count; i++)
            {
                buffer[offset + i] = _dataReader.ReadByte();
                bytesRead++;
            }
            return bytesRead;
        }

        public void Dispose()
        {
            _dataReader.Dispose();
            _inputStream.Dispose();
            _stream.Dispose();
        }
    }
#endif
}

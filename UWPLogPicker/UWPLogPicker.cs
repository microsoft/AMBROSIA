using Ambrosia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace Ambrosia
{
    // I wrote this version of LogWriter using the documentation here:
    // https://docs.microsoft.com/en-us/windows/uwp/files/quickstart-reading-and-writing-files
    //
    // TODO: figure out proper way to handle synchronous LogWriter methods when underlying UWP
    // class only provides an async implementation
    //
    // TODO: figure out proper way to handle async LogWriter methods when underlying UWP class only
    // provides a synchronous implementation
    internal class LogWriterUWP : IDisposable, ILogWriter
    {
        StorageFile _file;
        IRandomAccessStream _stream;
        IOutputStream _outputStream;
        DataWriter _dataWriter;

        private ulong _fileSize = 0;

        public LogWriterUWP(string fileName,
                                   uint chunkSize,
                                   uint maxChunksPerWrite,
                                   bool appendOpen = false)
        {
            InitializeAsync(fileName, appendOpen).Wait();
        }

        public async Task InitializeAsync(string fileName, bool appendOpen = false)
        {
            DirectoryInfo pathInfo = new DirectoryInfo(fileName);
            string parentPath = pathInfo.Parent.FullName;
            StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(parentPath);
            _file = await folder.CreateFileAsync(pathInfo.Name, CreationCollisionOption.OpenIfExists);

            _stream = await _file.OpenAsync(FileAccessMode.ReadWrite);
            ulong position = 0;
            if (appendOpen)
            {
                BasicProperties properties = await _file.GetBasicPropertiesAsync();
                position = properties.Size;
            }
            _outputStream = _stream.GetOutputStreamAt(position);
            _dataWriter = new DataWriter(_outputStream);
        }

        public ulong FileSize { get { return _fileSize; } }

        public void Dispose()
        {
            _dataWriter.Dispose();
            _outputStream.Dispose();
            _stream.Dispose();
        }
        public void Flush()
        {
            _dataWriter.StoreAsync().AsTask().Wait();
            _outputStream.FlushAsync().AsTask().Wait();
        }

        public async Task FlushAsync()
        {
            await _dataWriter.StoreAsync();
            await _outputStream.FlushAsync();
        }

        public void WriteByte(byte value)
        {
            _fileSize++;
            _dataWriter.WriteByte(value);
        }

        // These three methods are all copied from the .NET Framework version of LogWriter
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

        public void Write(byte[] buffer,
                          int offset,
                          int length)
        {
            _fileSize += (ulong)length;

            // Hopefully there is a more performant way to do this
            byte[] subBuffer = new byte[length];
            Array.Copy(buffer, offset, subBuffer, 0, length);
            _dataWriter.WriteBytes(subBuffer);
        }

        // Copied from Write() implementation above
        public async Task WriteAsync(byte[] buffer,
                                     int offset,
                                     int length)
        {
            _fileSize += (ulong)length;

            // Hopefully there is a more performant way to do this
            byte[] subBuffer = new byte[length];
            Array.Copy(buffer, offset, subBuffer, 0, length);
            _dataWriter.WriteBytes(subBuffer);
        }
    }

    internal class LogWriterStaticsUWP : ILogWriterStatic
    {
        public void CreateDirectoryIfNotExists(string path)
        {
            DirectoryInfo pathInfo = new DirectoryInfo(path);
            string parentPath = pathInfo.Parent.FullName;
            StorageFolder folder = StorageFolder.GetFolderFromPathAsync(parentPath).AsTask().Result;
            folder.CreateFolderAsync(pathInfo.Name, CreationCollisionOption.OpenIfExists).AsTask().Wait();
        }

        public bool DirectoryExists(string path)
        {
            DirectoryInfo pathInfo = new DirectoryInfo(path);
            string parentPath = pathInfo.Parent.FullName;
            StorageFolder parentFolder = StorageFolder.GetFolderFromPathAsync(parentPath).AsTask().Result;
            bool result;
            try
            {
                StorageFolder queriedFolder = parentFolder.GetFolderAsync(pathInfo.Name).AsTask().Result;
                result = true;
            }
            catch (System.AggregateException)
            {
                result = false;
            }
            return result;
        }

        public bool FileExists(string path)
        {
            FileInfo pathInfo = new FileInfo(path);
            string parentPath = pathInfo.Directory.FullName;
            StorageFolder parentFolder = StorageFolder.GetFolderFromPathAsync(parentPath).AsTask().Result;
            bool result;
            try
            {
                StorageFile queriedFile = parentFolder.GetFileAsync(pathInfo.Name).AsTask().Result;
                result = true;
            }
            catch (System.AggregateException)
            {
                result = false;
            }
            return result;
        }

        public void DeleteFile(string path)
        {
            StorageFile file = StorageFile.GetFileFromPathAsync(path).AsTask().Result;
            file.DeleteAsync().AsTask().Wait();
        }

        public ILogWriter Generate(string fileName,
                                   uint chunkSize,
                                   uint maxChunksPerWrite,
                                   bool appendOpen = false)
        {
            return new LogWriterUWP(fileName, chunkSize, maxChunksPerWrite, appendOpen);
        }
    }

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
    public class UWPLogReader : ILogReader
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

        public UWPLogReader(string fileName)
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

    internal class UWPLogReaderStatics : ILogReaderStatic
    {
        public ILogReader Generate(string fileName)
        {
            return new UWPLogReader(fileName);
        }
    }

    public static class UWPLogsInterface
    {
        public static void SetToUWPLogs()
        {
            LogReaderStaticPicker.curStatic = new UWPLogReaderStatics();
            LogWriterStaticPicker.curStatic = new LogWriterStaticsUWP();
        }
    }
}

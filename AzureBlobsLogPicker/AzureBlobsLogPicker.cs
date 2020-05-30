using System;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure;
using Azure.Storage.Blobs.Models;
using System.Threading.Tasks;
using System.Threading;
using CRA.ClientLibrary;
using System.IO;
using System.Collections.Generic;

namespace Ambrosia
{
    internal class AzureBlobsLogWriter : IDisposable, ILogWriter
    {
        BlobContainerClient _blobsContainerClient;
        AppendBlobClient _logClient;
        MemoryStream _bytesToSend;

        public AzureBlobsLogWriter(BlobContainerClient blobsContainerClient,
                                   string fileName,
                                   bool appendOpen = false)
        {
            fileName = AzureBlobsLogsInterface.PathFixer(fileName);
            _blobsContainerClient = blobsContainerClient;
            _logClient = _blobsContainerClient.GetAppendBlobClient(fileName);
            if (appendOpen)
            {
                _logClient.CreateIfNotExists();
            }
            else
            {
                _logClient.Create();
            }
            _bytesToSend = new MemoryStream();
        }

        public ulong FileSize { 
            get 
            {
                BlobProperties blobProps = _logClient.GetProperties();
                return (ulong) blobProps.ContentLength;
            } 
        }

        public void Dispose()
        {
        }

        public void Flush()
        {
            _bytesToSend.Position = 0;
            _logClient.AppendBlock(_bytesToSend);
            _bytesToSend.Position = 0;
            _bytesToSend.SetLength(0);
        }

        public async Task FlushAsync()
        {
            _bytesToSend.Position = 0;
            await _logClient.AppendBlockAsync(_bytesToSend);
            _bytesToSend.Position = 0;
            _bytesToSend.SetLength(0);
        }

        public void WriteInt(int value)
        {
            _bytesToSend.WriteInt(value);
        }
        public void WriteIntFixed(int value)
        {
            _bytesToSend.WriteIntFixed(value);
        }

        public void WriteLongFixed(long value)
        {
            _bytesToSend.WriteLongFixed(value);
        }
        public void Write(byte[] buffer,
                          int offset,
                          int length)
        {
            _bytesToSend.Write(buffer, offset, length);
        }

        public async Task WriteAsync(byte[] buffer,
                                     int offset,
                                     int length)
        {
            await _bytesToSend.WriteAsync(buffer, offset, length);
        }
    }

    internal class AzureBlobsLogWriterStatics : ILogWriterStatic
    {
        BlobContainerClient _blobsContainerClient;

        public AzureBlobsLogWriterStatics(BlobContainerClient blobsContainerClient)
        {
            _blobsContainerClient = blobsContainerClient;
        }

        public void CreateDirectoryIfNotExists(string path)
        {
            path = AzureBlobsLogsInterface.PathFixer(path);
            var logClient = _blobsContainerClient.GetAppendBlobClient(path);
            if (!logClient.Exists())
            {
                logClient.Create();
            }
        }

        public bool DirectoryExists(string path)
        {
            path = AzureBlobsLogsInterface.PathFixer(path);
            return FileExists(path);
        }

        public bool FileExists(string path)
        {
            path = AzureBlobsLogsInterface.PathFixer(path);
            var logClient = _blobsContainerClient.GetAppendBlobClient(path);
            return logClient.Exists();
        }

        public void DeleteFile(string path)
        {
            path = AzureBlobsLogsInterface.PathFixer(path);
            var logClient = _blobsContainerClient.GetAppendBlobClient(path);
            logClient.Delete(DeleteSnapshotsOption.IncludeSnapshots);
        }

        public ILogWriter Generate(string fileName,
                                   uint chunkSize,
                                   uint maxChunksPerWrite,
                                   bool appendOpen = false)
        {
            fileName = AzureBlobsLogsInterface.PathFixer(fileName);
            return new AzureBlobsLogWriter(_blobsContainerClient, fileName, appendOpen);
        }
    }

    public class AzureBlobsLogReader : ILogReader
    {
        BlobDownloadInfo _download;
        BlobClient _logClient;

        public long Position
        {
            get { return _download.Content.Position; }
            set 
            {
                _download.Content.Dispose();
                var downloadRange = new HttpRange(value);
                _download = _logClient.Download(downloadRange); 
            }
        }

        public AzureBlobsLogReader(BlobContainerClient blobsContainerClient, string fileName)
        {
            fileName = AzureBlobsLogsInterface.PathFixer(fileName);
            _logClient = blobsContainerClient.GetBlobClient(fileName);
            var downloadRange = new HttpRange(0);
            _download = _logClient.Download(downloadRange);
        }

        public async Task<Tuple<int, int>> ReadIntAsync(byte[] buffer)
        {
            return await _download.Content.ReadIntAsync(buffer);
        }

        public async Task<Tuple<int, int>> ReadIntAsync(byte[] buffer, CancellationToken ct)
        {
            return await _download.Content.ReadIntAsync(buffer, ct);
        }

        public Tuple<int, int> ReadInt(byte[] buffer)
        {
            return _download.Content.ReadInt(buffer);
        }

        public int ReadInt()
        {
            return _download.Content.ReadInt();
        }

        public async Task<int> ReadAllRequiredBytesAsync(byte[] buffer,
                                                        int offset,
                                                        int count,
                                                        CancellationToken ct)
        {
            return await _download.Content.ReadAllRequiredBytesAsync(buffer, offset, count, ct);
        }

        public async Task<int> ReadAllRequiredBytesAsync(byte[] buffer,
                                                int offset,
                                                int count)
        {
            return await _download.Content.ReadAllRequiredBytesAsync(buffer, offset, count);
        }

        public int ReadAllRequiredBytes(byte[] buffer,
                                       int offset,
                                       int count)
        {
            return _download.Content.ReadAllRequiredBytes(buffer, offset, count);
        }

        public long ReadLongFixed()
        {
            return _download.Content.ReadLongFixed();
        }

        public int ReadIntFixed()
        {
            return _download.Content.ReadIntFixed();
        }

        public byte[] ReadByteArray()
        {
            return _download.Content.ReadByteArray();
        }

        public int ReadByte()
        {
            return _download.Content.ReadByte();
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            return _download.Content.Read(buffer, offset, count);
        }

        public void Dispose()
        {
            _download.Dispose();
        }
    }

    internal class AzureBlobsLogReaderStatics : ILogReaderStatic
    {
        BlobContainerClient _blobsContainerClient;

        public AzureBlobsLogReaderStatics(BlobContainerClient blobsContainerClient)
        {
            _blobsContainerClient = blobsContainerClient;
        }

        public ILogReader Generate(string fileName)
        {
            return new AzureBlobsLogReader(_blobsContainerClient, fileName);
        }
    }


    public static class AzureBlobsLogsInterface
    {
        static BlobServiceClient _blobsClient;
        static BlobContainerClient _blobsContainerClient;

        internal static string PathFixer(string fileName)
        {
            var substrings = fileName.Split('/');
            string fixedFileName = "";
            bool emptyFileName = true;
            foreach (var substring in substrings)
            {
                var subdirCands = substring.Split('\\');
                foreach (var subdir in subdirCands)
                {
                    if (subdir.CompareTo("") != 0)
                    {
                        if (emptyFileName)
                        {
                            fixedFileName = subdir;
                            emptyFileName = false;
                        }
                        else
                        {
                            fixedFileName += "/" + subdir;
                        }
                    }
                }
            }
            return fixedFileName;
        }

        public static void SetToAzureBlobsLogs()
        {
            var storageConnectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONN_STRING");
            _blobsClient = new BlobServiceClient(storageConnectionString);
            _blobsContainerClient = _blobsClient.GetBlobContainerClient("ambrosialogs");
            _blobsContainerClient.CreateIfNotExists();
            LogReaderStaticPicker.curStatic = new AzureBlobsLogReaderStatics(_blobsContainerClient);
            LogWriterStaticPicker.curStatic = new AzureBlobsLogWriterStatics(_blobsContainerClient);
        }
    }
}

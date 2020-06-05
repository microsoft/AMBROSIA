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
using System.Diagnostics;
using System.ComponentModel;

namespace Ambrosia
{
    internal class AzureBlobsLogWriter : IDisposable, ILogWriter
    {
        BlobContainerClient _blobsContainerClient;
        AppendBlobClient _logClient;
        MemoryStream _bytesToSend;
        BlobLeaseClient _leaseClient;
        BlobLease _curLease;
        AppendBlobRequestConditions _leaseCondition;
        Thread _leaseRenewThread;

        public AzureBlobsLogWriter(BlobContainerClient blobsContainerClient,
                                   string fileName,
                                   bool appendOpen = false)
        {
            fileName = AzureBlobsLogsInterface.PathFixer(fileName);
            _blobsContainerClient = blobsContainerClient;
            _logClient = _blobsContainerClient.GetAppendBlobClient(fileName);
            ETag currentETag;

            if (appendOpen)
            {
                var response = _logClient.CreateIfNotExists();
                if (response != null)
                {
                    currentETag = response.Value.ETag;
                }
                else
                {
                    currentETag = _logClient.GetProperties().Value.ETag;
                }
            }
            else
            {
                currentETag = _logClient.Create().Value.ETag;
            }

            _leaseClient = _logClient.GetBlobLeaseClient();
            var etagCondition = new RequestConditions();
            etagCondition.IfMatch = currentETag;
            _curLease = _leaseClient.Acquire(TimeSpan.FromSeconds(15), etagCondition).Value;
            _leaseRenewThread = new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    _leaseClient.Renew();
                }
            })
            { IsBackground = true };
            _leaseRenewThread.Start();
            _leaseCondition = new AppendBlobRequestConditions();
            _leaseCondition.LeaseId = _curLease.LeaseId;
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
            _leaseRenewThread.Abort();
            _leaseClient.Release();
        }

        public void Flush()
        {
            var numSendBytes = _bytesToSend.Length;
            var OrigSendBytes = numSendBytes;
            var buffer = _bytesToSend.GetBuffer();
            int bufferPosition = 0;
            while (numSendBytes > 0)
            {
                int numAppendBytes = (int) Math.Min(numSendBytes, 1024*1024);
                var sendStream = new MemoryStream(buffer, bufferPosition, numAppendBytes);
                _logClient.AppendBlock(sendStream, null, _leaseCondition);
                bufferPosition += numAppendBytes;
                numSendBytes -= numAppendBytes;
            }
            Debug.Assert(OrigSendBytes == _bytesToSend.Length);
            _bytesToSend.Position = 0;
            _bytesToSend.SetLength(0);
        }

        public async Task FlushAsync()
        {
            var numSendBytes = _bytesToSend.Length;
            var OrigSendBytes = numSendBytes;
            var buffer = _bytesToSend.GetBuffer();
            int bufferPosition = 0;
            while (numSendBytes > 0)
            {
                int numAppendBytes = (int)Math.Min(numSendBytes, 1024 * 1024);
                var sendStream = new MemoryStream(buffer, bufferPosition, numAppendBytes);
                await _logClient.AppendBlockAsync(sendStream, null, _leaseCondition);
                bufferPosition += numAppendBytes;
                numSendBytes -= numAppendBytes;
            }
            Debug.Assert(OrigSendBytes == _bytesToSend.Length);
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
            return new AzureBlobsLogWriter(_blobsContainerClient, fileName, appendOpen);
        }
    }

    public class AzureBlobsLogReader : ILogReader
    {
        BlobDownloadInfo _download;
        BlobClient _logClient;
        long _streamOffset;

        public long Position
        {
            get { return _download.Content.Position + _streamOffset; }
            set 
            {
                _download.Content.Dispose();
                if (value > 0)
                {
                    _streamOffset = value - 1;
                    var downloadRange = new HttpRange(value - 1);
                    _download = _logClient.Download(downloadRange);
                    _download.Content.ReadByte();
                }
                else
                {
                    _streamOffset = 0;
                    _download = _logClient.Download();
                }
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

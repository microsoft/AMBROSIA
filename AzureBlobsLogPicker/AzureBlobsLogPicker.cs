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
        static Dictionary<string, ETag> _previousOpenAttempts = new Dictionary<string, ETag>();
        BlobContainerClient _blobsContainerClient;
        AppendBlobClient _logClient;
        MemoryStream _bytesToSend;
        BlobLeaseClient _leaseClient;
        BlobLease _curLease;
        AppendBlobRequestConditions _leaseCondition;
        Thread _leaseRenewThread;
        IDictionary<string, string> _blobMetadata;
        volatile bool _stopRelockThread;
        volatile bool _relockThreadStopped;

        public AzureBlobsLogWriter(BlobContainerClient blobsContainerClient,
                                   string fileName,
                                   bool appendOpen = false)
        {
            fileName = AzureBlobsLogsInterface.PathFixer(fileName);
            Console.WriteLine("Opening or creating " + fileName);
            _blobsContainerClient = blobsContainerClient;
            _logClient = _blobsContainerClient.GetAppendBlobClient(fileName);
            ETag currentETag;
            if (_previousOpenAttempts.ContainsKey(fileName) && appendOpen)
            {
                Console.WriteLine("Was open before");
                // We've opened this blob before and want to be non-destructive. We don't need to CreateIfNotExists, which could be VERY slow.
                currentETag = _logClient.GetProperties().Value.ETag;
            }
            else
            {
                Console.WriteLine("First time opening");
                try
                {
                    // Create the file non-destructively if needed, guaranteeing write continuity on creation by grabbing the etag of the create, if needed
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
                }
                catch { currentETag = _logClient.GetProperties().Value.ETag; }
            }
            // Try to grab the blob lease
            _leaseClient = _logClient.GetBlobLeaseClient();
            // The blob hasn't be touched since the last time. This is a candidate for breaking the lease.
            if (_previousOpenAttempts.ContainsKey(fileName) && (_previousOpenAttempts[fileName].ToString().Equals(currentETag.ToString())))
            {
                Console.WriteLine("Passed Test" + currentETag.ToString());
                _previousOpenAttempts[fileName] = currentETag;
                // The blob hasn't been updated. Try to break the lease and reacquire
                var requestConditions = new BlobRequestConditions();
                requestConditions = new BlobRequestConditions();
                requestConditions.IfMatch = currentETag;
                // If the condition fails in the break, it's because someone else managed to touch the file, so give up
                Console.WriteLine("Breaking Lease");
                ETag newETag;
                try
                {
                    newETag = _leaseClient.Break(null, requestConditions).Value.ETag;
                }
                catch (Exception e) { newETag = currentETag; }
                Console.WriteLine("Broke old lease");
                var etagCondition = new RequestConditions();
                etagCondition.IfMatch = newETag;
                // If the condition fails, someone snuck in and grabbed the lock before we could. Give up.
                _curLease = _leaseClient.Acquire(TimeSpan.FromSeconds(-1), etagCondition).Value;
                Console.WriteLine("Acquired lease");
            }
            else
            {
                Console.WriteLine("Failed Test" + currentETag.ToString());
                // Not a candidate for breaking the lease. Just try to acquire.
                _previousOpenAttempts[fileName] = currentETag;
                _curLease = _leaseClient.Acquire(TimeSpan.FromSeconds(-1)).Value;
                Console.WriteLine("Acquired lease");
            }

            _leaseCondition = new AppendBlobRequestConditions();
            _leaseCondition.LeaseId = _curLease.LeaseId;
            // We got the lease! Set up thread to periodically touch the blob to prevent others from breaking the lease.
            _blobMetadata = _logClient.GetProperties().Value.Metadata;
            _stopRelockThread = false;
            _relockThreadStopped = false;
            _leaseRenewThread = new Thread(() =>
            {
                while (!_stopRelockThread)               
                {
                    Thread.Sleep(100);
                    var response = _logClient.SetMetadata(_blobMetadata, _leaseCondition);
                }
                _relockThreadStopped = true;
            }) { IsBackground = true };
            _leaseRenewThread.Start();
            _bytesToSend = new MemoryStream();
            Debug.Assert(_logClient.Exists());
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
            _stopRelockThread = true;
            while (!_relockThreadStopped) { Thread.Sleep(100); }
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
                int numAppendBytes = (int)Math.Min(numSendBytes, 256 * 1024);
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
            // This operation hangs mysteriously with Azure blobs sometimes, so I just won't do it. This will leave the kill file around, but it causes no harm
/*            path = AzureBlobsLogsInterface.PathFixer(path);
            Console.WriteLine("Deleting " + path);
            var logClient = _blobsContainerClient.GetAppendBlobClient(path);
            logClient.DeleteIfExists();*/
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

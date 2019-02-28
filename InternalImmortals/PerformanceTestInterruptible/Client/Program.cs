using Ambrosia;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Server;
using System.Runtime.Serialization;
using JobAPI;
using System.Threading;
using System.IO;
using System.Collections.Concurrent;
using Microsoft.VisualStudio.Threading;

namespace Job
{
    [DataContract]
    sealed class Job : Immortal<IJobProxy>, IJob
    {
        const long oneGig = ((long)1 * 1024 * 1024 * 1024);
        [DataMember]
        string _perfServer = string.Empty;
        [DataMember]
        IServerProxy _server;
        [DataMember]
        internal long _bytesReceived = 0;
        [DataMember]
        internal long _callNum = 0;
        [DataMember]
        internal long _lastCallNum = 0;
        [DataMember]
        internal int _maxMessageSize;
        [DataMember]
        internal int _numRoundsLeft;
        [DataMember]
        internal bool _descendingSize;
        [DataMember]
        internal Random _randGenerator;

        public Job()
        {
        }

        public Job(string LocalPerfServer,
                   int maxMessageSize,
                   int numRounds,
                   bool descendingSize)
        {
            _perfServer = LocalPerfServer;
            _maxMessageSize = maxMessageSize;
            _numRoundsLeft = numRounds;
            _descendingSize = descendingSize;
            _randGenerator = new Random(0);
        }

        protected override async Task<bool> OnFirstStart()
        {
#if DEBUG
            Console.WriteLine("*X* Starting up in client container. Running performance test against:" + _perfServer);
#endif

            _server = GetProxy<IServerProxy>(_perfServer);
            Console.WriteLine("{0}\t{1}", "Bytes per RPC", "Throughput (GB/sec)");
            BoxedDateTime start;
            start.val = DateTime.Now;
            thisProxy.JobContinueFork(_maxMessageSize, 0, start);
            return true;
        }

        public async Task JobContinueAsync(int numRPCBytes,
                                long rep,
                                BoxedDateTime startTimeOfRoundBoxed)
        {
            var startTimeOfRound = startTimeOfRoundBoxed.val;
            var bytesSentInCurrentCall = 0;
            for (; _numRoundsLeft > 0; _numRoundsLeft--)
            {
                var RPCbuf = new byte[numRPCBytes];
                for (int i = 0; i < RPCbuf.Length; i++)
                {
                    RPCbuf[i] = (byte)i;
                }
                long iterations = ((long)1 * 1024 * 1024 * 1024) / numRPCBytes;
                if (rep == 0)
                {
                    startTimeOfRound = DateTime.Now;
                }
                for (; rep < iterations; rep++)
                {
                    if (bytesSentInCurrentCall > 10 * 1024 * 1024)
                    {
                        var newStartTimeOfRoundBoxed = new BoxedDateTime();
                        newStartTimeOfRoundBoxed.val = startTimeOfRound;
                        thisProxy.JobContinueFork(numRPCBytes, rep, newStartTimeOfRoundBoxed);
                        return;
                    }
                    bytesSentInCurrentCall += numRPCBytes;
                    _callNum++;
                    StreamCommunicator.WriteLong(RPCbuf, 0, _callNum);
                    _server.MFork(RPCbuf);
                }
                var endTimeOfRound = DateTime.Now;
                var roundDuration = endTimeOfRound - startTimeOfRound;
                long numberOfBytesSent = iterations * numRPCBytes;
                double numberOfGigabytesSent = ((double)numberOfBytesSent) / ((double)oneGig);
                rep = 0;
                Console.WriteLine("*X* {0}\t{1}",
                    numRPCBytes,
                    numberOfGigabytesSent / roundDuration.TotalSeconds
                    );
                if (_descendingSize)
                {
                    if (numRPCBytes > 16)
                    {
                        numRPCBytes >>= 1;
                    }
                }
                else
                {
                    int Choices = (int)Math.Log(_maxMessageSize, 2);
                    int Choice = _randGenerator.Next(4, Choices);
                    numRPCBytes = 1 << Choice;
                }
            }
            _server.PrintBytesReceivedFork();
        }

        public async Task PrintBytesReceivedAsync()
        {
            Console.WriteLine("Bytes received: {0}", _bytesReceived);
            Console.WriteLine("DONE");
            Console.Out.Flush();
            Console.Out.Flush();
            ClientBootstrapper.finishedTokenQ.Enqueue(0);
        }

        public async Task MAsync(byte[] arg)
        {
            _bytesReceived += arg.Length;
            if ((_bytesReceived - arg.Length) / oneGig != _bytesReceived / oneGig)
            {
                Console.WriteLine($"Service Received {_bytesReceived / (1024 * 1024)} MB so far");
            }
            var curCallNum = StreamCommunicator.ReadBufferedLong(arg, 0);
            if (_lastCallNum + 1 != curCallNum)
            {
//                Console.WriteLine("*X* Out of order message. Expected {0}, got {1}", _lastCallNum + 1, curCallNum);
            }
            _lastCallNum = curCallNum;
        }
    }


    class ClientBootstrapper
    {
        private static int _receivePort = -1;
        private static int _sendPort = -1;
        private static string _perfJob;
        private static string _perfServer;
        private static bool _autoContinue;
        private static int _maxMessageSize = 64 * 1024;
        private static int _numRounds = 13;
        private static bool _descendingSize = true;
        public static AsyncQueue<int> finishedTokenQ;

        static void Main(string[] args)
        {
            ParseAndValidateOptions(args);

            finishedTokenQ = new AsyncQueue<int>();
            
            // for debugging don't want to auto continue but for test automation want this to auto continue
            if (!_autoContinue)
            {
                Console.WriteLine("Pausing execution of " + _perfJob + ". Press enter to deploy and continue.");
                Console.ReadLine();
            }

#if DEBUG
            Console.WriteLine("*X* Connecting to: " + _perfServer + "....");
#endif

            var myClient = new Job(_perfServer, _maxMessageSize, _numRounds, _descendingSize);

            // Use "Empty" as the type parameter because this container doesn't run a service
            // that responds to any RPC calls.
            using (var c = AmbrosiaFactory.Deploy<IJob>(_perfJob, myClient, _receivePort, _sendPort))
            {

                finishedTokenQ.DequeueAsync().Wait();
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
            var options = new OptionSet {
                { "j|jobName=", "The service name of the job [REQUIRED].", j => _perfJob = j },
                { "s|serverName=", "The service name of the server [REQUIRED].", s => _perfServer = s },
                { "rp|receivePort=", "The service receive from port [REQUIRED].", rp => _receivePort = int.Parse(rp) },
                { "sp|sendPort=", "The service send to port. [REQUIRED]", sp => _sendPort = int.Parse(sp) },
                { "mms|maxMessageSize=", "The maximum message size.", mms => _maxMessageSize = int.Parse(mms) },
                { "n|numOfRounds=", "The number of rounds.", n => _numRounds = int.Parse(n) },
                { "nds|noDescendingSize", "Disable message descending size.", nds => _descendingSize = false },
                { "c|autoContinue", "Is continued automatically at start", c => _autoContinue = true },
                { "h|help", "show this message and exit", h => showHelp = h != null },
            };

            try
            {
                options.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine("Invalid arguments: " + e.Message);
                ShowHelp(options);
                Environment.Exit(1);
            }

            shouldShowHelp = showHelp;

            return options;
        }

        private static void ValidateOptions(OptionSet options, bool shouldShowHelp)
        {
            var errorMessage = string.Empty;
            if (_perfJob == null) errorMessage += "Job name is required.\n";
            if (_perfServer == null) errorMessage += "Server name is required.\n";
            if (_sendPort == -1) errorMessage += "Send port is required.\n";
            if (_receivePort == -1) errorMessage += "Receive port is required.\n";

            if (errorMessage != string.Empty)
            {
                Console.WriteLine(errorMessage);
                ShowHelp(options);
                Environment.Exit(1);
            }

            if (shouldShowHelp) ShowHelp(options);
        }

        private static void ShowHelp(OptionSet options)
        {
            var name = typeof(ClientBootstrapper).Assembly.GetName().Name;
#if NETCORE
            Console.WriteLine($"Usage: dotnet {name}.dll [OPTIONS]\nOptions:");
#else
            Console.WriteLine($"Usage: {name}.exe [OPTIONS]\nOptions:");
#endif
            options.WriteOptionDescriptions(Console.Out);
            Environment.Exit(0);
        }
    }

}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Server;
using System.Runtime.Serialization;
using System.Threading.Tasks.Dataflow;
using Microsoft.VisualStudio.Threading;
using Ambrosia;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;

namespace Job
{
    [DataContract]
    sealed class Job : Immortal<IJobProxy>, IJob
    {
        private const long OneGig = ((long)1 * 1024 * 1024 * 1024);

        [DataMember]
        private string _perfServer;

        [DataMember]
        private IServerProxy _server;

        [DataMember]
        internal long _bytesReceived = 0;

        [DataMember]
        internal long _callNum = 0;

        [DataMember]
        private int _numRoundsLeft;

        public Job()
        {
        }

        public Job(string localPerfServer,
                   int numRounds)
        {
            this._perfServer = localPerfServer;
            _numRoundsLeft = numRounds;
        }

        protected override async Task<bool> OnFirstStart()
        {
            Console.WriteLine("*X* Starting up in client container. Running performance test against:" + this._perfServer);
            this._server = GetProxy<IServerProxy>(this._perfServer);
            await this.RunAsync();

            return true;
        }

        public async Task<bool> RunAsync()
        {
            Image image = Image.FromFile("testImage.png");
            byte[] imageBytes;
            using (var ms = new MemoryStream()) 
            {
                image.Save(ms, ImageFormat.Png);
                imageBytes = ms.ToArray();
            }

            var sw = new Stopwatch();
            double totaltime = 0;
            int numberOfRPCs = 256;
            for (; _numRoundsLeft > 0; _numRoundsLeft--)
            {
                for (var rep = 0; rep < numberOfRPCs; rep++)
                {
                    long sendTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    var result = await this._server.ResizeImageAsync(imageBytes, sendTime);
                    long receiveTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    Console.WriteLine("*X* receive delay: {0}ms, Latency: {1}ms", receiveTime - result.Item2, receiveTime - sendTime);
                    //Thread.Sleep(100);
                }
                Console.Out.Flush();
                this._server.PrintComputeTimeFork();
                //Console.WriteLine("*X* Latency: {0}ms", totalMilliseconds / numberOfRPCs);
            }
            ClientBootstrapper.finishedTokenQ.Enqueue(0);
            return true;
        }
    }

    class ClientBootstrapper
    {
        private static int _receivePort = -1;
        private static int _sendPort = -1;
        private static string _perfJob;
        private static string _perfServer;
        private static int _numRounds = 13;
        private static bool _autoContinue;

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

            var myClient = new Job(_perfServer, _numRounds);

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
                { "n|numOfRounds=", "The number of rounds.", n => _numRounds = int.Parse(n) },
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

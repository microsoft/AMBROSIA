using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Ambrosia;
using Mono.Options;

namespace Server
{
    [DataContract]
    public class Server : Immortal<IServerProxy>, IServer
    {
        [DataMember]
        internal long bytesReceived = 0;

        const long oneMB = ((long)1 * 1024 * 1024);
        const long oneGig = ((long)1 * 1024 * oneMB);
        [DataMember]
        internal long _lastCallNum = 0;

        public Server()
        {
        }

        protected override async Task<bool> OnFirstStart()
        {
            Console.WriteLine("*X* Server in Entry Point");
            return true;
        }

        public async Task PrintBytesReceivedAsync()
        {
            Console.WriteLine("Bytes received: {0}", bytesReceived);
            Console.WriteLine("DONE");
            Console.Out.Flush();
            Console.Out.Flush();
        }

        public async Task<byte[]> MAsync(byte[] arg)
        {
            bytesReceived += arg.Length;
            var curCallNum = StreamCommunicator.ReadBufferedLong(arg, 0);
            if (_lastCallNum + 1 != curCallNum)
            {
                Console.WriteLine("*X* Out of order message. Expected {0}, got {1}", _lastCallNum + 1, curCallNum);
            }
            _lastCallNum = curCallNum;
            if ((bytesReceived - arg.Length) / oneGig != bytesReceived / oneGig)
            {
                Console.WriteLine($"*X* Received {bytesReceived / oneMB} MB so far");
                //this.thisProxy.PrintMessageFork("hey!", 3.14);
            }

            return arg;
        }

        public async Task PrintMessageAsync(string s, double d)
        {
            Console.WriteLine($"Server: {d}: {s}");
        }
        protected override void BecomingPrimary()
        {
            Console.WriteLine("Becoming a primary now");
        }
    }

    [DataContract]
    class ServerUpgraded : Immortal, IServer
    {
        [DataMember]
        internal long bytesReceived = 0;

        const long oneGig = ((long)1 * 1024 * 1024 * 1024);

        public ServerUpgraded()
        {
        }

        public ServerUpgraded(Server copyFrom)
        {
            bytesReceived = copyFrom.bytesReceived;
        }

        protected override async Task<bool> OnFirstStart()
        {
            Console.WriteLine("Server Upgraded in Entry Point");
            return true;
        }

        public async Task PrintBytesReceivedAsync()
        {
            Console.WriteLine("Bytes received: {0}", bytesReceived);
            Console.WriteLine("DONE");
            Console.Out.Flush();
            Console.Out.Flush();
        }

        public async Task<byte[]> MAsync(byte[] arg)
        {
            bytesReceived += arg.Length;
            if ((bytesReceived - arg.Length) / oneGig != bytesReceived / oneGig)
            {
                Console.WriteLine($"Service Upgraded Received {bytesReceived / (1024 * 1024)} MB so far");
            }
            return arg;
        }
        
        public async Task PrintMessageAsync(string s, double d)
        {
            Console.WriteLine($"Server Upgraded: {d}: {s}");
        }
    }

    class ServerBootstrapper
    {
        private static int _receivePort = -1;
        private static int _sendPort = -1;
        private static string _perfServer;
        private static bool _autoContinue;

        static void Main(string[] args)
        {
            ParseAndValidateOptions(args);

            // for debugging don't want to auto continue but for test automation want this to auto continue
            if (!_autoContinue)
            {
                Console.WriteLine("Pausing execution of " + _perfServer + ". Press enter to deploy and continue.");
                Console.ReadLine();
            }

            using (var c = AmbrosiaFactory.Deploy<IServer>(_perfServer, new Server(), _receivePort, _sendPort))
            {
                // nothing to call on c, just doing this for calling Dispose.
                Console.WriteLine("*X* Press enter to terminate program.");
                Console.ReadLine();
            }

            // for upgrading
/*               using (var c = AmbrosiaFactory.Deploy<IServer, IServer, ServerUpgraded>(perfServer, new Server(), receivePort, sendPort))
                        {
                            // nothing to call on c, just doing this for calling Dispose.
                            Console.WriteLine("Press enter to terminate program.");
                            Console.ReadLine();
                        }
*/                        
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
                { "s|serverName=", "The service name of the server [REQUIRED].", s => _perfServer = s },
                { "rp|receivePort=", "The service receive from port [REQUIRED].", rp => _receivePort = int.Parse(rp) },
                { "sp|sendPort=", "The service send to port. [REQUIRED]", sp => _sendPort = int.Parse(sp) },
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
            var name = typeof(ServerBootstrapper).Assembly.GetName().Name;
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

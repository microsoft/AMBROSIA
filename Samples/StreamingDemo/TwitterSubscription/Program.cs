using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Runtime.Serialization;
using System.Reactive.Linq;
using TwitterObservable;
using Analytics;
using static Ambrosia.ServiceConfiguration;
using System.Threading;
using Ambrosia;
using Mono.Options;
using Empty = Microsoft.StreamProcessing.Empty;

namespace TwitterHost
{
    [DataContract]
    sealed class TwitterSubscription : Immortal
    {
        [DataMember]
        IAnalyticsProxy _resilientTwitterProxy;

        public TwitterSubscription()
        {
        }

        /// <summary>
        /// read settings from configuration file
        /// </summary>
        /// <returns></returns>
        public static TwitterConfig ReadTwitterInputConfig()
        {
            return new TwitterConfig
            {
                // Put your keys and secrets here
                oauth_consumer_key = "",
                oauth_consumer_secret = "",
                oauth_token = "",
                oauth_token_secret = "",
                twitter_keywords = "Office Microsoft,Surface Microsoft,Phone Window,Windows 8,SQL Server,SharePoint,Bing,Skype,XBox,System Center,Microsoft,msftluv"
            };
        }

        protected override async Task<bool> OnFirstStart()
        {
            // Read Twitter auth information and topics of interest from App.config
            var twitterConfig = ReadTwitterInputConfig();

            // Create an observable of tweets with sentiment
            var inputObservable = TwitterStream.Create(twitterConfig);

            _resilientTwitterProxy = GetProxy<IAnalyticsProxy>(AnalyticsServiceName);
            inputObservable.ForEachAsync(
                    x =>
                    {
                        if (x.IsData)
                        {
                            Console.WriteLine("{0}", x.ToString());
                            _resilientTwitterProxy.OnNextFork(x);
                        }
                    });

            return true;
        }
    }

    class Program
    {
        private static int _receivePort = -1;
        private static int _sendPort = -1;
        private static string _serviceName = SubscriptionServiceName;

        static void Main(string[] args)
        {
            ParseAndValidateOptions(args);

            //Console.WriteLine("Pausing execution. Press enter to deploy and continue.");
            //Console.ReadLine();

            var myClient = new TwitterSubscription();

            // Use "Empty" as the type parameter because this container doesn't run a service that responds to any RPC calls.
            using (var c = AmbrosiaFactory.Deploy<Empty>(_serviceName, myClient, _receivePort, _sendPort))
            {
                // nothing to call on c, just doing this for calling Dispose.
                //Console.WriteLine("Press enter to terminate program.");
                //Console.ReadLine();
                Thread.Sleep(TimeSpan.FromDays(10));
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
                { "rp|recievePort=", "The service recieve from port [REQUIRED].", rp => _receivePort = int.Parse(rp) },
                { "sp|sendPort=", "The service send to port. [REQUIRED]", sp => _sendPort = int.Parse(sp) },
                { "n|serviceName=", "The service name.", n => _serviceName = n },
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
            if (_sendPort == -1) errorMessage += "Send port is required.\n";
            if (_receivePort == -1) errorMessage += "Recieve port is required.\n";

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
            var name = typeof(Program).Assembly.GetName().Name;
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

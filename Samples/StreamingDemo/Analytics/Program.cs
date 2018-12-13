using System;
using System.Runtime.Serialization;
using TwitterObservable;
using Microsoft.StreamProcessing;
using System.Reactive.Subjects;
using DashboardAPI;
using System.IO;
using static Ambrosia.ServiceConfiguration;
using System.Threading;
using System.Threading.Tasks;
using Ambrosia;
using Mono.Options;
using System.Reactive.Linq;

namespace Analytics
{
    [DataContract]
    class Analytics : Immortal<IAnalyticsProxy>, IAnalytics
    {
        Subject<StreamEvent<Tweet>> _tweetConduit;
        Process _queryProcess;
        IObservable<StreamEvent<Tweet>> _inputObservable;

        [DataMember]
        IDashboardProxy _dashboard;

        [DataMember]
        bool _serializedQueryOnce;


        public Analytics()
        {
            _serializedQueryOnce = false;
        }

        protected override async Task<bool> OnFirstStart()
        {
            _dashboard = GetProxy<IDashboardProxy>(DashboardServiceName);
            lock (_dashboard)
            {
                var query = CreateQuery();
                _queryProcess = query.Restore();
            }
            return true;
        }

        private QueryContainer CreateQuery()
        {
            var query = new QueryContainer();
            Config.ForceRowBasedExecution = true;

            // incoming events are received via a conduit
            _tweetConduit = new Subject<StreamEvent<Tweet>>();
            var streamInput = query.RegisterInput(_tweetConduit, OnCompletedPolicy.None(), DisorderPolicy.Drop(TimeSpan.FromSeconds(3).Ticks),
                PeriodicPunctuationPolicy.Time((ulong)TimeSpan.FromMilliseconds(1).Ticks));

            // outgoing events are pushed to the dashboard
            var myOutput = query.RegisterOutput(TwitterAnalytics(streamInput), ReshapingPolicy.None());
            myOutput.Subscribe(tweetEvent => Publish(tweetEvent));
            return query;
        }

        IStreamable<Microsoft.StreamProcessing.Empty, RankedSentiment> TwitterAnalytics(IStreamable<Microsoft.StreamProcessing.Empty, Tweet> input)
        {
            return
             // Sample analytics query - output average sentiment score per tweet topic
             input
                 .GroupApply(e => e.Topic,
                             str => str.HoppingWindowLifetime(TimeSpan.FromMinutes(5).Ticks, TimeSpan.FromSeconds(5).Ticks)
                                   .Average(e => e.SentimentScore),
                             (g, c) => new RankedSentiment { Topic = g.Key, AvgSentiment = c })
                //.TopK(10)
                .AlterEventDuration(1)
                ;
        }

        private void Publish(StreamEvent<RankedSentiment> tweetEvent)
        {
            if (tweetEvent.IsData)
            {
                _dashboard.OnNextFork(tweetEvent.ToResult());
            }
        }

        public async Task OnNextAsync(StreamEvent<Tweet> next)
        {
            lock (_dashboard)
            {
                if (_queryProcess != null)
                {
                    _tweetConduit.OnNext(next);
                }
            }
        }

        protected override void OnSave(Stream stream)
        {
            if (_queryProcess != null)
            {
                lock (_dashboard)
                {
                    _queryProcess.Checkpoint(stream);
                    _serializedQueryOnce = true;
                }
            }
        }

        protected override void BecomingPrimary()
        {
            // Read Twitter auth information and topics of interest from App.config
            var twitterConfig = ReadTwitterInputConfig();

            // Create an observable of tweets with sentiment
            _inputObservable = TwitterStream.Create(twitterConfig);

            _inputObservable.ForEachAsync(
                    x =>
                    {
                        if (x.IsData)
                        {
                            Console.WriteLine("{0}", x.ToString());
                            thisProxy.OnNextFork(x);
                        }
                    });
        }

        protected override void OnRestore(Stream stream)
        {
            if (_serializedQueryOnce)
            {
                Console.WriteLine("Deserializing Trill Query!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                var query = CreateQuery();
                _queryProcess = query.Restore(stream);
            }
        }

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
    }


    class Program
    {
        private static int _receivePort = -1;
        private static int _sendPort = -1;

        static void Main(string[] args)
        {
            ParseAndValidateOptions(args);

            //Console.WriteLine("Pausing execution. Press enter to deploy and continue.");
            //Console.ReadLine();

            var myImmortal = new Analytics();

            using (var c = AmbrosiaFactory.Deploy<IAnalytics>(AnalyticsServiceName, myImmortal, _receivePort, _sendPort))
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

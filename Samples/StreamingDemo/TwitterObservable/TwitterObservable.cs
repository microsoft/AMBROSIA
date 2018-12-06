using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Globalization;

using System.Reactive.Linq;
using Microsoft.StreamProcessing;

namespace TwitterObservable
{

    public static class TwitterStream
    {
        internal static Dictionary<TwitterConfig, IObservable<StreamEvent<Tweet>>> twitterReaders = new Dictionary<TwitterConfig,IObservable<StreamEvent<Tweet>>>();
        internal static Dictionary<TwitterConfig, IDisposable> twitterReaderDisposers = new Dictionary<TwitterConfig,IDisposable>();

        public static IObservable<StreamEvent<Tweet>> Create(TwitterConfig config)
        {
            IObservable<StreamEvent<Tweet>> obs;
            if (twitterReaders.TryGetValue(config, out obs))
            {
                return obs;
            }
            var newobs = new TwitterStreamEventObservable(config).Publish();
            var disp = newobs.Connect();
            twitterReaderDisposers.Add(config, disp);
            twitterReaders.Add(config, newobs);
            return newobs;
        }

        public static void Dispose(TwitterConfig config)
        {
            IDisposable disp;
            if (!twitterReaderDisposers.TryGetValue(config, out disp))
            {
                return;
            }
            disp.Dispose();

            twitterReaderDisposers.Remove(config);
            twitterReaders.Remove(config);
        }

        public static void DisposeAll()
        {
            foreach (var key in twitterReaders.Keys)
            {
                Dispose(key);
            }
        }
    }

    internal class TwitterStreamEventObservable : IObservable<StreamEvent<Tweet>>
    {
        private TwitterConfig _config;

        public TwitterStreamEventObservable(TwitterConfig config)
        {
            this._config = config;
        }

        public IDisposable Subscribe(IObserver<StreamEvent<Tweet>> observer)
        {
            return new TwitterSubscription(_config, observer);
        }
    }

    internal class TwitterSubscription : IDisposable
    {
        private readonly static IFormatProvider DateFormatProvider = CultureInfo.GetCultureInfo("en-us").DateTimeFormat;
        private const string DateFormatString = "ddd MMM dd HH:mm:ss yyyy";
        private bool _disposed = false;
        private Sentiment140 _sentimentService;
        private TwitterStreaming _twitterStreaming;
        private TwitterConfig _config;
        private IObserver<StreamEvent<Tweet>> Observer;

        public TwitterSubscription(TwitterConfig config, IObserver<StreamEvent<Tweet>> observer)
        {
            this._config = config;
            this._twitterStreaming = new TwitterStreaming(config);
            this._sentimentService = new Sentiment140();
            this.Observer = observer;

            new Thread(new ThreadStart(ProduceEvents)).Start();
        }

        private void ProduceEvents()
        {
            try
            {
                using (var streamReader = this._twitterStreaming.Read())
                {
                    // Loop until stop signal
                    while (!_disposed)
                    {
                        try
                        {
                            // Read from source
                            var line = streamReader.ReadLine();

                            if (string.IsNullOrEmpty(line)) continue;

                            // Parse
                            var jObject = JObject.Parse(line);
                            string text = Unquote(jObject.SelectToken("text").Value<string>());
                            string topic = TextAnalysis.DetermineTopc(text, _config.twitter_keywords);
                            string language = (null == jObject.SelectToken("user.lang")) ? string.Empty : jObject.SelectToken("user.lang").Value<string>();

                            // filter out tweets we don't care about: non-english tweets or deleted tweets
                            if ((jObject["delete"] != null) ||
                                (language != "en") ||
                                (topic == "Unknown"))
                                continue;

                            var tweet = new Tweet();
                            tweet.ID = jObject.SelectToken("id_str").Value<Int64>();
                            tweet.CreatedAt = ParseTwitterDateTime(jObject.SelectToken("created_at").Value<string>());
                            tweet.UserName = Unquote(jObject.SelectToken("user.screen_name").Value<string>());
                            tweet.ProfileImageUrl = jObject.SelectToken("user.profile_image_url").Value<string>();
                            tweet.Text = text;
                            tweet.Topic = topic;
                            tweet.RawJson = line;

                            tweet.SentimentScore = this.Sentiment(text);

                            // Produce INSERT event
                            Observer.OnNext(StreamEvent.CreatePoint(tweet.CreatedAt.Ticks, tweet));

                        }
                        catch (Exception ex)
                        {
                            // Error handling should go here
                            Console.WriteLine("EXCEPTION RAISED in TwitterInputAdapter: " + ex.Message);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Observer.OnError(e);
                TwitterStream.Dispose(_config);
            }
        }

        private string Unquote(string str)
        {
            return str.Trim('"');
        }

        private DateTime ParseTwitterDateTime(string p)
        {
            p = p.Replace("+0000 ", "");
            DateTimeOffset result;

            if (DateTimeOffset.TryParseExact(p, DateFormatString, DateFormatProvider, DateTimeStyles.AssumeUniversal, out result))
                return result.DateTime;
            else
                return DateTime.Now;
        }

        private int Sentiment(string text)
        {
            try
            {
                var result = this._sentimentService.Analyze(text);
                return (int)result.Mood;
            }
            catch (Exception e)
            {
                Console.WriteLine(this.GetType().ToString() + ".Sentiment - " + e.Message + e.StackTrace);
                return 0;
            }
        }

        public void Dispose()
        {
            this._disposed = true;
        }
    }
}

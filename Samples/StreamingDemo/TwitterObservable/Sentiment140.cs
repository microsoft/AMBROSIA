using System.IO;
using System.Net;
using System.Web;
using Newtonsoft.Json.Linq;

namespace TwitterObservable
{
    /// <summary>
    /// the implementation for the Viralheat Sentiment Service
    /// </summary>
    internal class Sentiment140 
    {
        private string _jsonURL = @"http://www.sentiment140.com/api/classify";

        public Sentiment140()
        { }

        public SentimentAnalysisResult Analyze(string textToAnalyze)
        {

            string url = string.Format("{0}?text={1}", this._jsonURL, 
                                        HttpUtility.UrlEncode(textToAnalyze, System.Text.Encoding.UTF8));

            var request = HttpWebRequest.Create(url);
            var response = request.GetResponse();

            using (var streamReader = new StreamReader(response.GetResponseStream()))
            {
                SentimentAnalysisResult result = new SentimentAnalysisResult() { Mood = SentimentScore.Neutral, Probability = 100 };

                try
                {

                    // Read from source
                    var line = streamReader.ReadLine();

                    // Parse
                    var jObject = JObject.Parse(line);

                    int polarity = jObject.SelectToken("results", true).SelectToken("polarity", true).Value<int>();
                    switch (polarity)
                    {
                        case 0:
                            result.Mood = SentimentScore.Negative;
                            break;
                        case 4:
                            result.Mood = SentimentScore.Positive;
                            break;
                        default: // 2 or others
                            result.Mood = SentimentScore.Neutral;
                            break;
                    }
                }
                catch (System.Exception)
                {
                    result.Mood = SentimentScore.Neutral;
                    result.Probability = 0;
                }

                return result;
            }
        }

    }
}

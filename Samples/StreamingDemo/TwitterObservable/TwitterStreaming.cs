using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Configuration;

namespace TwitterObservable
{
    internal class TwitterStreaming
    {
        private TwitterConfig _config;

        public TwitterStreaming(TwitterConfig config)
        {
            this._config = config;
        }

        public TextReader Read()
        {
            {

                var oauth_version = "1.0";
                var oauth_signature_method = "HMAC-SHA1";

                // unique request details
                var oauth_nonce = Convert.ToBase64String(
                new ASCIIEncoding().GetBytes(DateTime.Now.Ticks.ToString()));
                var timeSpan = DateTime.UtcNow
                - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                var oauth_timestamp = Convert.ToInt64(timeSpan.TotalSeconds).ToString();

                var resource_url = "https://stream.twitter.com/1.1/statuses/filter.json";

                // create oauth signature
                var baseFormat = "oauth_consumer_key={0}&oauth_nonce={1}&oauth_signature_method={2}" +
                "&oauth_timestamp={3}&oauth_token={4}&oauth_version={5}&track={6}";

                var baseString = string.Format(baseFormat,
                _config.oauth_consumer_key,
                oauth_nonce,
                oauth_signature_method,
                oauth_timestamp,
                _config.oauth_token,
                oauth_version,
                Uri.EscapeDataString(_config.twitter_keywords)
                );

                baseString = string.Concat("POST&", Uri.EscapeDataString(resource_url), "&", Uri.EscapeDataString(baseString));

                var compositeKey = string.Concat(Uri.EscapeDataString(_config.oauth_consumer_secret),
                "&", Uri.EscapeDataString(_config.oauth_token_secret));

                string oauth_signature;
                using (HMACSHA1 hasher = new HMACSHA1(ASCIIEncoding.ASCII.GetBytes(compositeKey)))
                {
                    oauth_signature = Convert.ToBase64String(
                    hasher.ComputeHash(ASCIIEncoding.ASCII.GetBytes(baseString)));
                }

                // create the request header
                var headerFormat = "OAuth oauth_nonce=\"{0}\", oauth_signature_method=\"{1}\", " +
                "oauth_timestamp=\"{2}\", oauth_consumer_key=\"{3}\", " +
                "oauth_token=\"{4}\", oauth_signature=\"{5}\", " +
                "oauth_version=\"{6}\"";

                var authHeader = string.Format(headerFormat,
                Uri.EscapeDataString(oauth_nonce),
                Uri.EscapeDataString(oauth_signature_method),
                Uri.EscapeDataString(oauth_timestamp),
                Uri.EscapeDataString(_config.oauth_consumer_key),
                Uri.EscapeDataString(_config.oauth_token),
                Uri.EscapeDataString(oauth_signature),
                Uri.EscapeDataString(oauth_version)
                );


                // make the request

                ServicePointManager.Expect100Continue = false;

                var postBody = "track=" + _config.twitter_keywords; 
                resource_url += "?" + postBody;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(resource_url);
                request.Headers.Add("Authorization", authHeader);
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                request.PreAuthenticate = true;
                request.AllowWriteStreamBuffering = true;

                WebResponse response = request.GetResponse();


                return new StreamReader(response.GetResponseStream());
            }
        }

     
    }
}

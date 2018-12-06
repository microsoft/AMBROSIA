namespace TwitterObservable
{
    /// <summary>
    /// Configuration for textToAnalyze adapter
    /// </summary>
    public struct TwitterConfig
    {
        public string oauth_consumer_key { get; set; }
        public string oauth_consumer_secret { get; set; }
        public string oauth_token { get; set; }
        public string oauth_token_secret { get; set; }

        public string twitter_keywords { get; set; }

        public long delay_ms { get; set; }
    }
}

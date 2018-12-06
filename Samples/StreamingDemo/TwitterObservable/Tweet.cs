using Microsoft.StreamProcessing;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace TwitterObservable
{
    [DataContract]
    public struct Tweet
    {
        [DataMember]
        public Int64 ID { get; set; }
        [DataMember]
        public DateTime CreatedAt { get; set; }
        [DataMember]
        public string UserName { get; set; }
        [DataMember]
        public string ProfileImageUrl { get; set; }
        [DataMember]
        public string Text { get; set; }
        [DataMember]
        public string Language { get; set; }
        [DataMember]
        public string Topic { get; set; }
        [DataMember]
        public int SentimentScore { get; set; }

        [DataMember]
        public string RawJson { get; set; }

        public override string ToString()
        {
            return new { ID, CreatedAt, UserName, ProfileImageUrl, Text, Language, Topic, SentimentScore }.ToString();
        }
    }

    public enum SentimentScore
    {
        Positive = 4,
        Neutral = 2,
        Negative = 0,
        Undefined = -1
    }

    [DataContract]
    public struct AnalyticsResultString
    {
        [DataMember]
        public string topkTopics;
    }

    [DataContract]
    public struct RankedSentiment
    {
        [DataMember]
        public int Rank;
        [DataMember]
        public string Topic;
        [DataMember]
        public double AvgSentiment;

        public override string ToString()
        {
            return Rank.ToString() + "\t" + Topic + "\t" + AvgSentiment.ToString();
        }
    }

    public static class Extension
    {
        public static AnalyticsResultString ToResult(this StreamEvent<List<RankedEvent<RankedSentiment>>> list)
        {
            string result = "\n" + new DateTime(list.StartTime).ToString() + ":\n";

            foreach (var entry in list.Payload)
            {
                result += entry.Rank + "\t" + entry.Payload + "\n";
            }
            return new AnalyticsResultString { topkTopics = result };
        }

        public static AnalyticsResultString ToResult(this StreamEvent<RankedSentiment> list)
        {
            string result = new DateTime(list.StartTime).ToString() + "\t";
            result += list.Payload.Topic + "\t" + list.Payload.AvgSentiment;
            return new AnalyticsResultString { topkTopics = result };
        }
    }
    public class SentimentAnalysisResult
    {
        public SentimentScore Mood { get; set; }
        //public string SentimentScore { get; set; }
        public double Probability { get; set; }
    }
}

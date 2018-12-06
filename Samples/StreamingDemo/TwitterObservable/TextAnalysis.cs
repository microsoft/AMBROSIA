using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TwitterObservable
{
    internal class TextAnalysis
    {
        /// <summary>
        /// Lazy man's text analysis.  Only for demonstration purposes.
        /// </summary>
        /// <param name="tweetText"></param>
        /// <param name="keywordFilters"></param>
        /// <returns></returns>
        public static string DetermineTopc(string tweetText, string keywordFilters)
        {
            if (string.IsNullOrEmpty(tweetText)) return string.Empty;

            string subject = string.Empty;

            //keyPhrases are specified in app.config separated by commas.  Can have no leading or trailing spaces.  Example of key phrases in app.config
            //	<add key="twitter_keywords" value="Microsoft, Office, Surface,Windows Phone,Windows 8,Windows Server,SQL Server,SharePoint,Bing,Skype,XBox,System Center"/><!--comma to spit multiple keywords-->
            string[] keyPhrases = keywordFilters.Split(',');

            foreach (string keyPhrase in keyPhrases)
            {
                 
                subject = keyPhrase;
               
                //a key phrase may have multiple key words, like: Windows Phone.  If this is the case we will only assign it a subject if both words are 
                //included and in the correct order. For example, a tweet will match if "Windows 8" is found within the tweet but will not match if
                // the tweet is "There were 8 broken Windows".  This is not case sensitive
               
                //string[] keywords = keyPhrase.Split(' ');
               // if (tweetText.Contains(keywords[0])) break;
               
                
                // if (tweetText.ToUpper().Contains(keyPhrase.ToUpper())) return subject;
                //if (tweetText.ToUpper().Contains("#" + keyPhrase.ToUpper().Replace(" ", ""))) return subject;
                //if (tweetText.ToUpper().Contains("@" + keyPhrase.ToUpper().Replace(" ", ""))) return subject;

                //Creates one array that breaks the tweet into individual words and one array that breaks the key phrase into individual words.  Within 
                //This for loop another array is created from the tweet that includes the same number of words as the keyphrase.  These are compared.  For example,
                // KeyPhrase = "Microsoft Office" Tweet= "I Love Microsoft Office"  "Microsoft Office" will be compared to "I Love" then "Love Microsoft" and 
                //Finally "Microsoft Office" which will be returned as the subject.  if no match is found "Do Not Include" is returned. 
                string[] KeyChunk = keyPhrase.Trim().Split(' ');
                string[] tweetTextChunk = tweetText.Split(' ');
                string Y;
                for (int i = 0; i <= (tweetTextChunk.Length - KeyChunk.Length) ; i++)
                {
                    Y = null;
                    for (int j = 0; j <= (KeyChunk.Length-1); j++)
                    {
                        Y += tweetTextChunk[(i + j)] + " ";
                    }
                    if (Y != null) Y = Y.Trim();
                    if (Y.ToUpper().Contains(keyPhrase.ToUpper())) { return subject; }
                }
            }
             
            return "Unknown";   
        }
    }
}
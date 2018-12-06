using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitterObservable;
using Microsoft.StreamProcessing;

namespace Analytics
{
    public interface IAnalytics
    {
        void OnNext(StreamEvent<Tweet> next);
    }
}
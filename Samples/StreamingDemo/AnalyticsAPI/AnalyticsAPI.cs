using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitterObservable;
using Microsoft.StreamProcessing;
using Ambrosia;

namespace Analytics
{
    public interface IAnalytics
    {
        [ImpulseHandler]
        void OnNext(StreamEvent<Tweet> next);
    }
}
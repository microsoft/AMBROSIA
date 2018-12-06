using Microsoft.StreamProcessing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitterObservable;

namespace TwitterHostAPI
{
    public interface ITwitterHostIDL
    {
        void OnNext();
    }

    public interface ITwitterHost
    {
        void OnNext(StreamEvent<Tweet> next);
    }

    public interface ITwitterHostProxy : ITwitterHost
    {
        Task OnNextAsync(StreamEvent<Tweet> next);
        void OnNextFork(StreamEvent<Tweet> next);
    }
}

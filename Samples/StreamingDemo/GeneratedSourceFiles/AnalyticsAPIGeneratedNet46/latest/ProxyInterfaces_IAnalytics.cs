
using System;
using Ambrosia;
using System.Threading.Tasks;
using static Ambrosia.StreamCommunicator;

namespace Analytics
{
    /// <summary>
    // Generated from IAnalytics by the proxy generation.
    // This is the API that any immortal implementing the interface must be a subtype of.
    /// </summary>
    public interface IAnalytics
    {
        Task OnNextAsync(Microsoft.StreamProcessing.StreamEvent<TwitterObservable.Tweet> p_0);
    }

    /// <summary>
    // Generated from IAnalytics by the proxy generation.
    // This is the API that is used to call a immortal that implements
    /// </summary>
    [Ambrosia.InstanceProxy(typeof(IAnalytics))]
    public interface IAnalyticsProxy
    {
        Task OnNextAsync(Microsoft.StreamProcessing.StreamEvent<TwitterObservable.Tweet> p_0);
        void OnNextFork(Microsoft.StreamProcessing.StreamEvent<TwitterObservable.Tweet> p_0);
    }
}

using System;
using Ambrosia;
using System.Threading.Tasks;
using static Ambrosia.StreamCommunicator;

namespace DashboardAPI
{
    /// <summary>
    // Generated from IDashboard by the proxy generation.
    // This is the API that any immortal implementing the interface must be a subtype of.
    /// </summary>
    public interface IDashboard
    {
        Task OnNextAsync(TwitterObservable.AnalyticsResultString p_0);
    }

    /// <summary>
    // Generated from IDashboard by the proxy generation.
    // This is the API that is used to call a immortal that implements
    /// </summary>
    [Ambrosia.InstanceProxy(typeof(IDashboard))]
    public interface IDashboardProxy
    {
        Task OnNextAsync(TwitterObservable.AnalyticsResultString p_0);
        void OnNextFork(TwitterObservable.AnalyticsResultString p_0);
    }
}
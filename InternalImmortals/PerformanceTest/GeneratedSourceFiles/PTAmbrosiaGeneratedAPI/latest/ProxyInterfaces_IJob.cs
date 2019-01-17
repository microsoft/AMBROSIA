
using System;
using Ambrosia;
using System.Threading.Tasks;
using static Ambrosia.StreamCommunicator;

namespace Job
{
    /// <summary>
    // Generated from IJob by the proxy generation.
    // This is the API that any immortal implementing the interface must be a subtype of.
    /// </summary>
    public interface IJob
    {
        Task PrintBytesReceivedAsync();
    }

    /// <summary>
    // Generated from IJob by the proxy generation.
    // This is the API that is used to call a immortal that implements
    /// </summary>
    [Ambrosia.InstanceProxy(typeof(IJob))]
    public interface IJobProxy
    {
        Task PrintBytesReceivedAsync();
        void PrintBytesReceivedFork();
    }
}
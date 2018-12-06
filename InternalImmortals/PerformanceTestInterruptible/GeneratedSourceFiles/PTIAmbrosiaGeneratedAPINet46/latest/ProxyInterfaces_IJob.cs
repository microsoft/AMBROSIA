
using System;
using Ambrosia;
using System.Threading.Tasks;
using static Ambrosia.StreamCommunicator;

namespace JobAPI
{
    /// <summary>
    // Generated from IJob by the proxy generation.
    // This is the API that any immortal implementing the interface must be a subtype of.
    /// </summary>
    public interface IJob
    {
        Task JobContinueAsync(System.Int32 p_0,System.Int64 p_1,JobAPI.BoxedDateTime p_2);
        Task MAsync(System.Byte[] p_0);
        Task PrintBytesReceivedAsync();
    }

    /// <summary>
    // Generated from IJob by the proxy generation.
    // This is the API that is used to call a immortal that implements
    /// </summary>
    [Ambrosia.InstanceProxy(typeof(IJob))]
    public interface IJobProxy
    {
        Task JobContinueAsync(System.Int32 p_0,System.Int64 p_1,JobAPI.BoxedDateTime p_2);
        void JobContinueFork(System.Int32 p_0,System.Int64 p_1,JobAPI.BoxedDateTime p_2);
        Task MAsync(System.Byte[] p_0);
        void MFork(System.Byte[] p_0);
        Task PrintBytesReceivedAsync();
        void PrintBytesReceivedFork();
    }
}
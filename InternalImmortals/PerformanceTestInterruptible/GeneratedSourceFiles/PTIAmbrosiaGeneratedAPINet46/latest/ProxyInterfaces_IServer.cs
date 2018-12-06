
using System;
using Ambrosia;
using System.Threading.Tasks;
using static Ambrosia.StreamCommunicator;

namespace Server
{
    /// <summary>
    // Generated from IServer by the proxy generation.
    // This is the API that any immortal implementing the interface must be a subtype of.
    /// </summary>
    public interface IServer
    {
        Task MAsync(System.Byte[] p_0);
        Task AmIHealthyAsync(System.DateTime p_0);
        Task PrintBytesReceivedAsync();
    }

    /// <summary>
    // Generated from IServer by the proxy generation.
    // This is the API that is used to call a immortal that implements
    /// </summary>
    [Ambrosia.InstanceProxy(typeof(IServer))]
    public interface IServerProxy
    {
        Task MAsync(System.Byte[] p_0);
        void MFork(System.Byte[] p_0);
        void AmIHealthyFork(System.DateTime p_0);
        Task PrintBytesReceivedAsync();
        void PrintBytesReceivedFork();
    }
}
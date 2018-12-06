
using System;
using Ambrosia;
using System.Threading.Tasks;
using static Ambrosia.StreamCommunicator;

namespace IServer
{
    /// <summary>
    // Generated from IServer by the proxy generation.
    // This is the API that any immortal implementing the interface must be a subtype of.
    /// </summary>
    public interface IServer
    {
        Task<Int32> ReceiveMessageAsync(System.String p_0);
    }

    /// <summary>
    // Generated from IServer by the proxy generation.
    // This is the API that is used to call a immortal that implements
    /// </summary>
    [Ambrosia.InstanceProxy(typeof(IServer))]
    public interface IServerProxy
    {
        Task<Int32> ReceiveMessageAsync(System.String p_0);
        void ReceiveMessageFork(System.String p_0);
    }
}
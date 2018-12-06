
using System;
using Ambrosia;
using System.Threading.Tasks;
using static Ambrosia.StreamCommunicator;

namespace IClient
{
    /// <summary>
    // Generated from IClient by the proxy generation.
    // This is the API that any immortal implementing the interface must be a subtype of.
    /// </summary>
    public interface IClient
    {
        Task SendMessageAsync(System.String p_0);
    }

    /// <summary>
    // Generated from IClient by the proxy generation.
    // This is the API that is used to call a immortal that implements
    /// </summary>
    [Ambrosia.InstanceProxy(typeof(IClient))]
    public interface IClientProxy
    {
        Task SendMessageAsync(System.String p_0);
        void SendMessageFork(System.String p_0);
    }
}
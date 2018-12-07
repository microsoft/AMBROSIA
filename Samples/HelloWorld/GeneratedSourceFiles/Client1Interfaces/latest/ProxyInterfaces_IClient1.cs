
using System;
using Ambrosia;
using System.Threading.Tasks;
using static Ambrosia.StreamCommunicator;

namespace IClient1
{
    /// <summary>
    // Generated from IClient1 by the proxy generation.
    // This is the API that any immortal implementing the interface must be a subtype of.
    /// </summary>
    public interface IClient1
    {
        Task SendMessageAsync(System.String p_0);
    }

    /// <summary>
    // Generated from IClient1 by the proxy generation.
    // This is the API that is used to call a immortal that implements
    /// </summary>
    [Ambrosia.InstanceProxy(typeof(IClient1))]
    public interface IClient1Proxy
    {
        Task SendMessageAsync(System.String p_0);
        void SendMessageFork(System.String p_0);
    }
}
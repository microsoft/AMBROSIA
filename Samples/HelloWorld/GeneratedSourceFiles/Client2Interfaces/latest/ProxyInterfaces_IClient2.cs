
using System;
using Ambrosia;
using System.Threading.Tasks;
using static Ambrosia.StreamCommunicator;

namespace Client2
{
    /// <summary>
    // Generated from IClient2 by the proxy generation.
    // This is the API that any immortal implementing the interface must be a subtype of.
    /// </summary>
    public interface IClient2
    {
        Task ReceiveKeyboardInputAsync(System.String p_0);
    }

    /// <summary>
    // Generated from IClient2 by the proxy generation.
    // This is the API that is used to call a immortal that implements
    /// </summary>
    [Ambrosia.InstanceProxy(typeof(IClient2))]
    public interface IClient2Proxy
    {
        void ReceiveKeyboardInputFork(System.String p_0);
    }
}
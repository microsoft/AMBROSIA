
using System;
using Ambrosia;
using System.Threading.Tasks;
using static Ambrosia.StreamCommunicator;

namespace XamarinCommandShell
{
    /// <summary>
    // Generated from ICommandShellImmortal by the proxy generation.
    // This is the API that any immortal implementing the interface must be a subtype of.
    /// </summary>
    public interface ICommandShellImmortal
    {
        Task SubmitCommandAsync(System.String p_0);
        Task SetCurrentDirectoryAsync(System.String p_0);
    }

    /// <summary>
    // Generated from ICommandShellImmortal by the proxy generation.
    // This is the API that is used to call a immortal that implements
    /// </summary>
    [Ambrosia.InstanceProxy(typeof(ICommandShellImmortal))]
    public interface ICommandShellImmortalProxy
    {
        void SubmitCommandFork(System.String p_0);
        void SetCurrentDirectoryFork(System.String p_0);
    }
}
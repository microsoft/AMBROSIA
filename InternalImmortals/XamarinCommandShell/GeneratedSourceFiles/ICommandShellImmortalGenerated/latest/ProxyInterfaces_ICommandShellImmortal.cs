
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
        Task SetRootDirectoryAsync(System.String p_0);
        Task SetRelativeDirectoryAsync(System.String p_0);
        Task AddConsoleOutputAsync(System.String p_0);
        Task IncCurrentCommandAsync();
        Task DecCurrentCommandAsync();
    }

    /// <summary>
    // Generated from ICommandShellImmortal by the proxy generation.
    // This is the API that is used to call a immortal that implements
    /// </summary>
    [Ambrosia.InstanceProxy(typeof(ICommandShellImmortal))]
    public interface ICommandShellImmortalProxy
    {
        void SubmitCommandFork(System.String p_0);
        void SetRootDirectoryFork(System.String p_0);
        void SetRelativeDirectoryFork(System.String p_0);
        void AddConsoleOutputFork(System.String p_0);
        void IncCurrentCommandFork();
        void DecCurrentCommandFork();
    }
}
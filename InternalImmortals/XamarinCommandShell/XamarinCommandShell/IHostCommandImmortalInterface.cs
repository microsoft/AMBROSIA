using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace XamarinCommandShell
{
    /// <summary>
    /// IHostCommandImmortalInterface defines the interface which the GUI uses to interact with the CommandShellImmortal. As such,
    /// CommandShellImmortal implements both this, and ICommandShellImmortal. Note that the rule of thumb for the implementation of
    /// these methods (implemented in CommandShellImmortal.cs) is as follows:
    /// 
    /// 1. Any method which alters the state of the immortal must do so through an impulse call, which is logged, and therefore replayed
    /// upon recovery.
    /// 
    /// 2. Any method which retrieves recoverable state may simply return an unmodifiable form of the state, in a threadsafe manner.
    /// We retrieve this state through properties.
    /// </summary>
    public interface IHostCommandImmortalInterface
    {
        /// <summary>
        /// Submits a command to the CommandShellImmortal
        /// </summary>
        void HostSubmitCommand(string command);

        /// <summary>
        /// Adds console output to the CommandShellImmortal
        /// </summary>
        void HostAddConsoleOutput(string output);

        /// <summary>
        /// Sets the root directory for the CommandShellImmortal
        /// </summary>
        void HostSetRootDirectory(string newDirectory);

        /// <summary>
        /// Sets the relative directory for the CommandShellImmortal
        /// </summary>
        void HostSetRelativeDirectory(string newDirectory);

        /// <summary>
        /// Gets the root directory for the CommandShellImmortal
        /// </summary>
        string HostRootDirectory();

        /// <summary>
        /// Gets the relative directory for the CommandShellImmortal
        /// </summary>
        string HostRelativeDirectory();

        /// <summary>
        /// Gets the console output for the CommandShellImmortal
        /// </summary>
        string HostConsoleOutput();

        /// <summary>
        /// Gets the current command for the CommandShellImmortal from the command history
        /// </summary>
        string HostCurrentCommand();

        /// <summary>
        /// Gets the previous command for the CommandShellImmortal from the command history
        /// </summary>
        Task<string> HostPreviousCommandAsync();

        /// <summary>
        /// Gets the next command for the CommandShellImmortal from the command history
        /// </summary>
        Task<string> HostNextCommandAsync();
    }
}

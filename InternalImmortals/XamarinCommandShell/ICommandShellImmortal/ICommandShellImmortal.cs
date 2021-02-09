using System;
using Ambrosia;

namespace XamarinCommandShell
{
    /// <summary>
    /// ICommandShellImmortal defines the Immortal (distributed) interface to the command shell app. These are the messages 
    /// which appear in the CommandShell replay log, and are replayed during recovery. In this case, all the logged messages 
    /// are impulses which ingress user input from the Xamain GUI.
    /// </summary>
    public interface ICommandShellImmortal
    {
        /// <summary>
        /// SubmitCommand is the impulse used to ingress a command into the durable part of the CommandShell state. In 
        /// particular, commands submitted through this API will get added to the command history.
        /// </summary>
        [ImpulseHandler]
        void SubmitCommand(string command);

        /// <summary>
        /// SetRootDirectory is the impulse used to set the root directory, which is recovered when the app is restarted.
        /// </summary>
        [ImpulseHandler]
        void SetRootDirectory(string newDirectory);

        /// <summary>
        /// SetRelativeDirectory is the impulse used to set the relative directory, which is recovered when the app is restarted.
        /// </summary>
        [ImpulseHandler]
        void SetRelativeDirectory(string newDirectory);

        /// <summary>
        /// AddConsoleOutput is the impulse used to add 
        /// </summary>
        [ImpulseHandler]
        void AddConsoleOutput(string outputToAdd);
    }
}

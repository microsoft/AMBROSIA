using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace XamarinCommandShell
{
    /// <summary>
    /// IOSCommands defines the interface which the GUI uses to spawn the actual execution of commands in whatever operating system
    /// we are currently running.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="workingDirectory">The fully qualified path (concatenation of root and relative directories) in which 
    /// the command should execute</param>
    /// <param name="commandOutputWriter">The text writer to send the execution output to.</param>
    public interface IOSCommands
    {
        void ExecuteCommand(string command,
                            string workingDirectory,
                            TextWriter commandOutputWriter);
    }
}

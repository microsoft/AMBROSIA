using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace XamarinCommandShell
{
    public interface IOSCommands
    {
        void ExecuteCommand(string command,
                            TextWriter commandOutputWriter);
    }
}

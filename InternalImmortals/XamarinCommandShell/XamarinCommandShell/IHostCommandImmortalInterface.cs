using System;
using System.Collections.Generic;
using System.Text;

namespace XamarinCommandShell
{
    public interface IHostCommandImmortalInterface
    {
        void HostSubmitCommand(string command);

        void HostSetCurrentDirectory(string newDirectory);

        string HostGetCurrentDirectory();

        string HostGetConsoleOutput();
    }
}

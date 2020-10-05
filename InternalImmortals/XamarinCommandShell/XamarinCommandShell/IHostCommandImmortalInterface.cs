using System;
using System.Collections.Generic;
using System.Text;
using Xamarin.Forms;

namespace XamarinCommandShell
{
    public interface IHostCommandImmortalInterface
    {
        void HostSubmitCommand(string command);

        void HostAddConsoleOutput(string output);

        void HostSetRootDirectory(string newDirectory);

        string HostGetRootDirectory();

        void HostSetRelativeDirectory(string newDirectory);

        string HostGetRelativeDirectory();

        string HostGetConsoleOutput();
    }
}

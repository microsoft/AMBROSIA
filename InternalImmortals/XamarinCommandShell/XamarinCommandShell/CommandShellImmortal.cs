using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Ambrosia;
using Xamarin.Forms;

namespace XamarinCommandShell
{
    [DataContract]
    public class CommandShellImmortal : Immortal<ICommandShellImmortalProxy>, ICommandShellImmortal, IHostCommandImmortalInterface
    {
        [DataMember]
        List<string> _commandHistory = null;
        [DataMember]
        string _outputHistory = "";
        [DataMember]
        string _currentDirectory = "";

        //        Editor _outputWindowContents;

        volatile static public IHostCommandImmortalInterface myCommandShellImmortal = null;

        public CommandShellImmortal()
        {
        }

        protected override async Task<bool> OnFirstStart()
        {
            _commandHistory = new List<string>();
            return true;
        }

        protected override void BecomingPrimary()
        {
            myCommandShellImmortal = this;
        }

        public async Task SubmitCommandAsync(string command)
        {
            _commandHistory.Add(command);
            _outputHistory += command + "\n";
        }

        public async Task AddConsoleOutputAsync(string outputToAdd)
        {
            _outputHistory += outputToAdd;
        }

        public async Task SetCurrentDirectoryAsync(string newDirectory)
        {
            _currentDirectory = newDirectory;
        }

        public void HostSubmitCommand(string command)
        {
            thisProxy.SubmitCommandFork(command);
        }

        public void HostSetCurrentDirectory(string newDirectory)
        {
            thisProxy.SetCurrentDirectoryFork(newDirectory);
        }

        public string HostGetCurrentDirectory()
        {
            return _currentDirectory;
        }
        public string HostGetConsoleOutput()
        {
            return _outputHistory;
        }
        public void HostAddConsoleOutput(string outputToAdd)
        {
            thisProxy.AddConsoleOutputFork(outputToAdd);
        }
    }
}

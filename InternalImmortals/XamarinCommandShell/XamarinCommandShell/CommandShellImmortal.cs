using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Ambrosia;

namespace XamarinCommandShell
{
    [DataContract]
    public class CommandShellImmortal : Immortal<ICommandShellImmortalProxy>, ICommandShellImmortal, IHostCommandImmortalInterface
    {
        // Protected state
        [DataMember]
        List<string> _commandHistory = null;

        [DataMember]
        string _rootDirectory = "";

        [DataMember]
        string _relativeDirectory = "";

        [DataMember]
        string _outputHistory;

        public CommandShellImmortal()
        {
        }

        protected override async Task<bool> OnFirstStart()
        {
            _commandHistory = new List<string>();
            _outputHistory = "";
            // Can let the UI use this now that the Immortal has been properly initialized
            myCommandShellImmortal = this;
            return true;
        }

        protected override void BecomingPrimary()
        {
            if (_outputHistory != null)
            {
                // The immortal was properly initialized already. Go ahead and let the UI use it.
                myCommandShellImmortal = this;
            }
        }

        public async Task SubmitCommandAsync(string command)
        {
            _commandHistory.Add(command);
            _outputHistory += "\n_______________________________________________________________________________________________________________________________________________________\n" +
                              ">" + _relativeDirectory + command +
                              "\n_______________________________________________________________________________________________________________________________________________________\n";
        }

        public async Task AddConsoleOutputAsync(string outputToAdd)
        {
            _outputHistory += outputToAdd;
        }

        public async Task SetRootDirectoryAsync(string newDirectory)
        {
            _rootDirectory = newDirectory;
        }

        public async Task SetRelativeDirectoryAsync(string newRelativeDirectory)
        {
            _relativeDirectory = newRelativeDirectory;
        }

        volatile static public IHostCommandImmortalInterface myCommandShellImmortal = null;

        public void HostSubmitCommand(string command)
        {
            thisProxy.SubmitCommandFork(command);
        }

        public void HostSetRootDirectory(string newDirectory)
        {
            thisProxy.SetRootDirectoryFork(newDirectory);
        }

        public void HostSetRelativeDirectory(string newRelativeDirectory)
        {
            thisProxy.SetRelativeDirectoryFork(newRelativeDirectory);
        }

        public string HostGetRootDirectory()
        {
            return _rootDirectory;
        }
        public string HostGetRelativeDirectory()
        {
            return _relativeDirectory;
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

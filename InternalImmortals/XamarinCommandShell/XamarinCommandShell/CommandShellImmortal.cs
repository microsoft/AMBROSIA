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
    /// <summary>
    /// CommandShellImmortal defines the Immortal class which contains all protected state and logic which is potentially replayed on 
    /// recovery/migration. State is protected by labeling a member with the attribute "DataMember", which makes it part of the serialized
    /// state of any object of this type.
    /// </summary>
    [DataContract]
    public class CommandShellImmortal : Immortal<ICommandShellImmortalProxy>, ICommandShellImmortal, IHostCommandImmortalInterface
    {
        // Protected state
        /// <summary>
        /// _commandHistory is a list of commands which have been submitted for execution.
        /// </summary>
        [DataMember]
        List<string> _commandHistory = null;

        /// <summary>
        /// _rootDirectory is a changeable directory which is the root of all activity. This is useful in situations where commands are
        /// issued relative to directory structures which are rooted in different places, but where the relative directory structure is
        /// the same. For instance, Github repos which are checked out in multiple places on the same machine, or on different machines.
        /// </summary>
        [DataMember]
        string _rootDirectory = "";

        /// <summary>
        /// _relativeDirectory is a changeable directory which is the current directory, relative to the root directory.
        /// </summary>
        [DataMember]
        string _relativeDirectory = "";

        /// <summary>
        /// _outputHistory contains the output from the execution of previously submitted commands
        /// </summary>
        [DataMember]
        string _outputHistory = "";

        /// <summary>
        /// _currentCommandHistoryIndex is the index of the currently scrolled command in the _commandHistory member.
        /// </summary>
        [DataMember]
        int _currentCommandHistoryIndex = -1;


        /// <summary>
        /// Physical constructor
        /// </summary>
        public CommandShellImmortal()
        {
        }

        // This member is set to this, after it has been fully recovered, as a way of allowing the UI
        // access to it once it's ready.
        volatile static public IHostCommandImmortalInterface myCommandShellImmortal = null;

        /// <summary>
        /// OnFirstStart is the logical constructor for CommandShellImmortal, which means that if we 
        /// recover from an initial state, we first re-execute this code as part of recovery.
        /// </summary>
        protected override async Task<bool> OnFirstStart()
        {
            _commandHistory = new List<string>();
            // Can let the UI use this now that the Immortal has been properly initialized
            myCommandShellImmortal = this;
            return true;
        }

        /// <summary>
        /// BecomingPrimary is called after replay, as part of exiting recovery. Note that if recovery happens
        /// from the initial state, OnFirstStart may not have been called yet.
        /// </summary>
        protected override void BecomingPrimary()
        {

            if (_outputHistory != null)
            {
                /*
                // Initialize the local index in the command history
                _currentCommandHistoryIndex = -1;
                if (_commandHistory.Count > 0)
                {
                    _currentCommandHistoryIndex = _commandHistory.Count;
                }*/

                // The immortal was properly initialized already. Go ahead and let the UI use it.
                myCommandShellImmortal = this;
            }
        }

        /// <summary>
        /// Impulse which submits a command to the command shell state. This includes updating both the command 
        /// history, and the output history. As a side effect, it also resets the current command index to the end, 
        /// which means a "new" command.
        /// </summary>
        public async Task SubmitCommandAsync(string command)
        {
            _commandHistory.Add(command);
            _outputHistory += "\n_______________________________________________________________________________________________________________________________________________________\n" +
                              ">" + _relativeDirectory + command +
                              "\n_______________________________________________________________________________________________________________________________________________________\n";
            
            // Reinitialize the index in the command history when a new command has been entered
            _currentCommandHistoryIndex = _commandHistory.Count;
        }

        /// <summary>
        /// Impulse which adds output to the console output history, typically as a consequence of executing a command
        /// </summary>
        public async Task AddConsoleOutputAsync(string outputToAdd)
        {
            _outputHistory += outputToAdd;
        }

        /// <summary>
        /// Impulse which sets the root directory
        /// </summary>
        public async Task SetRootDirectoryAsync(string newDirectory)
        {
            _rootDirectory = newDirectory;
        }

        /// <summary>
        /// Impulse which sets the relative directory
        /// </summary>
        public async Task SetRelativeDirectoryAsync(string newRelativeDirectory)
        {
            _relativeDirectory = newRelativeDirectory;
        }

        // The rest of the methods are the implementation of IHostCommandImmortalInterface, which is the interface that
        // the GUI uses to interact with the immortal.

        /// <summary>
        /// Host command (callable througth IHostCommandImmortalInterface), which calls the appropriate impulse 
        /// USING OUR OWN PROXY, which ensures that the call is logged.
        /// </summary>
        public void HostSubmitCommand(string command)
        {
            thisProxy.SubmitCommandFork(command);
        }

        /// <summary>
        /// Host command (callable througth IHostCommandImmortalInterface), which calls the appropriate impulse
        /// USING OUR OWN PROXY, which ensures that the call is logged.
        /// </summary>
        public void HostSetRootDirectory(string newDirectory)
        {
            thisProxy.SetRootDirectoryFork(newDirectory);
        }

        /// <summary>
        /// Host command (callable througth IHostCommandImmortalInterface), which calls the appropriate impulse
        /// USING OUR OWN PROXY, which ensures that the call is logged.
        /// </summary>
        public void HostSetRelativeDirectory(string newRelativeDirectory)
        {
            thisProxy.SetRelativeDirectoryFork(newRelativeDirectory);
        }

        /// <summary>
        /// Host command (callable througth IHostCommandImmortalInterface), which calls the appropriate impulse
        /// USING OUR OWN PROXY, which ensures that the call is logged.
        /// </summary>
        public void HostAddConsoleOutput(string outputToAdd)
        {
            thisProxy.AddConsoleOutputFork(outputToAdd);
        }

        /// <summary>
        /// Host command (callable througth IHostCommandImmortalInterface), which returns 
        /// </summary>
        public string HostRootDirectory
        {
            get => _rootDirectory;
        }

        /// <summary>
        /// Host command (callable througth IHostCommandImmortalInterface), which calls the appropriate impulse 
        /// </summary>
        public string HostRelativeDirectory
        {
            get => _relativeDirectory;
        }

        /// <summary>
        /// Property which returns the console output. Note that returning this is safe, since the state of the
        /// immortal can't be changed with the return value.
        /// </summary>
        public string HostConsoleOutput
        {
            get => _outputHistory;
        }

        /// <summary>
        /// Property which returns the previous command. Note that returning this is safe, since the state of the
        /// immortal can't be changed with the return value.
        /// </summary>
        public string HostPreviousCommand
        {
            get
            {
                string returnedCommand = "";

                if (_commandHistory.Count > 0)
                {
                    if (_currentCommandHistoryIndex > 0) _currentCommandHistoryIndex--;
                    returnedCommand = _commandHistory[_currentCommandHistoryIndex];
                }

                return returnedCommand;
            }
        }

        /// <summary>
        /// Property which returns the next command. Note that returning this is safe, since the state of the
        /// immortal can't be changed with the return value.
        /// </summary>
        public string HostNextCommand
        {
            get
            {
                string returnedCommand = "";

                if (_commandHistory.Count > 0)
                {
                    if (_currentCommandHistoryIndex < _commandHistory.Count - 1) _currentCommandHistoryIndex++;
                    returnedCommand = _commandHistory[_currentCommandHistoryIndex];
                }

                return returnedCommand;
            }
        }
    }
}

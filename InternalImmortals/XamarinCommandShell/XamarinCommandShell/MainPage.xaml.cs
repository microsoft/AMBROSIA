using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using Ambrosia;
using System.Threading;
using System.IO;
using Microsoft.VisualStudio.Threading;

namespace XamarinCommandShell
{
    public partial class MainPage : ContentPage
    {
        public class TextOutputAccumulator : TextWriter
        {
            private MainPage _myMainPage;

            public TextOutputAccumulator(MainPage myMainPage)
            {
                _myMainPage = myMainPage;
            }

            public override void Write(char value)
            {
                lock (_myMainPage._myAccumulatedOutput)
                {
                    _myMainPage._myAccumulatedOutput += value;
                }
            }

            public override void Write(string value)
            {
                lock (_myMainPage._myAccumulatedOutput)
                {
                    _myMainPage._myAccumulatedOutput += value;
                }
                if (_myMainPage._refreshQueue.IsEmpty)
                {
                    _myMainPage._refreshQueue.Enqueue(true);
                }
            }

            public override Encoding Encoding
            {
                get { return Encoding.Unicode; }
            }
        }

        // OS specific code for executing commands
        IOSCommands _commandExecuter;

        // Manages batched writing to the Xamarin output scroller for good Xamarin performance
        TextOutputAccumulator _commandOutputWriter;

        internal String _myAccumulatedOutput;
        internal AsyncQueue<bool> _refreshQueue;

        async Task CheckRefreshQueueAsync()
        {
            while (true)
            {
                await _refreshQueue.DequeueAsync();
                lock (_myAccumulatedOutput)
                {
                    consoleOutput.Dispatcher.BeginInvokeOnMainThread(() =>
                    {
                        // Add the output to the recoverable state
                        CommandShellImmortal.myCommandShellImmortal.HostAddConsoleOutput(_myAccumulatedOutput);
                        consoleOutput.Text += _myAccumulatedOutput;
                        _myAccumulatedOutput = "";
                        outputScroller.Dispatcher.BeginInvokeOnMainThread(() => { outputScroller.ScrollToAsync(0, consoleOutput.Bounds.Bottom, false); });
                    });
                }
            }
        }

        public MainPage(IOSCommands inCommands)
        {
            _commandExecuter = inCommands;
            InitializeComponent();
            // Initialize the on-screen homeDirectory to the potentially recovered contents
            homeDirectory.Dispatcher.BeginInvokeOnMainThread(() => { homeDirectory.Text = CommandShellImmortal.myCommandShellImmortal.HostRootDirectory; });
            // Initialize the on-screen relativeDirectory to the potentially recovered contents
            homeDirectory.Dispatcher.BeginInvokeOnMainThread(() => { relativeDirectory.Text = CommandShellImmortal.myCommandShellImmortal.HostRelativeDirectory; });
            _myAccumulatedOutput = "";
            // Initialize the on-screen contents to the potentially recovered contents
            consoleOutput.Text = CommandShellImmortal.myCommandShellImmortal.HostConsoleOutput;
            _commandOutputWriter = new TextOutputAccumulator(this);
            _refreshQueue = new AsyncQueue<bool>();
            CheckRefreshQueueAsync();           
        }

        void Command_Completed(object sender, EventArgs e)
        {
            // Submit the command to the recoverable state
            CommandShellImmortal.myCommandShellImmortal.HostSubmitCommand(((Entry)sender).Text);

            var splitCommand = ((Entry)sender).Text.Split(' ');
            if (splitCommand[0].ToLower() == "cd")
            {
                // We are changing the current directory
                string newRelativeDirectory;
                if (splitCommand[1] == "..")
                {
                    var splitPath = relativeDirectory.Text.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                    newRelativeDirectory = "";
                    for (int i = 0; i < splitPath.Length - 1; i++)
                    {
                        newRelativeDirectory += splitPath[i] + "\\";
                    }
                }
                else if (splitCommand[1].StartsWith("\\"))
                {
                    newRelativeDirectory = splitCommand[1];
                }
                else
                {
                    newRelativeDirectory = relativeDirectory.Text + splitCommand[1];
                }
                if (!newRelativeDirectory.EndsWith("\\"))
                {
                    newRelativeDirectory += '\\';
                }
                relativeDirectory.Text = newRelativeDirectory;
                // Make the new relative directory recoverable
                CommandShellImmortal.myCommandShellImmortal.HostSetRelativeDirectory(newRelativeDirectory);
                command.Text = "";
            }
            else
            {
                consoleOutput.Dispatcher.BeginInvokeOnMainThread(() =>
                {
                    consoleOutput.Text += "\n_______________________________________________________________________________________________________________________________________________________\n" +
                                          relativeDirectory.Text + ">" + ((Entry)sender).Text +
                                          "\n_______________________________________________________________________________________________________________________________________________________\n";
                    command.Text = "";
                    outputScroller.Dispatcher.BeginInvokeOnMainThread(() => { outputScroller.ScrollToAsync(0, consoleOutput.Bounds.Bottom, false); });
                });
                _commandExecuter.ExecuteCommand(((Entry)sender).Text, homeDirectory.Text + relativeDirectory.Text, _commandOutputWriter);
            }
        }

        void relativeDirectory_Completed(object sender, EventArgs e)
        {
            // Set the relative directory in the recoverable part of the state
            CommandShellImmortal.myCommandShellImmortal.HostSetRelativeDirectory(((Entry)sender).Text);
        }

        void homeDirectory_Completed(object sender, EventArgs e)
        {
            // Set the root directory in the recoverable part of the state
            CommandShellImmortal.myCommandShellImmortal.HostSetRootDirectory(((Entry)sender).Text);
        }

        private void btnPreviousCmd_Clicked(object sender, EventArgs e)
        {
            // Get the previous command from the recoverable state
             command.Text = CommandShellImmortal.myCommandShellImmortal.HostPreviousCommand;
        }

        private void btnNextCmd_Clicked(object sender, EventArgs e)
        {
            // Get the next command from the recoverable state
            command.Text = CommandShellImmortal.myCommandShellImmortal.HostNextCommand;
        }
    }
}

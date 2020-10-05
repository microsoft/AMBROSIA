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

        IOSCommands _commandExecuter;
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
                        CommandShellImmortal.myCommandShellImmortal.HostAddConsoleOutput(_myAccumulatedOutput);
                        consoleOutput.Text += _myAccumulatedOutput;
                        _myAccumulatedOutput = "";
                        outputScroller.Dispatcher.BeginInvokeOnMainThread(() => { outputScroller.ScrollToAsync(0, consoleOutput.Bounds.Bottom, true); });
                    });
                }
            }
        }

        public MainPage(IOSCommands inCommands)
        {
            _commandExecuter = inCommands;
            InitializeComponent();
            homeDirectory.Dispatcher.BeginInvokeOnMainThread(() => { homeDirectory.Text = CommandShellImmortal.myCommandShellImmortal.HostGetRootDirectory(); });
            _myAccumulatedOutput = "";
            consoleOutput.Text = CommandShellImmortal.myCommandShellImmortal.HostGetConsoleOutput();
             _commandOutputWriter = new TextOutputAccumulator(this);
            _refreshQueue = new AsyncQueue<bool>();
            CheckRefreshQueueAsync();
        }

        void Command_Completed(object sender, EventArgs e)
        {
            CommandShellImmortal.myCommandShellImmortal.HostSubmitCommand(relativeDirectory.Text + ((Entry)sender).Text);
            consoleOutput.Dispatcher.BeginInvokeOnMainThread(() =>
            {
                consoleOutput.Text += "\n_______________________________________________________________________________________________________________________________________________________\n" +
                                      relativeDirectory.Text + ">" + ((Entry)sender).Text +
                                      "\n_______________________________________________________________________________________________________________________________________________________\n";
                command.Text = "";
                outputScroller.Dispatcher.BeginInvokeOnMainThread(() => { outputScroller.ScrollToAsync(0, consoleOutput.Bounds.Bottom, true); });
            });
            _commandExecuter.ExecuteCommand(((Entry)sender).Text, homeDirectory.Text + relativeDirectory.Text, _commandOutputWriter);
        }

        void relativeDirectory_Completed(object sender, EventArgs e)
        {
        }

        void homeDirectory_Completed(object sender, EventArgs e)
        {
            CommandShellImmortal.myCommandShellImmortal.HostSetRootDirectory(((Entry)sender).Text);
        }
    }
}

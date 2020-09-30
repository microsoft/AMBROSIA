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

namespace XamarinCommandShell
{
    public partial class MainPage : ContentPage
    {
        public class CommandImmortalLinkedScrolledLabelWriter : TextWriter
        {
            // The control where we will write text.
            private Label _myConsoleLabel;
            private ScrollView _myConsoleScroller;
            private IHostCommandImmortalInterface _myCommandImmortal;

            public CommandImmortalLinkedScrolledLabelWriter(Label inConsoleLabel,
                                                            IHostCommandImmortalInterface myImmortal,
                                                            ScrollView myConsoleScroller)
            {
                _myConsoleLabel = inConsoleLabel;
                _myConsoleScroller = myConsoleScroller;
                _myCommandImmortal = myImmortal;
            }

            public override void Write(char value)
            {
                _myCommandImmortal.HostAddConsoleOutput(value.ToString());
                _myConsoleLabel.Dispatcher.BeginInvokeOnMainThread(() =>
                    {
                        _myConsoleLabel.Text += value;
                        _myConsoleScroller.Dispatcher.BeginInvokeOnMainThread(() => { _myConsoleScroller.ScrollToAsync(0, _myConsoleLabel.Bounds.Bottom, false); });
                    });
            }

            public override void Write(string value)
            {
                _myCommandImmortal.HostAddConsoleOutput(value);
                _myConsoleLabel.Dispatcher.BeginInvokeOnMainThread(() => 
                    { 
                        _myConsoleLabel.Text += value;
                        _myConsoleScroller.Dispatcher.BeginInvokeOnMainThread(() => { _myConsoleScroller.ScrollToAsync(0, _myConsoleLabel.Bounds.Bottom, false); });
                    });
            }

            public override Encoding Encoding
            {
                get { return Encoding.Unicode; }
            }
        }

        IOSCommands _commandExecuter;
        CommandImmortalLinkedScrolledLabelWriter _commandOutputWriter;

        public MainPage(IOSCommands inCommands)
        {
            _commandExecuter = inCommands;
            InitializeComponent();
            homeDirectory.Dispatcher.BeginInvokeOnMainThread(() => { homeDirectory.Text = CommandShellImmortal.myCommandShellImmortal.HostGetCurrentDirectory(); });
            _commandOutputWriter = new CommandImmortalLinkedScrolledLabelWriter(consoleOutput, CommandShellImmortal.myCommandShellImmortal, outputScroller);
            consoleOutput.Dispatcher.BeginInvokeOnMainThread(() => { consoleOutput.Text += CommandShellImmortal.myCommandShellImmortal.HostGetConsoleOutput(); });
        }

        void Command_Completed(object sender, EventArgs e)
        {
            consoleOutput.Dispatcher.BeginInvokeOnMainThread(() => { consoleOutput.Text += ((Entry)sender).Text + "\n"; });
            _commandExecuter.ExecuteCommand(homeDirectory.Text + ((Entry)sender).Text, _commandOutputWriter);
            CommandShellImmortal.myCommandShellImmortal.HostSubmitCommand(((Entry)sender).Text);
        }

        void homeDirectory_Completed(object sender, EventArgs e)
        {
            CommandShellImmortal.myCommandShellImmortal.HostSetCurrentDirectory(((Entry)sender).Text);
        }
    }
}

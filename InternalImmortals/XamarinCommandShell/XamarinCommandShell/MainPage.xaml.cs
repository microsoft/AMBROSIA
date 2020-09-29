using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using Ambrosia;
using System.Threading;

namespace XamarinCommandShell
{
    public partial class MainPage : ContentPage
    {
        IOSCommands _commandExecuter;

        public MainPage()
        {
            InitializeComponent();
            consoleOutput.Dispatcher.BeginInvokeOnMainThread(() => { consoleOutput.Text = CommandShellImmortal.myCommandShellImmortal.HostGetConsoleOutput(); });
            homeDirectory.Dispatcher.BeginInvokeOnMainThread(() => { homeDirectory.Text = CommandShellImmortal.myCommandShellImmortal.HostGetCurrentDirectory(); });
        }

        public MainPage(IOSCommands inCommands)
        {
            _commandExecuter = inCommands;
            InitializeComponent();
            consoleOutput.Dispatcher.BeginInvokeOnMainThread(() => { consoleOutput.Text = CommandShellImmortal.myCommandShellImmortal.HostGetConsoleOutput(); });
            homeDirectory.Dispatcher.BeginInvokeOnMainThread(() => { homeDirectory.Text = CommandShellImmortal.myCommandShellImmortal.HostGetCurrentDirectory(); });
        }

        void Command_Completed(object sender, EventArgs e)
        {
            consoleOutput.Dispatcher.BeginInvokeOnMainThread(() => { consoleOutput.Text += ((Entry)sender).Text + "\n"; });
            _commandExecuter.ExecuteCommand(homeDirectory.Text + ((Entry)sender).Text);
            CommandShellImmortal.myCommandShellImmortal.HostSubmitCommand(((Entry)sender).Text);
        }

        void homeDirectory_Completed(object sender, EventArgs e)
        {
            CommandShellImmortal.myCommandShellImmortal.HostSetCurrentDirectory(((Entry)sender).Text);
        }


    }
}

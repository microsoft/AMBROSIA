using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Ambrosia;
using System.Threading;
using System.Threading.Tasks;

namespace XamarinCommandShell
{
    public partial class App : Application
    {
        IDisposable _immortalHandle;

        public App()
        {
            AzureBlobsLogsInterface.SetToAzureBlobsLogs();
            new Thread(new ThreadStart(() => _immortalHandle = AmbrosiaFactory.Deploy<ICommandShellImmortal>("CommandShell", new CommandShellImmortal(), 2500))).Start();
            while (CommandShellImmortal.myCommandShellImmortal == null) ;
            InitializeComponent();
            MainPage = new MainPage();
        }

        public App(IOSCommands inCommands)
        {
            AzureBlobsLogsInterface.SetToAzureBlobsLogs();
            new Thread(new ThreadStart(() => _immortalHandle = AmbrosiaFactory.Deploy<ICommandShellImmortal>("CommandShell", new CommandShellImmortal(), 2500))).Start();
            while (CommandShellImmortal.myCommandShellImmortal == null) ;
            InitializeComponent();
            MainPage = new MainPage(inCommands);
        }

        protected override void OnStart()
        {
        }

        protected override void OnSleep()
        {
        }

        protected override void OnResume()
        {
        }
    }
}

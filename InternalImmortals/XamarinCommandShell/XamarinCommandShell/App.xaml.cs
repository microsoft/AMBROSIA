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

        public App(IOSCommands inCommands)
        {
//            GenericLogsInterface.SetToGenericLogs();
            // Store the logs in Azure by default. Comment the line below and uncomment the line above to store them in the file system.

            AzureBlobsLogsInterface.SetToAzureBlobsLogs();

            // Deploy the immortal on another thread as part of startup.
            new Thread(new ThreadStart(() => _immortalHandle = AmbrosiaFactory.Deploy<ICommandShellImmortal>("CommandShell", new CommandShellImmortal(), 2500))).Start();

            // Wait for the immortal to finish recovery
            while (CommandShellImmortal.myCommandShellImmortal == null) ;
            InitializeComponent();

            // Pass along the instantiated operating system specific part of the code to the main page initializer.
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

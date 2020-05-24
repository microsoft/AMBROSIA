using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace ICGUI
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(false)]
    public partial class MainPage : ContentPage
    {
        public class TextBoxWriter : TextWriter
        {
            // The control where we will write text.
            private Editor _myConsoleLabel;
            public TextBoxWriter(Editor inConsoleLabel)
            {
                _myConsoleLabel = inConsoleLabel;
            }

            public override void Write(char value)
            {
                _myConsoleLabel.Dispatcher.BeginInvokeOnMainThread(() => { _myConsoleLabel.Text += value; });
            }

            public override void Write(string value)
            {
                _myConsoleLabel.Dispatcher.BeginInvokeOnMainThread(() => { _myConsoleLabel.Text += value; });
            }

            public override Encoding Encoding
            {
                get { return Encoding.Unicode; }
            }
        }

        public MainPage()
        {
            InitializeComponent();
            TextBoxWriter writer = new TextBoxWriter(consoleOutput);
            Console.SetOut(writer);
        }

        static Thread _ambrosiaThread;
        static string myName;
        static string myPort;
        static string [] ambrosiaArgs;

        void OnStartIC(object sender, EventArgs e)
        {
            myName = icName.Text;
            myPort = portNum.Text;
            ambrosiaArgs = new string[2];
            ambrosiaArgs[0] = "-i=" + myName;
            ambrosiaArgs[1] = "-p=" + myPort;
            Console.WriteLine("ImmortalCoordinator -i=" + myName + " -p=" + myPort.ToString());
            _ambrosiaThread = new Thread(() => CRA.Worker.Program.main(ambrosiaArgs)) { IsBackground = true };
        }


    }
}

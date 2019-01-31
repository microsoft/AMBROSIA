using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Ambrosia;
using CRA.ClientLibrary;
using GraphicalImmortalAPI;
using GraphicalImmortalImpl;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace GraphicalAppUWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private static int CLIENT_1_RECEIVE_PORT = 1001;
        private static int CLIENT_1_SEND_PORT = 1000;
        private static string CLIENT_1_THIS_NAME = "uwptestclientA";
        private static string CLIENT_1_REMOTE_NAME = "uwptestclientB";
        private static int CLIENT_1_CRA_PORT = 1500;

        private static int CLIENT_2_RECEIVE_PORT = 2001;
        private static int CLIENT_2_SEND_PORT = 2000;
        private static string CLIENT_2_THIS_NAME = "uwptestclientB";
        private static string CLIENT_2_REMOTE_NAME = "uwptestclientA";
        private static int CLIENT_2_CRA_PORT = 2500;

        private class ImmortalThreadStartParams
        {
            public int _receivePort;
            public int _sendPort;
            public string _thisName;
            public string _remoteName;

            public ImmortalThreadStartParams(int receivePort, int sendPort, string thisName, string remoteName)
            {
                _receivePort = receivePort;
                _sendPort = sendPort;
                _thisName = thisName;
                _remoteName = remoteName;
            }
        }

        private class CRAWorkerThreadStartParams
        {
            public string _craInstanceName;
            public string _ambrosiaInstanceName;
            public string _ipAddress;
            public int _port;
            public string _storageConnectionString;
            public ISecureStreamConnectionDescriptor _descriptor;
            public int _streamsPoolSize;
            public string _thisServiceName;

            public CRAWorkerThreadStartParams(string craInstanceName, string ipAddress, int port, string storageConnectionString, ISecureStreamConnectionDescriptor descriptor, int streamsPoolSize, string ambrosiaInstanceName)
            {
                _craInstanceName = craInstanceName;
                _ipAddress = ipAddress;
                _port = port;
                _storageConnectionString = storageConnectionString;
                _descriptor = descriptor;
                _streamsPoolSize = streamsPoolSize;
                _ambrosiaInstanceName = ambrosiaInstanceName;
            }
        }

        private GraphicalImmortal immortal;
        private CRAWorker worker;
        private DispatcherTimer timer = new DispatcherTimer();

        private Point pointerPosInternal;
        private bool pointerDownInternal = false;

        public MainPage()
        {
            this.InitializeComponent();
            Console.SetOut(new DebugTextWriter());

            timer.Tick += Timer_Tick;
            timer.Interval = new TimeSpan(0, 0, 0, 0, 33);
            timer.Start();
        }

        private void Timer_Tick(object sender, object e)
        {
            if (immortal == null)
            {
                return;
            }

            if (immortal.DoneRecovering())
            {
                immortal.HandleUserInputExternal((int)pointerPosInternal.X, (int)pointerPosInternal.Y, pointerDownInternal);
            }

            canvas.Children.Clear();

            List<System.Drawing.Point> myPoints;
            List<System.Drawing.Point> otherPoints;
            immortal.GetAppStateExternal(out myPoints, out otherPoints);

            for (int i = 0; i < myPoints.Count() - 1; i++)
            {
                Line line = new Line();
                line.X1 = myPoints[i].X;
                line.Y1 = myPoints[i].Y;
                line.X2 = myPoints[i + 1].X;
                line.Y2 = myPoints[i + 1].Y;
                line.Stroke = new SolidColorBrush(Windows.UI.Colors.Black);
                canvas.Children.Add(line);
            }

            for (int i = 0; i < otherPoints.Count() - 1; i++)
            {
                Line line = new Line();
                line.X1 = otherPoints[i].X;
                line.Y1 = otherPoints[i].Y;
                line.X2 = otherPoints[i + 1].X;
                line.Y2 = otherPoints[i + 1].Y;
                line.Stroke = new SolidColorBrush(Windows.UI.Colors.Red);
                canvas.Children.Add(line);
            }
        }

        private void StartImmortal(int receivePort, int sendPort, string thisInstanceName, string remoteInstanceName)
        {
            ImmortalThreadStartParams threadParams = new ImmortalThreadStartParams(receivePort, sendPort, thisInstanceName, remoteInstanceName);
            Thread thread = new Thread(new ParameterizedThreadStart(RunImmortal));
            thread.IsBackground = true;
            thread.Start(threadParams);
        }

        private void RunImmortal(Object obj)
        {
            ImmortalThreadStartParams threadParams = (ImmortalThreadStartParams)obj;
            immortal = new GraphicalImmortal(threadParams._remoteName);
            using (var c = AmbrosiaFactory.Deploy<IGraphicalImmortal>(threadParams._thisName, immortal, threadParams._receivePort, threadParams._sendPort))
            {
                Thread.Sleep(14 * 24 * 3600 * 1000);
            }
        }

        private void StartCRAWorker(string craInstanceName, int port, string ambrosiaInstanceName)
        {
            string ipAddress = GetLocalIPAddress();
            string storageConnectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONN_STRING");
            int connectionsPoolPerWorker = 1000;

            CRAWorkerThreadStartParams threadParams = new CRAWorkerThreadStartParams(craInstanceName, ipAddress, port, storageConnectionString, null, connectionsPoolPerWorker, ambrosiaInstanceName);
            Thread thread = new Thread(new ParameterizedThreadStart(RunCRAWorker));
            thread.IsBackground = true;
            thread.Start(threadParams);
        }

        private void RunCRAWorker(Object obj)
        {
            CRAWorkerThreadStartParams threadParams = (CRAWorkerThreadStartParams)obj;
            worker = new CRAWorker(
                threadParams._craInstanceName,
                threadParams._ipAddress,
                threadParams._port,
                threadParams._storageConnectionString,
                threadParams._descriptor,
                threadParams._streamsPoolSize);
            worker.DisableDynamicLoading();
            worker.SideloadVertex(new AmbrosiaRuntime(), threadParams._ambrosiaInstanceName);
            worker.Start();
        }

        // Copied from Program.cs in CRA.Worker project
        private static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new InvalidOperationException("Local IP Address Not Found!");
        }

        private void canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint point = e.GetCurrentPoint(canvas);
            pointerPosInternal = point.Position;
        }

        private void canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            pointerDownInternal = true;
        }

        private void canvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            pointerDownInternal = false;
        }

        private void startButton_Click(object sender, RoutedEventArgs e)
        {
            string thisInstanceName = thisInstanceTextBox.Text;

            int craPort = int.Parse(craPortTextBox.Text);
            string replicaName = thisInstanceName + "0";
            StartCRAWorker(replicaName, craPort, thisInstanceName);

            int receivePort = int.Parse(receivePortTextBox.Text);
            int sendPort = int.Parse(sendPortTextBox.Text);
            string remoteInstanceName = remoteInstanceTextBox.Text;
            StartImmortal(receivePort, sendPort, thisInstanceName, remoteInstanceName);
        }

        private void client1PresetsButton_Click(object sender, RoutedEventArgs e)
        {
            receivePortTextBox.Text = CLIENT_1_RECEIVE_PORT.ToString();
            sendPortTextBox.Text = CLIENT_1_SEND_PORT.ToString();
            thisInstanceTextBox.Text = CLIENT_1_THIS_NAME;
            remoteInstanceTextBox.Text = CLIENT_1_REMOTE_NAME;
            craPortTextBox.Text = CLIENT_1_CRA_PORT.ToString();
        }

        private void client2PresetsButton_Click(object sender, RoutedEventArgs e)
        {
            receivePortTextBox.Text = CLIENT_2_RECEIVE_PORT.ToString();
            sendPortTextBox.Text = CLIENT_2_SEND_PORT.ToString();
            thisInstanceTextBox.Text = CLIENT_2_THIS_NAME;
            remoteInstanceTextBox.Text = CLIENT_2_REMOTE_NAME;
            craPortTextBox.Text = CLIENT_2_CRA_PORT.ToString();
        }
    }
}

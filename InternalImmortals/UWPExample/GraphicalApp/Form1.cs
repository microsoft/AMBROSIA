using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Ambrosia;
using CRA.ClientLibrary;
using GraphicalImmortalAPI;
using GraphicalImmortalImpl;

namespace GraphicalApp
{
    public partial class Form1 : Form
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
            public string _ipAddress;
            public int _port;
            public string _storageConnectionString;
            public ISecureStreamConnectionDescriptor _descriptor;
            public int _streamsPoolSize;

            public CRAWorkerThreadStartParams(string craInstanceName, string ipAddress, int port, string storageConnectionString, ISecureStreamConnectionDescriptor descriptor, int streamsPoolSize)
            {
                _craInstanceName = craInstanceName;
                _ipAddress = ipAddress;
                _port = port;
                _storageConnectionString = storageConnectionString;
                _descriptor = descriptor;
                _streamsPoolSize = streamsPoolSize;
            }
        }

        private System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private GraphicalImmortal immortal;

        // These variables are updated whenever Form1's mouse events fire. If
        // the Windows Forms API contained methods to directly read mouse data,
        // these variables would be unnecessary.
        private bool _mouseDownInternal = false;
        private Point _mousePosInternal;

        public Form1()
        {
            InitializeComponent();

            timer.Interval = 33;
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (immortal != null && immortal.DoneRecovering())
            {
                immortal.HandleUserInputExternal(_mousePosInternal.X, _mousePosInternal.Y, _mouseDownInternal);
            }
            Refresh();
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            if (immortal == null)
            {
                return;
            }

            List<Point> myPoints;
            List<Point> otherPoints;
            immortal.GetAppStateExternal(out myPoints, out otherPoints);
            for (int i = 0; i < myPoints.Count() - 1; i++)
            {
                e.Graphics.DrawLine(Pens.Black, myPoints[i].X, myPoints[i].Y, myPoints[i + 1].X, myPoints[i + 1].Y);
            }
            for (int i = 0; i < otherPoints.Count() - 1; i++)
            {
                e.Graphics.DrawLine(Pens.Red, otherPoints[i].X, otherPoints[i].Y, otherPoints[i + 1].X, otherPoints[i + 1].Y);
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

        private void StartCRAWorker(string craInstanceName, int port)
        {
            string ipAddress = GetLocalIPAddress();
            string storageConnectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONN_STRING");
            int connectionsPoolPerWorker = 1000;

            CRAWorkerThreadStartParams threadParams = new CRAWorkerThreadStartParams(craInstanceName, ipAddress, port, storageConnectionString, null, connectionsPoolPerWorker);
            Thread thread = new Thread(new ParameterizedThreadStart(RunCRAWorker));
            thread.IsBackground = true;
            thread.Start(threadParams);
        }

        private void RunCRAWorker(Object obj)
        {
            CRAWorkerThreadStartParams threadParams = (CRAWorkerThreadStartParams)obj;
            CRAWorker worker = new CRAWorker(
                threadParams._craInstanceName,
                threadParams._ipAddress,
                threadParams._port,
                threadParams._storageConnectionString,
                threadParams._descriptor,
                threadParams._streamsPoolSize);
            worker.DisableDynamicLoading();
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

        private void startButton_Click(object sender, EventArgs e)
        {
            string thisInstanceName = thisInstanceTextBox.Text;

            int craPort = int.Parse(craPortTextBox.Text);
            string replicaName = thisInstanceName + "0";
            StartCRAWorker(replicaName, craPort);

            int receivePort = int.Parse(receivePortTextBox.Text);
            int sendPort = int.Parse(sendPortTextBox.Text);
            string remoteInstanceName = remoteInstanceTextBox.Text;
            StartImmortal(receivePort, sendPort, thisInstanceName, remoteInstanceName);
        }

        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            _mouseDownInternal = true;
        }

        private void Form1_MouseUp(object sender, MouseEventArgs e)
        {
            _mouseDownInternal = false;
        }

        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {
            _mousePosInternal = PointToClient(Cursor.Position);
        }

        private void client1PresetsButton_Click(object sender, EventArgs e)
        {
            receivePortTextBox.Text = CLIENT_1_RECEIVE_PORT.ToString();
            sendPortTextBox.Text = CLIENT_1_SEND_PORT.ToString();
            thisInstanceTextBox.Text = CLIENT_1_THIS_NAME;
            remoteInstanceTextBox.Text = CLIENT_1_REMOTE_NAME;
            craPortTextBox.Text = CLIENT_1_CRA_PORT.ToString();
        }

        private void client2PresetsButton_Click(object sender, EventArgs e)
        {
            receivePortTextBox.Text = CLIENT_2_RECEIVE_PORT.ToString();
            sendPortTextBox.Text = CLIENT_2_SEND_PORT.ToString();
            thisInstanceTextBox.Text = CLIENT_2_THIS_NAME;
            remoteInstanceTextBox.Text = CLIENT_2_REMOTE_NAME;
            craPortTextBox.Text = CLIENT_2_CRA_PORT.ToString();
        }
    }
}

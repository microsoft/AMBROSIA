using System;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Ambrosia;
using Client2;
using Server;
using Microsoft.VisualStudio.Threading;
using System.IO;
using System.Diagnostics;

namespace Client2
{
    [DataContract]
    class Client2 : Immortal<IClient2Proxy>, IClient2
    {
        [DataMember]
        private string _serverName;

        [DataMember]
        private IServerProxy _server;

        public Client2(string serverName)
        {
            _serverName = serverName;
        }

        void InputLoop()
        {
            while (true)
            {
                Console.Write("Enter a message (hit ENTER to send, send an empty line to end): ");
                string input = Console.ReadLine();
                if (input == "")
                {
                    Program.finishedTokenQ.Enqueue(0);
                    return;
                }
                else
                {
                    Console.WriteLine("Sending keyboard input {0}", input);
                    thisProxy.ReceiveKeyboardInputFork(input);
                }
            }
        }

        protected override void BecomingPrimary()
        {
            Console.WriteLine("Finished initializing state/recovering");
            Thread timerThread = new Thread(InputLoop);
            timerThread.Start();
        }

        public async Task ReceiveKeyboardInputAsync(string input)
        {
            _server.ReceiveMessageFork(input);
        }

        protected override async Task<bool> OnFirstStart()
        {
            _server = GetProxy<IServerProxy>(_serverName);
            return true;
        }
    }

    class Program
    {
        public static AsyncQueue<int> finishedTokenQ;

        static void Main(string[] args)
        {
            finishedTokenQ = new AsyncQueue<int>();

            int coordinatorPort = 1500;
            string clientInstanceName = "client";
            string serverInstanceName = "server";

            if (args.Length >= 1)
            {
                clientInstanceName = args[0];
            }

            if (args.Length == 2)
            {
                serverInstanceName = args[1];
            }

            Client2 client = new Client2(serverInstanceName);
            using (var coordinatorOutput = new StreamWriter("CoordOut.txt", false))
            {
                var iCListener = new TextWriterTraceListener(coordinatorOutput);
                Trace.Listeners.Add(iCListener);
                GenericLogsInterface.SetToGenericLogs();
                using (AmbrosiaFactory.Deploy<IClient2>(clientInstanceName, client, coordinatorPort))
                {
                    finishedTokenQ.DequeueAsync().Wait();
                }
            }
        }
    }
}

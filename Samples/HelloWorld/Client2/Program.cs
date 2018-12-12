using System;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Ambrosia;
using Client2;
using Server;
using Microsoft.VisualStudio.Threading;

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
                Console.Write("Enter a message (hit ENTER to send): ");
                string input = Console.ReadLine();
                thisProxy.ReceiveKeyboardInputFork(input);
                Thread.Sleep(1000);
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
            Console.WriteLine("Sending keyboard input {0}", input);
            _server.ReceiveMessageFork(input);
        }

        public async Task SendMessageAsync(string message)
        {
            Console.WriteLine("Sending message to server: " + message);
            int numMessages = await _server.ReceiveMessageAsync(message);
            Console.WriteLine("Sent message to server! Server has received " + numMessages + " messages.");
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

            int receivePort = 1001;
            int sendPort = 1000;
            string clientInstanceName = "client1";
            string serverInstanceName = "server1";

            if (args.Length >= 1)
            {
                clientInstanceName = args[0];
            }

            if (args.Length == 2)
            {
                serverInstanceName = args[1];
            }

            Client2 client = new Client2(serverInstanceName);
            using (var c = AmbrosiaFactory.Deploy<IClient2>(clientInstanceName, client, receivePort, sendPort))
            {
                while (finishedTokenQ.IsEmpty)
                {
                    finishedTokenQ.DequeueAsync().Wait();
                }
            }
        }
    }
}

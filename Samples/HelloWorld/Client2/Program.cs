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
                Thread.Sleep(2000);
                Console.Write("Enter a message (hit ENTER to send): ");
                string input = Console.ReadLine();
                thisProxy.ReceiveKeyboardInputFork(input);
            }
        }

      /*  protected override void BecomingPrimary()
        {
            Console.WriteLine("Finished initializing state/recovering");
            Thread timerThread = new Thread(InputLoop);
            timerThread.Start();
        }*/

        public async Task ReceiveKeyboardInputAsync(string input)
        {
            Console.WriteLine("Sending keyboard input {0}", input);
            _server.ReceiveMessageFork(input);
        }

        protected override async Task<bool> OnFirstStart()
        {
            _server = GetProxy<IServerProxy>(_serverName);

            for (int i = 0; i < 1000; i++)
            {
                Console.WriteLine("Client 2: Hello World {0}", 2 * i + 1);
                _server.ReceiveMessageFork("Client 2: Hello World " + (2 * i + 1).ToString());
                Thread.Sleep(1000);
            }


            /*_server.ReceiveMessageFork("\n!! Client: Hello World 1!");

            using (ConsoleColorScope.SetForeground(ConsoleColor.Yellow))
            {
                Console.WriteLine("\n!! Client: Sent message 1.");
                Console.WriteLine("\n!! Client: Press enter to continue (will send 2&3)");
            }

            Console.ReadLine();
            _server.ReceiveMessageFork("\n!! Client: Hello World 2!");
            _server.ReceiveMessageFork("\n!! Client: Hello World 3!");

            using (ConsoleColorScope.SetForeground(ConsoleColor.Yellow))
            {
                Console.WriteLine("\n!! Client: Press enter to shutdown.");
            }*/

            Console.ReadLine();
            Program.finishedTokenQ.Enqueue(0);
            return true;
        }
    }

    class Program
    {
        public static AsyncQueue<int> finishedTokenQ;

        static void Main(string[] args)
        {
            finishedTokenQ = new AsyncQueue<int>();

            int receivePort = 3001;
            int sendPort = 3000;
            string clientInstanceName = "client2";
            string serverInstanceName = "server-2";

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

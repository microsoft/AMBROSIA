using Ambrosia;
using Client1;
using Server;
using Microsoft.VisualStudio.Threading;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Threading;

namespace Client1
{
    class ConsoleColorScope : IDisposable
    {
        public static ConsoleColorScope SetForeground(ConsoleColor color)
        {
            Console.ForegroundColor = color;
            return new ConsoleColorScope();
        }

        public void Dispose()
        {
            Console.ResetColor();
        }
    }

    [DataContract]
    class Client1 : Immortal<IClient1Proxy>, IClient1
    {
        [DataMember]
        private string _serverName;

        [DataMember]
        private IServerProxy _server;

        public Client1(string serverName)
        {
            _serverName = serverName;
        }

        protected override async Task<bool> OnFirstStart()
        {
            _server = GetProxy<IServerProxy>(_serverName);

            for (int i = 0; i < 1000; i++)
            {
                Console.WriteLine("Client 1: Hello World {0}", 2*i);
                _server.ReceiveMessageFork("Client 1: Hello World " + (2*i).ToString());
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

            int receivePort = 1001;
            int sendPort = 1000;
            string clientInstanceName = "client1";
            string serverInstanceName = "server-1";

            if (args.Length >= 1)
            {
                clientInstanceName = args[0];
            }

            if (args.Length == 2)
            {
                serverInstanceName = args[1];
            }

            using (var c = AmbrosiaFactory.Deploy<IClient1>(clientInstanceName, new Client1(serverInstanceName), receivePort, sendPort))
            {
                finishedTokenQ.DequeueAsync().Wait();
            }
        }
    }
}

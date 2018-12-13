using Ambrosia;
using Client1;
using Server;
using Microsoft.VisualStudio.Threading;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Client1
{
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


            _server.ReceiveMessageFork("\n!! Client: Hello World 1!");

	    Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n!! Client: Sent message 1.");
            Console.WriteLine("\n!! Client: Press enter to continue (will send 2&3)");
	    Console.ResetColor();
	                
	    Console.ReadLine(); // Console.ReadKey();
            _server.ReceiveMessageFork("\n!! Client: Hello World 2!");
            _server.ReceiveMessageFork("\n!! Client: Hello World 3!");

	    Console.ForegroundColor = ConsoleColor.Yellow; 	    
            Console.WriteLine("\n!! Client: Press enter to shutdown.");

            Console.ReadLine(); // Console.ReadKey();
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

            using (var c = AmbrosiaFactory.Deploy<IClient1>(clientInstanceName, new Client1(serverInstanceName), receivePort, sendPort))
            {
                finishedTokenQ.DequeueAsync().Wait();
            }
        }
    }
}

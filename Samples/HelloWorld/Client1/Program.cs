using Ambrosia;
using Client1;
using Server;
using Microsoft.VisualStudio.Threading;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.IO;

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


            _server.ReceiveMessageFork("\n!! Client: Hello World 1!");

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
            }

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

            using (var coordinatorOutput = new StreamWriter("CoordOut.txt", false))
            {
                GenericLogsInterface.SetToGenericLogs();
                using (AmbrosiaFactory.Deploy<IClient1>(clientInstanceName, new Client1(serverInstanceName), coordinatorPort))
                {
                    finishedTokenQ.DequeueAsync().Wait();
                }
            }
        }
    }
}

using Ambrosia;
using Server;
using Microsoft.VisualStudio.Threading;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.IO;

namespace Client3
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

    // Because this Immortal makes async instance calls, it, and the associated generated proxies and base classes, must be
    // compiled DEBUG. For an explanation, see HelloWorldExplained.
    [DataContract]
    class Client3 : Immortal<IClient3Proxy>, IClient3
    {
        [DataMember]
        private string _serverName;
        [DataMember]
        private string _myName;

        [DataMember]
        private IServerProxy _server;

        public Client3(string serverName,
                       string myName)
        {
            _serverName = serverName;
            _myName = myName;
        }

        protected override async Task<bool> OnFirstStart()
        {
            _server = GetProxy<IServerProxy>(_serverName);
            _server.AddRespondeeFork(_myName);

            using (ConsoleColorScope.SetForeground(ConsoleColor.Yellow))
            {
                _server.ReceiveMessageFork("\n!! Client: Hello World 3 Message #1!");
                Console.WriteLine("\n!! Client: Sent message #1.");
            }
            return true;
        }

        public async Task ResponseFromServerAsync(int numMessages)
        {
            Console.WriteLine($"\n!! Client: Message #{numMessages} completed. Server acknowledges processing {numMessages} messages.");
            if (numMessages < 2)
            {
                _server.ReceiveMessageFork($"\n!! Client: Hello World 3 Message #{numMessages+1}!");
                Console.WriteLine($"\n!! Client: Sent message #{numMessages + 1}.");
            }
            else
            {
                Console.WriteLine("\n!! Client: Shutting down");
                Program.finishedTokenQ.Enqueue(0);
            }
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
                using (AmbrosiaFactory.Deploy<IClient3>(clientInstanceName, new Client3(serverInstanceName, clientInstanceName), coordinatorPort))
                {
                    finishedTokenQ.DequeueAsync().Wait();
                }
            }
        }
    }
}

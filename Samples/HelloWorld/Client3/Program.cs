using Ambrosia;
using Server;
using Microsoft.VisualStudio.Threading;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Client3
{
    [DataContract]
    class Client3 : Immortal<IClient3Proxy>, IClient3
    {
        [DataMember]
        private string _serverName;

        [DataMember]
        private IServerProxy _server;

        public Client3(string serverName)
        {
            _serverName = serverName;
        }

        protected override async Task<bool> OnFirstStart()
        {
            _server = GetProxy<IServerProxy>(_serverName);

            var t1 = _server.ReceiveMessageAsync("\n!! Client: Hello World 3 Message #1!");
            var t2 = _server.ReceiveMessageAsync("\n!! Client: Hello World 3 Message #2!");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n!! Client: Sent messages 1 & 2.");

            Console.WriteLine("\n!! Client: Press enter to continue (will await message 2)");
            Console.ResetColor();

            Console.ReadLine(); // Console.ReadKey();

            var res2 = await t2;
            Console.WriteLine($"\n!! Client: Message 2 completed. Server acknowledges processing {res2} messages.");

            Console.WriteLine("\n!! Client: Press enter to continue (will await message 1)");

            var res1 = await t1;
            Console.WriteLine($"\n!! Client: Message 1 completed. Server acknowledges processing {res1} messages.");

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

            using (var c = AmbrosiaFactory.Deploy<IClient3>(clientInstanceName, new Client3(serverInstanceName), receivePort, sendPort))
            {
                finishedTokenQ.DequeueAsync().Wait();
            }
        }
    }
}

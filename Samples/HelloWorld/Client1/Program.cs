using Ambrosia;
using IClient;
using IServer;
using Microsoft.VisualStudio.Threading;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Client1
{
    [DataContract]
    class Client1 : Immortal<IClientProxy>, IClient.IClient
    {
        [DataMember]
        private string _serverName;

        [DataMember]
        private IServerProxy _server;

        public Client1(string serverName)
        {
            _serverName = serverName;
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
            await thisProxy.SendMessageAsync("Hello world!");
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
            string serverInstanceName = "server1";

            using (var c = AmbrosiaFactory.Deploy<IClient.IClient>(clientInstanceName, new Client1(serverInstanceName), receivePort, sendPort))
            {
                finishedTokenQ.DequeueAsync().Wait();
            }
        }
    }
}

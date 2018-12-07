using System;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Ambrosia;
using IClient2;
using IServer;
using Microsoft.VisualStudio.Threading;

namespace Client2
{
    [DataContract]
    class Client2 : Immortal<IClient2Proxy>, IClient2.IClient2
    {
        [DataMember]
        private string _serverName;

        [DataMember]
        private IServerProxy _server;

        public Client2(string serverName)
        {
            _serverName = serverName;
        }

        public void IngressKeyboardInput(string input)
        {
            thisProxy.ReceiveKeyboardInputFork(input);
        }

        public async Task ReceiveKeyboardInputAsync(string input)
        {
            await thisProxy.SendMessageAsync(input);
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
            string clientInstanceName = "client2";
            string serverInstanceName = "server1";

            Client2 client = new Client2(serverInstanceName);
            using (var c = AmbrosiaFactory.Deploy<IClient2.IClient2>(clientInstanceName, client, receivePort, sendPort))
            {
                while (finishedTokenQ.IsEmpty)
                {
                    Console.Write("Enter a message (hit ENTER to send): ");
                    string input = Console.ReadLine();
                    client.IngressKeyboardInput(input);
                    Thread.Sleep(1000);
                }
                finishedTokenQ.DequeueAsync().Wait();
            }
        }
    }
}

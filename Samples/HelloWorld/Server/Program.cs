using Ambrosia;
using IServer;
using Microsoft.VisualStudio.Threading;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Server
{
    class Program
    {
        public static AsyncQueue<int> finishedTokenQ;

        [DataContract]
        sealed class Server : Immortal<IServerProxy>, IServer.IServer
        {
            [DataMember]
            int _messagesReceived = 0;

            public Server()
            {
            }

            public async Task<int> ReceiveMessageAsync(string message)
            {
                _messagesReceived++;
                return _messagesReceived;
            }

            protected override async Task<bool> OnFirstStart()
            {
                return true;
            }
        }

        static void Main(string[] args)
        {
            int receivePort = 2001;
            int sendPort = 2000;
            string serviceName = "server1";

            using (var c = AmbrosiaFactory.Deploy<IServer.IServer>(serviceName, new Server(), receivePort, sendPort))
            {
                finishedTokenQ.DequeueAsync().Wait();
            }
        }
    }
}

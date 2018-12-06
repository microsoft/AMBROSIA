using Ambrosia;
using System;
using System.Threading.Tasks;

namespace Client1
{
    class Client1 : Immortal
    {
        public Client1()
        {
        }

        protected override async Task<bool> OnFirstStart()
        {
            return true;
        }
    }
    class Program
    {


        static void Main(string[] args)
        {
            int receivePort = 1001;
            int sendPort = 1000;
            string serviceName = "client1";

            using (var c = AmbrosiaFactory.Deploy<Empty>(serviceName, new Client1(), receivePort, sendPort))
            {
                finishedTokenQ.DequeueAsync().Wait();
            }
        }
    }
}

using Research.Franklin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TwitterDashboardAPI;
using TwitterObservable;
using static Research.Franklin.ServiceConfiguration;

namespace TwitterDashboard
{
    [DataContract]
    class TwitterDashboard : Zombie, ITwitterDashboard
    {
        public TwitterDashboard()
        {
        }

        protected override void EntryPoint()
        {
        }

        public void OnNext(AnalyticsResultString next)
        {
            Console.WriteLine("{0}", next.ToString());
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            int receivePort = 1001;
            int sendPort = 1000;

            if (args.Length == 2)
            {
                receivePort = int.Parse(args[0]);
                sendPort = int.Parse(args[1]);
            }

            Console.WriteLine("Pausing execution. Press enter to deploy and continue.");
            Console.ReadLine();

            var myClient = new TwitterDashboard();

            using (var c = FranklinFactory.Deploy<ITwitterDashboard>(DashboardServiceName, myClient, receivePort, sendPort))
            {
                // nothing to call on c, just doing this for calling Dispose.
                Console.WriteLine("Press enter to terminate program.");
                Console.ReadLine();
            }
        }
    }
}

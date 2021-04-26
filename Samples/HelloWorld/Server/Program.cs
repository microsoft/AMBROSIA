using Ambrosia;
using Client3;
using Server;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Server
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

    class Program
    {
        [DataContract]
        sealed class Server : Immortal<IServerProxy>, IServer
        {
            [DataMember]
            int _messagesReceived = 0;

            [DataMember]
            List<IClient3Proxy> _respondeeList;

            public Server()
            {
            }

            public async Task ReceiveMessageAsync(string message)
            {
                using (ConsoleColorScope.SetForeground(ConsoleColor.Green))
                {
                    Console.WriteLine("\n!! SERVER Received message from a client: " + message);
                }
                _messagesReceived++;
                foreach (var r in _respondeeList)
                {
                    r.ResponseFromServerFork(_messagesReceived);
                }
            }

            public async Task AddRespondeeAsync(string respondeeName)
            {
                var newRespondee = GetProxy<IClient3Proxy>(respondeeName);
                _respondeeList.Add(newRespondee);
            }

            protected override async Task<bool> OnFirstStart()
            {
                _respondeeList = new List<IClient3Proxy>();
                return true;
            }
        }

        enum ICExecutionStyle{
            TwoProc,
            InProc,
            TimeTravelDebugging
        }

        static void Main(string[] args)
        {
            int coordinatorPort = 2500;
            int receivePort = 2001;
            int sendPort = 2000;
            string serviceName = "server";

            if (args.Length >= 1)
            {
                serviceName = args[0];
            }
            var iCExecutionStyle = ICExecutionStyle.InProc;
            string logPath = null;
            int checkpointToLoad = 0;
            if (args.Length >= 2)
            {
                if (args[1].ToUpper().Equals("TTD"))
                {
                    iCExecutionStyle = ICExecutionStyle.TimeTravelDebugging;
                    logPath = args[2];
                    checkpointToLoad = int.Parse(args[3]);
                }
                else if (args[1].ToUpper().Equals("TWOPROC"))
                {
                    iCExecutionStyle = ICExecutionStyle.TwoProc;
                    if (args.Length >= 3)
                    {
                        receivePort = int.Parse(args[2]);
                    }
                    if (args.Length >= 4)
                    {
                        sendPort = int.Parse(args[3]);
                    }
                }
            }
            GenericLogsInterface.SetToGenericLogs();
            switch (iCExecutionStyle)
            {
                case ICExecutionStyle.InProc:
                    using (var coordinatorOutput = new StreamWriter("CoordOut.txt", false))
                    {
                        var iCListener = new TextWriterTraceListener(coordinatorOutput);
                        Trace.Listeners.Add(iCListener);
                        using (AmbrosiaFactory.Deploy<IServer>(serviceName, new Server(), coordinatorPort))
                        {
                            Thread.Sleep(14 * 24 * 3600 * 1000);
                        }
                    }
                    break;
                case ICExecutionStyle.TwoProc:
                    using (AmbrosiaFactory.Deploy<IServer>(serviceName, new Server(), receivePort, sendPort))
                    {
                        Thread.Sleep(14 * 24 * 3600 * 1000);
                    }
                    break;
                case ICExecutionStyle.TimeTravelDebugging:
                    using (var coordinatorOutput = new StreamWriter("CoordOut.txt", false))
                    {
                        var iCListener = new TextWriterTraceListener(coordinatorOutput);
                        Trace.Listeners.Add(iCListener);
                        using (AmbrosiaFactory.Deploy<IServer>(serviceName, new Server(), logPath, checkpointToLoad))
                        {
                            Thread.Sleep(14 * 24 * 3600 * 1000);
                        }
                    }
                    break;
            }
        }
    }
}

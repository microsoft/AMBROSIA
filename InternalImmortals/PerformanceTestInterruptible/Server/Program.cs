using JobAPI;
using Ambrosia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.VisualStudio.Threading;

namespace Server
{
    [DataContract]
    class Server : Immortal<IServerProxy>, IServer
    {
        [DataMember]
        internal long _bytesReceived = 0;
        [DataMember]
        internal IJobProxy[] _jobs;
        [DataMember]
        internal string _jobName = string.Empty;
        [DataMember]
        internal long _lastCallNum = 0;
        [DataMember]
        internal bool _isBidirectional;
        [DataMember]
        internal int _nCalls = 0;
        [DataMember]
        internal int _numJobs;
        [DataMember]
        internal int _bytesReceivedSoFar = 0;
        [DataMember]
        internal List<byte[]> _allocatedMemory;


        const long oneMB = ((long)1 * 1024 * 1024);
        const long oneGig = ((long)1 * 1024 * oneMB);
        const long fiveHundredMB = ((long)1 * 500 * oneMB);

        public Server(string jobName,
                      bool isBidirectional,
                      int numJobs,
                      long memoryUsed)
        {
            _jobName = jobName;
            _isBidirectional = isBidirectional;
            _jobs = new IJobProxy[numJobs];
            _numJobs = numJobs;
            if (memoryUsed > 0)
            {
                _allocatedMemory = new List<byte[]>();
                var buffer = new byte[memoryUsed % fiveHundredMB];
                _allocatedMemory.Add(buffer);
                for (int i = 0; i < memoryUsed / fiveHundredMB; i++)
                {
                    buffer = new byte[fiveHundredMB];
                    _allocatedMemory.Add(buffer);
                }
            }
            else
            {
                _allocatedMemory = null;
            }
        }

        protected override async Task<bool> OnFirstStart()
        {
            Console.WriteLine("*X* Server in Entry Point");
            _jobs = new IJobProxy[_numJobs];
            if (_numJobs > 1)
            {
                for (int i = 0; i < _numJobs; i++)
                {
                    _jobs[i] = GetProxy<IJobProxy>(_jobName + i.ToString());
                }
            }
            else
            {
                _jobs[0] = GetProxy<IJobProxy>(_jobName);
            }

            return true;
        }

        public async Task MAsync(byte[] arg)
        {
            _bytesReceived += arg.Length;
            if ((_bytesReceived - arg.Length) / oneGig != _bytesReceived / oneGig)
            {
                Console.WriteLine($"Received {_bytesReceived / oneMB} MB so far");
            }
            if (arg.Length >= 8)
            {
                var curCallNum = StreamCommunicator.ReadBufferedLong(arg, 0);
                if (_numJobs == 1 && _lastCallNum + 1 != curCallNum)
                {
                    Console.WriteLine("Out of order message. Expected {0}, got {1}", _lastCallNum + 1, curCallNum);
                }
                _lastCallNum = curCallNum;
            }
            if (_isBidirectional)
            {
                for (int i = 0; i < _numJobs; i++)
                {
                    _jobs[i].MFork(arg);
                }
            }
        }

        public async Task AmIHealthyAsync(DateTime currentTime)
        {
            _nCalls++;
            if (_nCalls % 3000 == 0)
            {
                Console.WriteLine("*X* I'm healthy after {0} checks at time:" + currentTime.ToString(), _nCalls);
            }
        }

        public async Task PrintBytesReceivedAsync()
        {
            _bytesReceivedSoFar++;
            if (_bytesReceivedSoFar == _numJobs)
            {
                Console.WriteLine("Bytes received: {0}", _bytesReceived);
                for (int i = 0; i < _numJobs; i++)
                {
                    _jobs[i].PrintBytesReceivedFork();
                }
                ServerBootstrapper.finishedTokenQ.Enqueue(0);
            }
        }

        void TimerLoop()
        {
            while (true)
            {
                Thread.Sleep(1);
                thisProxy.AmIHealthyFork(DateTime.Now);
            }
        }

        protected override void BecomingPrimary()
        {
            Console.WriteLine("*X* becoming primary");
            Thread timerThread = new Thread(TimerLoop);
            timerThread.Start();
        }

        protected override void OnSave(Stream stream)
        {
            Console.WriteLine("*X* At checkpoint, received {0} messages", _lastCallNum);
        }

    }


    [DataContract]
    class ServerUpgraded : Immortal<IServerProxy>, IServer
    {
        [DataMember]
        internal long _bytesReceived = 0;
        [DataMember]
        internal IJobProxy[] _jobs;
        [DataMember]
        internal string _jobName = string.Empty;
        [DataMember]
        internal long _lastCallNum = 0;
        [DataMember]
        internal bool _isBidirectional;
        [DataMember]
        internal int _nCalls = 0;
        [DataMember]
        internal int _numJobs;


        const long oneMB = ((long)1 * 1024 * 1024);
        const long oneGig = ((long)1 * 1024 * oneMB);
        const long oneHundredMB = ((long)1 * 100 * oneMB);

        public ServerUpgraded()
        {
        }

        public ServerUpgraded(Server copyFrom)
        {
            _bytesReceived = copyFrom._bytesReceived;
            _jobs = copyFrom._jobs;
            _jobName = copyFrom._jobName;
            _lastCallNum = copyFrom._lastCallNum;
            _isBidirectional = copyFrom._isBidirectional;
            _nCalls = copyFrom._nCalls;
            _numJobs = copyFrom._numJobs;
        }

        protected override async Task<bool> OnFirstStart()
        {
            Console.WriteLine("Server Upgraded in Entry Point");
            _jobs = new IJobProxy[_numJobs];
            if (_numJobs > 1)
            {
                for (int i = 0; i < _numJobs; i++)
                {
                    _jobs[i] = GetProxy<IJobProxy>(_jobName + i.ToString());
                }
            }
            else
            {
                _jobs[0] = GetProxy<IJobProxy>(_jobName);
            }

            return true;
        }

        public async Task MAsync(byte[] arg)
        {
            _bytesReceived += arg.Length;
            if ((_bytesReceived - arg.Length) / oneGig != _bytesReceived / oneGig)
            {
                Console.WriteLine($"Received {_bytesReceived / oneMB} MB so far");
            }
            var curCallNum = StreamCommunicator.ReadBufferedLong(arg, 0);
            if (_numJobs == 1 && _lastCallNum + 1 != curCallNum)
            {
                Console.WriteLine("*X* Out of order message. Expected {0}, got {1}", _lastCallNum + 1, curCallNum);
            }
            _lastCallNum = curCallNum;
            if (_isBidirectional)
            {
                for (int i = 0; i < _numJobs; i++)
                {
                    _jobs[i].MFork(arg);
                }
            }
        }

        public async Task AmIHealthyAsync(DateTime currentTime)
        {
            _nCalls++;
            if (_nCalls % 3000 == 0)
            {
                Console.WriteLine("*X* I'm healthy after {0} checks at time:" + currentTime.ToString(), _nCalls);
            }
        }

        public async Task PrintBytesReceivedAsync()
        {
            Console.WriteLine("Bytes received: {0}", _bytesReceived);
            for (int i = 0; i < _numJobs; i++)
            {
                _jobs[i].PrintBytesReceivedFork();
            }
            ServerBootstrapper.finishedTokenQ.Enqueue(0);
        }

        void TimerLoop()
        {
            while (true)
            {
                Thread.Sleep(1);
                thisProxy.AmIHealthyFork(DateTime.Now);
            }
        }

        protected override void BecomingPrimary()
        {
            Console.WriteLine("becoming upgraded primary");
            Thread timerThread = new Thread(TimerLoop);
            timerThread.Start();
        }

        protected override void OnSave(Stream stream)
        {
            Console.WriteLine("*X* At checkpoint, upgraded service received {0} messages", _lastCallNum);
        }

    }

    enum ICDeploymentMode
    {
        SecondProc,
        InProcDeploy,
        InProcManual,
        InProcTimeTravel
    }

    class ServerBootstrapper
    {
        private static string _serviceLogPath = Path.Combine(Path.GetPathRoot(Path.GetFullPath(".")), "AmbrosiaLogs") + Path.DirectorySeparatorChar;
        private static long _checkpointToLoad = 1;
        private static int _currentVersion = 0;
        private static int _receivePort = -1;
        private static int _sendPort = -1;
        private static int _icPort = -1;
        private static string _perfJob;
        private static string _perfServer;
        private static bool _autoContinue;
        private static bool _isBidirectional = true;
        private static int _numJobs = 1;
        private static bool _isUpgrading;
        private static long _memoryUsed;
        public static AsyncQueue<int> finishedTokenQ;
        private static ICDeploymentMode _ICDeploymentMode = ICDeploymentMode.SecondProc;
        static Thread _iCThread;
        static FileStream _iCWriter = null;
        static TextWriterTraceListener _iCListener;

        static internal void FlushOutput()
        {
            if (_iCWriter != null)
            {
                Trace.Flush();
                Trace.Flush();
                _iCListener.Flush();
                _iCListener.Flush();
                _iCWriter.Flush();
                _iCWriter.Flush();
            }
            Console.Out.Flush();
            Console.Out.Flush();
        }

        static internal async Task WaitForJobsToFinishAsync()
        {
            int jobsEnded = 0;
            while (jobsEnded < _numJobs)
            {
                await finishedTokenQ.DequeueAsync();
                jobsEnded++;
            }
            Console.WriteLine("DONE");
            FlushOutput();
            await Task.Delay(5000);
            return;
        }

        static void Main(string[] args)
        {
            ParseAndValidateOptions(args);

            if (_ICDeploymentMode == ICDeploymentMode.InProcDeploy || _ICDeploymentMode == ICDeploymentMode.InProcManual || _ICDeploymentMode == ICDeploymentMode.InProcTimeTravel)
            {
                GenericLogsInterface.SetToGenericLogs();
                _iCWriter = new FileStream("ICOutput_" + _perfServer.ToString() + "_" + _icPort.ToString() + ".txt", FileMode.Create);
                _iCListener = new TextWriterTraceListener(_iCWriter);
                Trace.Listeners.Add(_iCListener);
            }

            try
            {
                finishedTokenQ = new AsyncQueue<int>();

                // for debugging don't want to auto continue but for test automation want this to auto continue
                if (!_autoContinue)
                {
                    Console.WriteLine("Pausing execution of " + _perfServer + ". Press enter to deploy and continue.");
                    Console.ReadLine();
                }

                if (!_isUpgrading)
                {
                    var myServer = new Server(_perfJob, _isBidirectional, _numJobs, _memoryUsed);
                    switch (_ICDeploymentMode)
                    {
                        case ICDeploymentMode.SecondProc:
                            using (var c = AmbrosiaFactory.Deploy<IServer>(_perfServer, myServer, _receivePort, _sendPort))
                            {
                                WaitForJobsToFinishAsync().Wait();                            
                            }
                            break;
                        case ICDeploymentMode.InProcDeploy:
                            using (var c = AmbrosiaFactory.Deploy<IServer>(_perfServer, myServer, _icPort))
                            {
                                WaitForJobsToFinishAsync().Wait();
                            }
                            break;
                        case ICDeploymentMode.InProcTimeTravel:
                            using (var c = AmbrosiaFactory.Deploy<IServer>(_perfServer, myServer, _serviceLogPath, _checkpointToLoad, _currentVersion))
                            {
                                WaitForJobsToFinishAsync().Wait();
                            }
                            break;
                        case ICDeploymentMode.InProcManual:
                            var myName = _perfServer;
                            var myPort = _icPort;
                            var ambrosiaArgs = new string[2];
                            ambrosiaArgs[0] = "-i=" + myName;
                            ambrosiaArgs[1] = "-p=" + myPort;
                            Console.WriteLine("*X* ImmortalCoordinator -i=" + myName + " -p=" + myPort.ToString());
                            _iCThread = new Thread(() => CRA.Worker.Program.main(ambrosiaArgs)) { IsBackground = true };
                            _iCThread.Start();
                            using (var c = AmbrosiaFactory.Deploy<IServer>(_perfServer, myServer, _receivePort, _sendPort))
                            {
                                WaitForJobsToFinishAsync().Wait();
                            }
                            break;
                    }
                }
                else
                {
                    var myServer = new Server(_perfJob, _isBidirectional, _numJobs, _memoryUsed);
                    switch (_ICDeploymentMode)
                    {
                        case ICDeploymentMode.SecondProc:
                            using (var c = AmbrosiaFactory.Deploy<IServer, IServer, ServerUpgraded>(_perfServer, myServer, _receivePort, _sendPort))
                            {
                                WaitForJobsToFinishAsync().Wait();
                            }
                            break;
                        case ICDeploymentMode.InProcDeploy:
                            using (var c = AmbrosiaFactory.Deploy<IServer, IServer, ServerUpgraded>(_perfServer, myServer, _icPort))
                            {
                                WaitForJobsToFinishAsync().Wait();
                            }
                            break;
                        case ICDeploymentMode.InProcTimeTravel:
                            using (var c = AmbrosiaFactory.Deploy<IServer, IServer, ServerUpgraded>(_perfServer, myServer, _serviceLogPath, _checkpointToLoad, _currentVersion))
                            {
                                WaitForJobsToFinishAsync().Wait();
                            }
                            break;
                        case ICDeploymentMode.InProcManual:
                            var myName = _perfServer;
                            var myPort = _icPort;
                            var ambrosiaArgs = new string[2];
                            ambrosiaArgs[0] = "-i=" + myName;
                            ambrosiaArgs[1] = "-p=" + myPort;
                            Console.WriteLine("*X* ImmortalCoordinator -i=" + myName + " -p=" + myPort.ToString());
                            _iCThread = new Thread(() => CRA.Worker.Program.main(ambrosiaArgs)) { IsBackground = true };
                            _iCThread.Start();
                            using (var c = AmbrosiaFactory.Deploy<IServer, IServer, ServerUpgraded>(_perfServer, myServer, _receivePort, _sendPort))
                            {
                                WaitForJobsToFinishAsync().Wait();
                            }
                            break;
                    }
                }
            }
            catch (Exception e) { Console.WriteLine(e.Message); }
            Console.Out.Flush();
            Console.Out.Flush();
            System.Environment.Exit(0);
        }

        private static void ParseAndValidateOptions(string[] args)
        {
            var options = ParseOptions(args, out var shouldShowHelp);
            ValidateOptions(options, shouldShowHelp);
        }

        private static OptionSet ParseOptions(string[] args, out bool shouldShowHelp)
        {
            var showHelp = false;
            var options = new OptionSet {
                { "j|jobName=", "The service name of the job [REQUIRED].", j => _perfJob = j },
                { "s|serverName=", "The service name of the server [REQUIRED].", s => _perfServer = s },
                { "rp|receivePort=", "The service receive from port [REQUIRED].", rp => _receivePort = int.Parse(rp) },
                { "sp|sendPort=", "The service send to port. [REQUIRED]", sp => _sendPort = int.Parse(sp) },
                { "nbd|notBidirectional", "Disable bidirectional communication.", nbd => _isBidirectional = false },
                { "icp|ICPort=", "The IC port, if the IC should be run in proc. Note that if this is specified, the command line ports override stored registration settings", icp => _icPort = int.Parse(icp) },
                { "n|numOfJobs=", "The number of jobs.", n => _numJobs = int.Parse(n) },
                { "u|upgrading", "Is upgrading.", u => _isUpgrading = true },
                { "m|memoryUsed=", "Memory used.", m => _memoryUsed = long.Parse(m) },
                { "c|autoContinue", "Is continued automatically at start", c => _autoContinue = true },
                { "d|ICDeploymentMode=", "IC deployment mode specification (SecondProc(Default)/InProcDeploy/InProcManual/InProcTimeTravel)", d => _ICDeploymentMode = (ICDeploymentMode) Enum.Parse(typeof(ICDeploymentMode), d, true)},
                { "l|log=", "If TTD, the service log path.", l => _serviceLogPath = l },
                { "ch|checkpoint=", "If TTD, the checkpoint # to load.", c => _checkpointToLoad = long.Parse(c) },
                { "cv|currentVersion=", "The version # used to time travel debug (ignored otherwise).", cv => _currentVersion = int.Parse(cv)},
                { "h|help", "show this message and exit", h => showHelp = h != null },
            };

            try
            {
                options.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine("Invalid arguments: " + e.Message);
                ShowHelp(options);
                Environment.Exit(1);
            }

            shouldShowHelp = showHelp;

            return options;
        }

        private static void ValidateOptions(OptionSet options, bool shouldShowHelp)
        {
            var errorMessage = string.Empty;
            if (_perfJob == null) errorMessage += "Job name is required.\n";
            if (_perfServer == null) errorMessage += "Server name is required.\n";

            if (errorMessage != string.Empty)
            {
                Console.WriteLine(errorMessage);
                ShowHelp(options);
                Environment.Exit(1);
            }

            if (shouldShowHelp) ShowHelp(options);
        }

        private static void ShowHelp(OptionSet options)
        {
            var name = typeof(ServerBootstrapper).Assembly.GetName().Name;
#if NETCORE
            Console.WriteLine($"Usage: dotnet {name}.dll [OPTIONS]\nOptions:");
#else
            Console.WriteLine($"Usage: {name}.exe [OPTIONS]\nOptions:");
#endif
            options.WriteOptionDescriptions(Console.Out);
            Environment.Exit(0);
        }
    }
}
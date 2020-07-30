using System;
using System.Net;
using System.Net.Sockets;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using CRA.ClientLibrary;
using System.Reflection;
using Ambrosia;


namespace CRA.Worker
{
    public class Program
    {
        enum LogStorageOptions
        {
            Files,
            Blobs
        }

        private static string _instanceName;
        private static int _port = -1;
        private static string _ipAddress;
        private static string _secureNetworkAssemblyName;
        private static string _secureNetworkClassName;
        private static bool _isActiveActive = false;
        private static int _replicaNumber = 0;
        private static LogStorageOptions _logStorageType = LogStorageOptions.Files;

        public static void main(string[] args)
        {
            ParseAndValidateOptions(args);

            switch (_logStorageType)
            {
                case LogStorageOptions.Files:
                    GenericLogsInterface.SetToGenericLogs();
                    break;
                case LogStorageOptions.Blobs:
                    AzureBlobsLogsInterface.SetToAzureBlobsLogs();
                    break;
            }

            var replicaName = $"{_instanceName}{_replicaNumber}";

            if (_ipAddress == null)
            {
                _ipAddress = GetLocalIPAddress();
            }

            string storageConnectionString = null;

            if (storageConnectionString == null)
            {
                storageConnectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONN_STRING");
            }

            if (!_isActiveActive && _replicaNumber != 0)
            {
                throw new InvalidOperationException("Can't specify a replica number without the activeActive flag");
            }

            if (storageConnectionString == null)
            {
                throw new InvalidOperationException("Azure storage connection string not found. Use appSettings in your app.config to provide this using the key AZURE_STORAGE_CONN_STRING, or use the environment variable AZURE_STORAGE_CONN_STRING.");
            }

            int connectionsPoolPerWorker;
            string connectionsPoolPerWorkerString = "0";
            if (connectionsPoolPerWorkerString != null)
            {
                try
                {
                    connectionsPoolPerWorker = Convert.ToInt32(connectionsPoolPerWorkerString);
                }
                catch
                {
                    throw new InvalidOperationException("Maximum number of connections per CRA worker is wrong. Use appSettings in your app.config to provide this using the key CRA_WORKER_MAX_CONN_POOL.");
                }
            }
            else
            {
                connectionsPoolPerWorker = 1000;
            }

            ISecureStreamConnectionDescriptor descriptor = null;
            if (_secureNetworkClassName != null)
            {
                Type type;
                if (_secureNetworkAssemblyName != null)
                {
                    var assembly = Assembly.Load(_secureNetworkAssemblyName);
                    type = assembly.GetType(_secureNetworkClassName);
                }
                else
                {
                    type = Type.GetType(_secureNetworkClassName);
                }
                descriptor = (ISecureStreamConnectionDescriptor)Activator.CreateInstance(type);
            }

            var dataProvider = new CRA.DataProvider.Azure.AzureDataProvider(storageConnectionString);
            var worker = new CRAWorker
                (replicaName, _ipAddress, _port,
                dataProvider, descriptor, connectionsPoolPerWorker);

            worker.DisableDynamicLoading(); 
            worker.SideloadVertex(new AmbrosiaRuntime(), "ambrosia");

            worker.Start();
        }

        static void Main(string[] args)
        {
            main(args);
        }

        private static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new InvalidOperationException("Local IP Address Not Found!");
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
                { "i|instanceName=", "The instance name [REQUIRED].", n => _instanceName = n },
                { "p|port=", "An port number [REQUIRED].", p => _port = Int32.Parse(p) },
                {"aa|activeActive", "Is active-active enabled.", aa => _isActiveActive = true},
                { "r|replicaNum=", "The replica #", r => { _replicaNumber = int.Parse(r); _isActiveActive=true; } },
                { "an|assemblyName=", "The secure network assembly name.", an => _secureNetworkAssemblyName = an },
                { "ac|assemblyClass=", "The secure network assembly class.", ac => _secureNetworkClassName = ac },
                { "ip|IPAddr=", "Override automatic self IP detection", i => _ipAddress = i },
                { "h|help", "show this message and exit", h => showHelp = h != null },
                { "rp|receivePort=", "The service receive from port override.", rp => StartupParamOverrides.receivePort = int.Parse(rp) },
                { "sp|sendPort=", "The service send to port override.", sp => StartupParamOverrides.sendPort = int.Parse(sp) },
                { "lst|logStorageType(Files,Blobs)=", lst => _logStorageType = (LogStorageOptions) Enum.Parse(typeof(LogStorageOptions), lst)},
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
            if (_instanceName == null) errorMessage += "Instance name is required.";
            if (_port == -1) errorMessage += "Port number is required.";

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
            var name = typeof(Program).Assembly.GetName().Name;
            Console.WriteLine("Worker for Common Runtime for Applications (CRA) [http://github.com/Microsoft/CRA]");
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

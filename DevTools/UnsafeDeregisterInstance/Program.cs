using CRA.ClientLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnsafeDeregisterService
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("UnsafeDeregisterInstance InstanceName");
                Console.WriteLine("WARNING: This is a metadata hacking tool that should NEVER be used on a real deployment");
                Console.WriteLine("This tool is a convenience for developers who want to more easily test certain application modfications");
                Console.WriteLine("Usage: UnsafeDeregisterInstance InstanceName");
                return;
            }
            var client = new CRAClientLibrary(Environment.GetEnvironmentVariable("AZURE_STORAGE_CONN_STRING"));
            var serviceName = args[0];
            foreach (var endpt in client.GetInputEndpoints(serviceName))
            {
                client.DeleteEndpoint(serviceName, endpt);
            }
            foreach (var endpt in client.GetOutputEndpoints(serviceName))
            {
                client.DeleteEndpoint(serviceName, endpt);
            }
            foreach (var conn in client.GetConnectionsFromVertex(serviceName))
            {
                client.DeleteConnectionInfo(conn);
            }
            foreach (var conn in client.GetConnectionsToVertex(serviceName))
            {
                client.DeleteConnectionInfo(conn);
            }
            try
            {
                client.DeleteVertex(serviceName);
            }
            catch { }
        }
    }
}

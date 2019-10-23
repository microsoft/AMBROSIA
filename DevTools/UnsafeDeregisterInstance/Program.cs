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
            var dataProvider = new CRA.DataProvider.Azure.AzureDataProvider(Environment.GetEnvironmentVariable("AZURE_STORAGE_CONN_STRING"));
            var client = new CRAClientLibrary(dataProvider);
            var serviceName = args[0];
            foreach (var endpt in client.GetInputEndpointsAsync(serviceName).GetAwaiter().GetResult())
            {
                client.DeleteEndpoint(serviceName, endpt);
            }
            foreach (var endpt in client.GetOutputEndpointsAsync(serviceName).GetAwaiter().GetResult())
            {
                client.DeleteEndpoint(serviceName, endpt);
            }
            foreach (var conn in client.GetConnectionsFromVertexAsync(serviceName).GetAwaiter().GetResult())
            {
                client.DeleteConnectionInfoAsync(conn).GetAwaiter().GetResult();
            }
            foreach (var conn in client.GetConnectionsToVertexAsync(serviceName).GetAwaiter().GetResult())
            {
                client.DeleteConnectionInfoAsync(conn).GetAwaiter().GetResult();
            }
            try
            {
                client.DeleteVertexAsync(serviceName).GetAwaiter().GetResult();
            }
            catch { }
        }
    }
}

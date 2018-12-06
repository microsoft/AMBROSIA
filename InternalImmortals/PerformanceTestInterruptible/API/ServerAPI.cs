using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ambrosia;

namespace Server
{
    // Hand-written Franklin interface.
    // Written in C# for now. Could be a new IDL or re-use Protobuf or whatever.
    // Should also indicate the data serialization format for each parameter and return value.
    public interface IServer
    {
        void M(byte[] arg);

        [ImpulseHandler]
        void AmIHealthy(DateTime currentTime);
        void PrintBytesReceived();
    }
}
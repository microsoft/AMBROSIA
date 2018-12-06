using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    // This is the API that the client programs against.
    // The server programs against the original interface.
    public interface IServer
    {
        byte[] M(byte[] arg);

        void PrintMessage(string s, double d);

        void PrintBytesReceived();
    }
}

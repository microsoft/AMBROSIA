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
        Tuple<byte[], long> ResizeImage(byte[] arg, long sendTime);
        void PrintComputeTime();
    }
}

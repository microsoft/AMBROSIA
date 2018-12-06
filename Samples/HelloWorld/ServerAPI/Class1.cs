using Ambrosia;
using System;

namespace IServer
{
    public interface IServer
    {
        int ReceiveMessage(string Message);
    }
}

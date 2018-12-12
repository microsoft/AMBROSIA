using Ambrosia;

namespace Server
{
    public interface IServer
    {
        int ReceiveMessage(string Message);
    }
}

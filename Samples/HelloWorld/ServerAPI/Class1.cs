using Ambrosia;

namespace Server
{
    public interface IServer
    {
        void AddRespondee(string respondeeName);
        void ReceiveMessage(string Message);
    }
}

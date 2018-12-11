using Ambrosia;

namespace Client2
{
    public interface IClient2
    {
        void SendMessage(string message);

        [ImpulseHandler]
        void ReceiveKeyboardInput(string message);
    }
}

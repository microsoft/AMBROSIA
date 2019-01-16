using Ambrosia;

namespace Client2
{
    public interface IClient2
    {
        [ImpulseHandler]
        void ReceiveKeyboardInput(string message);
    }
}

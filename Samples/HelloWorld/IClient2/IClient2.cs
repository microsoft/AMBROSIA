using Ambrosia;
using System;

namespace IClient2
{
    public interface IClient2
    {
        void SendMessage(string message);

        [ImpulseHandler]
        void ReceiveKeyboardInput(string message);
    }
}

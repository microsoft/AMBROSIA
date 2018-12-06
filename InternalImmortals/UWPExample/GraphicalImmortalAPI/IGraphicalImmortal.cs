using System;
using System.Threading.Tasks;
using Ambrosia;

namespace GraphicalImmortalAPI
{
    public interface IGraphicalImmortal
    {
        [ImpulseHandler]
        void AcceptLocalInput(int x, int y, bool mouseDown);
        void AcceptRemoteInput(int x, int y, bool mouseDown);
    }
}

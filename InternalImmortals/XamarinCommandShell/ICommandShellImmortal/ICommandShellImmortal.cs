using System;
using Ambrosia;

namespace XamarinCommandShell
{
    public interface ICommandShellImmortal
    {
        [ImpulseHandler]
        void SubmitCommand(string command);

        [ImpulseHandler]
        void SetCurrentDirectory(string newDirectory);
    }
}

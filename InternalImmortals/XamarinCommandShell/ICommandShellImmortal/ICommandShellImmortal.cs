using System;
using Ambrosia;

namespace XamarinCommandShell
{
    public interface ICommandShellImmortal
    {
        [ImpulseHandler]
        void SubmitCommand(string command);

        [ImpulseHandler]
        void SetRootDirectory(string newDirectory);

        [ImpulseHandler]
        void SetRelativeDirectory(string newDirectory);

        [ImpulseHandler]
        void AddConsoleOutput(string outputToAdd);
    }
}

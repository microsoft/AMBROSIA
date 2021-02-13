using System;
using System.IO;
using System.Diagnostics;

namespace XamarinCommandShell.GTK
{
    class OSCommands : IOSCommands
    {
        public void ExecuteCommand(string command,
            string workingDirectory,
            TextWriter commandOutputWriter)

        {
            var commandProcess = new Process();
            commandProcess.StartInfo.FileName = "/bin/sh";
            commandProcess.StartInfo.WorkingDirectory = workingDirectory;
            commandProcess.StartInfo.Arguments = "-c \"" + command + "\"";
            //commandProcess.StartInfo.RedirectStandardInput = true;
            //            commandProcess.StartInfo.RedirectStandardOutput = true;
            //            commandProcess.StartInfo.RedirectStandardError = true;
            commandProcess.StartInfo.CreateNoWindow = true;
            commandProcess.StartInfo.UseShellExecute = false;
            commandProcess.StartInfo.RedirectStandardError = true;
            commandProcess.StartInfo.RedirectStandardOutput = true;
            commandProcess.OutputDataReceived += new DataReceivedEventHandler((sender, e) => { commandOutputWriter.WriteLine(e.Data); });
            commandProcess.ErrorDataReceived += new DataReceivedEventHandler((sender, e) => { commandOutputWriter.WriteLine(e.Data); });
            commandProcess.Start();
            commandProcess.BeginOutputReadLine();
            commandProcess.BeginErrorReadLine();
            commandProcess.WaitForExit();
        }
    }
}


#if false
using System;
using System.IO;

namespace XamarinCommandShell.GTK
{
    class OSCommands : IOSCommands
    {
        public void ExecuteCommand(string command,
            string workingDirectory,
            TextWriter commandOutputWriter)
        {
            Console.WriteLine("Not implementated");
        }
    }
}
#endif
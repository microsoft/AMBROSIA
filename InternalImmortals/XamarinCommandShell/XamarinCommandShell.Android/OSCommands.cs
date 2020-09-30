using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace XamarinCommandShell.Droid
{
    class OSCommands : IOSCommands
    {
        public void ExecuteCommand(string command,
                                   TextWriter commandOutputWriter)

        {
            Console.WriteLine("Not implemented");
        }
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GraphicalAppUWP
{
    public class DebugTextWriter : TextWriter
    {
        public override Encoding Encoding => new UTF8Encoding();

        public override void Write(char value)
        {
            Debug.Write(value);
        }

        public override void WriteLine(string value)
        {
            Debug.WriteLine(value);
        }
    }
}

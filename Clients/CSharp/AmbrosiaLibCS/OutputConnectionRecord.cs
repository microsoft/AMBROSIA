using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Ambrosia
{
    [DataContract]
    public class OutputConnectionRecord
    {
        public Stream ConnectionStream { get; set; }
        // Set on reconnection. Established where to replay from or filter to
        [DataMember]
        // RPC output buffers
        public EventBuffer BufferedOutput { get; set; }
        // A cursor which specifies where the last RPC output ended
        public EventBuffer.BuffersCursor placeInOutput;
        // Work Q for output producing work.
        public AsyncQueue<int> WorkQ { get; set; }
        // The number of sends which are currently enqueued. Should be updated with interlocked increment and decrement
        public long _sendsEnqueued;
        public OutputConnectionRecord()
        {
            ConnectionStream = null;
            BufferedOutput = new EventBuffer();
            WorkQ = new AsyncQueue<int>();
            _sendsEnqueued = 0;
        }
    }
}

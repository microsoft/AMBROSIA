using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ambrosia;
using SharedAmbrosiaConstants;

namespace Ambrosia
{
    public sealed class ConsoleImmortal : Immortal.Dispatcher
    {

        public ConsoleImmortal(Immortal c, string dataCenterName)
            : base(c, new Immortal.SimpleImmortalSerializer(), dataCenterName, -1 /*ignored when not setting up connections*/, -1 /*ignored when not setting up connections*/, false)
        { }
        public override void Dispose()
        {
            // nothing to dispose
        }

        public void WriteLine(object o)
        {
            Console.WriteLine(o);
        }

        public string ReadLine() { return Console.ReadLine(); }

        public override async Task<bool> DispatchToMethod(int methodId, RpcTypes.RpcType rpcType, string senderOfRPC, long sequenceNumber, byte[] buffer, int cursor)
        {
            // arg1: string, so read its length, then the string itself
            var arg1Length = buffer.ReadBufferedInt(cursor);
            cursor += StreamCommunicator.IntSize(arg1Length);
            var arg1Buffer = new byte[arg1Length];
            Buffer.BlockCopy(buffer, cursor, arg1Buffer, 0, arg1Length);
            cursor += arg1Length;
            var arg1 = Encoding.UTF8.GetString(arg1Buffer);

            // call the method
            Console.WriteLine(arg1);

            return true;
        }
    }
}
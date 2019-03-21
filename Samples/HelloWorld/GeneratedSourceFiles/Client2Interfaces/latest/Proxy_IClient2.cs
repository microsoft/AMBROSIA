
using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Ambrosia;
using static Ambrosia.StreamCommunicator;


namespace Client2
{
    /// <summary>
    /// This class is the proxy that runs in the client's process and communicates with the local Ambrosia runtime.
    /// It runs within the client's process, so it is generated in the language that the client is using.
    /// It is returned from ImmortalFactory.CreateClient when a client requests a container that supports the interface IClient2Proxy.
    /// </summary>
    [System.Runtime.Serialization.DataContract]
    public class IClient2Proxy_Implementation : Immortal.InstanceProxy, IClient2Proxy
    {

        public IClient2Proxy_Implementation(string remoteAmbrosiaRuntime, bool attachNeeded)
            : base(remoteAmbrosiaRuntime, attachNeeded)
        {
        }


        void IClient2Proxy.ReceiveKeyboardInputFork(System.String p_0)
        {
			if (!Immortal.IsPrimary)
			{
                throw new Exception("Unable to send an Impulse RPC while not being primary.");
			}

            SerializableTaskCompletionSource rpcTask;

            // Compute size of serialized arguments
            var totalArgSize = 0;

            // Argument 0
			int arg0Size = 0;
			byte[] arg0Bytes = null;

            arg0Bytes = Ambrosia.BinarySerializer.Serialize<System.String>(p_0);
arg0Size = IntSize(arg0Bytes.Length) + arg0Bytes.Length;

            totalArgSize += arg0Size;

            var wp = this.StartRPC<object>(1 /* method identifier for ReceiveKeyboardInput */, totalArgSize, out rpcTask, RpcTypes.RpcType.Impulse);

            // Serialize arguments


            // Serialize arg0
            wp.curLength += wp.PageBytes.WriteInt(wp.curLength, arg0Bytes.Length);
Buffer.BlockCopy(arg0Bytes, 0, wp.PageBytes, wp.curLength, arg0Bytes.Length);
wp.curLength += arg0Bytes.Length;


            this.ReleaseBufferAndSend();
            return;
        }

        private object
        ReceiveKeyboardInput_ReturnValue(byte[] buffer, int cursor)
        {
            // buffer will be an empty byte array since the method ReceiveKeyboardInput returns void
            // so nothing to read, just getting called is the signal to return to the client
            return this;
        }
    }
}
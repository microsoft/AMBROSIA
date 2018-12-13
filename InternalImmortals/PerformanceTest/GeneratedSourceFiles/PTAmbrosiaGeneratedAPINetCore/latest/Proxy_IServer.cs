
using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Ambrosia;
using static Ambrosia.StreamCommunicator;


namespace Server
{
    /// <summary>
    /// This class is the proxy that runs in the client's process and communicates with the local Ambrosia runtime.
    /// It runs within the client's process, so it is generated in the language that the client is using.
    /// It is returned from ImmortalFactory.CreateClient when a client requests a container that supports the interface IServerProxy.
    /// </summary>
    [System.Runtime.Serialization.DataContract]
    public class IServerProxy_Implementation : Immortal.InstanceProxy, IServerProxy
    {

        public IServerProxy_Implementation(string remoteAmbrosiaRuntime, bool attachNeeded)
            : base(remoteAmbrosiaRuntime, attachNeeded)
        {
        }

        async Task<Byte[]>
        IServerProxy.MAsync(System.Byte[] p_0)
        {
			return await MAsync(p_0);
        }

        async Task<Byte[]>
        MAsync(System.Byte[] p_0)
        {
            SerializableTaskCompletionSource rpcTask;
            // Make call, wait for reply
            // Compute size of serialized arguments
            var totalArgSize = 0;

			var p_1 = default(Byte[]);
			int arg0Size = 0;
			byte[] arg0Bytes = null;

            // Argument 0
            arg0Bytes = p_0;
arg0Size = IntSize(arg0Bytes.Length) + arg0Bytes.Length;

            totalArgSize += arg0Size;

            var wp = this.StartRPC<Byte[]>(methodIdentifier: 1 /* method identifier for M */, lengthOfSerializedArguments: totalArgSize, taskToWaitFor: out rpcTask);
			var asyncContext = new AsyncContext { SequenceNumber = Immortal.CurrentSequenceNumber };

            // Serialize arguments


            // Serialize arg0
            wp.curLength += wp.PageBytes.WriteInt(wp.curLength, arg0Bytes.Length);
Buffer.BlockCopy(arg0Bytes, 0, wp.PageBytes, wp.curLength, arg0Bytes.Length);
wp.curLength += arg0Bytes.Length;


            ReleaseBufferAndSend();

			var taskToWaitFor = Immortal.CallCache.Data[asyncContext.SequenceNumber].GetAwaitableTaskWithAdditionalInfoAsync();
            var currentResult = await taskToWaitFor;

			var isSaved = await Immortal.TrySaveContextContinuationAsync(currentResult);

			if (isSaved)
			{
				taskToWaitFor = Immortal.CallCache.Data[asyncContext.SequenceNumber].GetAwaitableTaskWithAdditionalInfoAsync();
				currentResult = await taskToWaitFor;
			}			

			var result = await Immortal.TryTakeCheckpointContinuationAsync(currentResult);

			return (Byte[]) result.Result;
        }

        void IServerProxy.MFork(System.Byte[] p_0)
        {
            SerializableTaskCompletionSource rpcTask;

            // Compute size of serialized arguments
            var totalArgSize = 0;

            // Argument 0
			int arg0Size = 0;
			byte[] arg0Bytes = null;

            arg0Bytes = p_0;
arg0Size = IntSize(arg0Bytes.Length) + arg0Bytes.Length;

            totalArgSize += arg0Size;

            var wp = this.StartRPC<Byte[]>(1 /* method identifier for M */, totalArgSize, out rpcTask, RpcTypes.RpcType.FireAndForget);

            // Serialize arguments


            // Serialize arg0
            wp.curLength += wp.PageBytes.WriteInt(wp.curLength, arg0Bytes.Length);
Buffer.BlockCopy(arg0Bytes, 0, wp.PageBytes, wp.curLength, arg0Bytes.Length);
wp.curLength += arg0Bytes.Length;


            this.ReleaseBufferAndSend();
            return;
        }

        private Byte[]
        M_ReturnValue(byte[] buffer, int cursor)
        {
            // deserialize return value
            var returnValue_ValueLength = buffer.ReadBufferedInt(cursor);
cursor += IntSize(returnValue_ValueLength);
var returnValue_ValueBuffer = new byte[returnValue_ValueLength];
Buffer.BlockCopy(buffer, cursor, returnValue_ValueBuffer, 0, returnValue_ValueLength);
cursor += returnValue_ValueLength;
var returnValue = returnValue_ValueBuffer;

            return returnValue;
        }
        async Task
        IServerProxy.PrintMessageAsync(System.String p_0,System.Double p_1)
        {
			 await PrintMessageAsync(p_0,p_1);
        }

        async Task
        PrintMessageAsync(System.String p_0,System.Double p_1)
        {
            SerializableTaskCompletionSource rpcTask;
            // Make call, wait for reply
            // Compute size of serialized arguments
            var totalArgSize = 0;

			int arg0Size = 0;
			byte[] arg0Bytes = null;

            // Argument 0
            arg0Bytes = Ambrosia.BinarySerializer.Serialize<System.String>(p_0);
arg0Size = IntSize(arg0Bytes.Length) + arg0Bytes.Length;

            totalArgSize += arg0Size;
			int arg1Size = 0;
			byte[] arg1Bytes = null;

            // Argument 1
            arg1Bytes = Ambrosia.BinarySerializer.Serialize<System.Double>(p_1);
arg1Size = IntSize(arg1Bytes.Length) + arg1Bytes.Length;

            totalArgSize += arg1Size;

            var wp = this.StartRPC<object>(methodIdentifier: 2 /* method identifier for PrintMessage */, lengthOfSerializedArguments: totalArgSize, taskToWaitFor: out rpcTask);
			var asyncContext = new AsyncContext { SequenceNumber = Immortal.CurrentSequenceNumber };

            // Serialize arguments


            // Serialize arg0
            wp.curLength += wp.PageBytes.WriteInt(wp.curLength, arg0Bytes.Length);
Buffer.BlockCopy(arg0Bytes, 0, wp.PageBytes, wp.curLength, arg0Bytes.Length);
wp.curLength += arg0Bytes.Length;


            // Serialize arg1
            wp.curLength += wp.PageBytes.WriteInt(wp.curLength, arg1Bytes.Length);
Buffer.BlockCopy(arg1Bytes, 0, wp.PageBytes, wp.curLength, arg1Bytes.Length);
wp.curLength += arg1Bytes.Length;


            ReleaseBufferAndSend();

			var taskToWaitFor = Immortal.CallCache.Data[asyncContext.SequenceNumber].GetAwaitableTaskWithAdditionalInfoAsync();
            var currentResult = await taskToWaitFor;

			var isSaved = await Immortal.TrySaveContextContinuationAsync(currentResult);

			if (isSaved)
			{
				taskToWaitFor = Immortal.CallCache.Data[asyncContext.SequenceNumber].GetAwaitableTaskWithAdditionalInfoAsync();
				currentResult = await taskToWaitFor;
			}			

			 await Immortal.TryTakeCheckpointContinuationAsync(currentResult);

			return;
        }

        void IServerProxy.PrintMessageFork(System.String p_0,System.Double p_1)
        {
            SerializableTaskCompletionSource rpcTask;

            // Compute size of serialized arguments
            var totalArgSize = 0;

            // Argument 0
			int arg0Size = 0;
			byte[] arg0Bytes = null;

            arg0Bytes = Ambrosia.BinarySerializer.Serialize<System.String>(p_0);
arg0Size = IntSize(arg0Bytes.Length) + arg0Bytes.Length;

            totalArgSize += arg0Size;
            // Argument 1
			int arg1Size = 0;
			byte[] arg1Bytes = null;

            arg1Bytes = Ambrosia.BinarySerializer.Serialize<System.Double>(p_1);
arg1Size = IntSize(arg1Bytes.Length) + arg1Bytes.Length;

            totalArgSize += arg1Size;

            var wp = this.StartRPC<object>(2 /* method identifier for PrintMessage */, totalArgSize, out rpcTask, RpcTypes.RpcType.FireAndForget);

            // Serialize arguments


            // Serialize arg0
            wp.curLength += wp.PageBytes.WriteInt(wp.curLength, arg0Bytes.Length);
Buffer.BlockCopy(arg0Bytes, 0, wp.PageBytes, wp.curLength, arg0Bytes.Length);
wp.curLength += arg0Bytes.Length;


            // Serialize arg1
            wp.curLength += wp.PageBytes.WriteInt(wp.curLength, arg1Bytes.Length);
Buffer.BlockCopy(arg1Bytes, 0, wp.PageBytes, wp.curLength, arg1Bytes.Length);
wp.curLength += arg1Bytes.Length;


            this.ReleaseBufferAndSend();
            return;
        }

        private object
        PrintMessage_ReturnValue(byte[] buffer, int cursor)
        {
            // buffer will be an empty byte array since the method PrintMessage returns void
            // so nothing to read, just getting called is the signal to return to the client
            return this;
        }
        async Task
        IServerProxy.PrintBytesReceivedAsync()
        {
			 await PrintBytesReceivedAsync();
        }

        async Task
        PrintBytesReceivedAsync()
        {
            SerializableTaskCompletionSource rpcTask;
            // Make call, wait for reply
            // Compute size of serialized arguments
            var totalArgSize = 0;


            var wp = this.StartRPC<object>(methodIdentifier: 3 /* method identifier for PrintBytesReceived */, lengthOfSerializedArguments: totalArgSize, taskToWaitFor: out rpcTask);
			var asyncContext = new AsyncContext { SequenceNumber = Immortal.CurrentSequenceNumber };

            // Serialize arguments


            ReleaseBufferAndSend();

			var taskToWaitFor = Immortal.CallCache.Data[asyncContext.SequenceNumber].GetAwaitableTaskWithAdditionalInfoAsync();
            var currentResult = await taskToWaitFor;

			var isSaved = await Immortal.TrySaveContextContinuationAsync(currentResult);

			if (isSaved)
			{
				taskToWaitFor = Immortal.CallCache.Data[asyncContext.SequenceNumber].GetAwaitableTaskWithAdditionalInfoAsync();
				currentResult = await taskToWaitFor;
			}			

			 await Immortal.TryTakeCheckpointContinuationAsync(currentResult);

			return;
        }

        void IServerProxy.PrintBytesReceivedFork()
        {
            SerializableTaskCompletionSource rpcTask;

            // Compute size of serialized arguments
            var totalArgSize = 0;


            var wp = this.StartRPC<object>(3 /* method identifier for PrintBytesReceived */, totalArgSize, out rpcTask, RpcTypes.RpcType.FireAndForget);

            // Serialize arguments


            this.ReleaseBufferAndSend();
            return;
        }

        private object
        PrintBytesReceived_ReturnValue(byte[] buffer, int cursor)
        {
            // buffer will be an empty byte array since the method PrintBytesReceived returns void
            // so nothing to read, just getting called is the signal to return to the client
            return this;
        }
    }
}
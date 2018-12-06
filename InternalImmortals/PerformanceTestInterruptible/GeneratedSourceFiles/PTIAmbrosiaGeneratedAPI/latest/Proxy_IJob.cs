
using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Ambrosia;
using static Ambrosia.StreamCommunicator;


namespace JobAPI
{
    /// <summary>
    /// This class is the proxy that runs in the client's process and communicates with the local Ambrosia runtime.
    /// It runs within the client's process, so it is generated in the language that the client is using.
    /// It is returned from ImmortalFactory.CreateClient when a client requests a container that supports the interface IJobProxy.
    /// </summary>
    [System.Runtime.Serialization.DataContract]
    public class IJobProxy_Implementation : Immortal.InstanceProxy, IJobProxy
    {

        public IJobProxy_Implementation(string remoteAmbrosiaRuntime, bool attachNeeded)
            : base(remoteAmbrosiaRuntime, attachNeeded)
        {
        }

        async Task
        IJobProxy.JobContinueAsync(System.Int32 p_0,System.Int64 p_1,JobAPI.BoxedDateTime p_2)
        {
			 await JobContinueAsync(p_0,p_1,p_2);
        }

        async Task
        JobContinueAsync(System.Int32 p_0,System.Int64 p_1,JobAPI.BoxedDateTime p_2)
        {
            SerializableTaskCompletionSource rpcTask;
            // Make call, wait for reply
            // Compute size of serialized arguments
            var totalArgSize = 0;

			int arg0Size = 0;
			byte[] arg0Bytes = null;

            // Argument 0
            arg0Bytes = Ambrosia.BinarySerializer.Serialize<System.Int32>(p_0);
arg0Size = IntSize(arg0Bytes.Length) + arg0Bytes.Length;

            totalArgSize += arg0Size;
			int arg1Size = 0;
			byte[] arg1Bytes = null;

            // Argument 1
            arg1Bytes = Ambrosia.BinarySerializer.Serialize<System.Int64>(p_1);
arg1Size = IntSize(arg1Bytes.Length) + arg1Bytes.Length;

            totalArgSize += arg1Size;
			int arg2Size = 0;
			byte[] arg2Bytes = null;

            // Argument 2
            arg2Bytes = Ambrosia.BinarySerializer.Serialize<JobAPI.BoxedDateTime>(p_2);
arg2Size = IntSize(arg2Bytes.Length) + arg2Bytes.Length;

            totalArgSize += arg2Size;

            var wp = this.StartRPC<object>(methodIdentifier: 1 /* method identifier for JobContinue */, lengthOfSerializedArguments: totalArgSize, taskToWaitFor: out rpcTask);
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


            // Serialize arg2
            wp.curLength += wp.PageBytes.WriteInt(wp.curLength, arg2Bytes.Length);
Buffer.BlockCopy(arg2Bytes, 0, wp.PageBytes, wp.curLength, arg2Bytes.Length);
wp.curLength += arg2Bytes.Length;


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

        void IJobProxy.JobContinueFork(System.Int32 p_0,System.Int64 p_1,JobAPI.BoxedDateTime p_2)
        {
            SerializableTaskCompletionSource rpcTask;

            // Compute size of serialized arguments
            var totalArgSize = 0;

            // Argument 0
			int arg0Size = 0;
			byte[] arg0Bytes = null;

            arg0Bytes = Ambrosia.BinarySerializer.Serialize<System.Int32>(p_0);
arg0Size = IntSize(arg0Bytes.Length) + arg0Bytes.Length;

            totalArgSize += arg0Size;
            // Argument 1
			int arg1Size = 0;
			byte[] arg1Bytes = null;

            arg1Bytes = Ambrosia.BinarySerializer.Serialize<System.Int64>(p_1);
arg1Size = IntSize(arg1Bytes.Length) + arg1Bytes.Length;

            totalArgSize += arg1Size;
            // Argument 2
			int arg2Size = 0;
			byte[] arg2Bytes = null;

            arg2Bytes = Ambrosia.BinarySerializer.Serialize<JobAPI.BoxedDateTime>(p_2);
arg2Size = IntSize(arg2Bytes.Length) + arg2Bytes.Length;

            totalArgSize += arg2Size;

            var wp = this.StartRPC<object>(1 /* method identifier for JobContinue */, totalArgSize, out rpcTask, RpcTypes.RpcType.FireAndForget);

            // Serialize arguments


            // Serialize arg0
            wp.curLength += wp.PageBytes.WriteInt(wp.curLength, arg0Bytes.Length);
Buffer.BlockCopy(arg0Bytes, 0, wp.PageBytes, wp.curLength, arg0Bytes.Length);
wp.curLength += arg0Bytes.Length;


            // Serialize arg1
            wp.curLength += wp.PageBytes.WriteInt(wp.curLength, arg1Bytes.Length);
Buffer.BlockCopy(arg1Bytes, 0, wp.PageBytes, wp.curLength, arg1Bytes.Length);
wp.curLength += arg1Bytes.Length;


            // Serialize arg2
            wp.curLength += wp.PageBytes.WriteInt(wp.curLength, arg2Bytes.Length);
Buffer.BlockCopy(arg2Bytes, 0, wp.PageBytes, wp.curLength, arg2Bytes.Length);
wp.curLength += arg2Bytes.Length;


            this.ReleaseBufferAndSend();
            return;
        }

        private object
        JobContinue_ReturnValue(byte[] buffer, int cursor)
        {
            // buffer will be an empty byte array since the method JobContinue returns void
            // so nothing to read, just getting called is the signal to return to the client
            return this;
        }
        async Task
        IJobProxy.MAsync(System.Byte[] p_0)
        {
			 await MAsync(p_0);
        }

        async Task
        MAsync(System.Byte[] p_0)
        {
            SerializableTaskCompletionSource rpcTask;
            // Make call, wait for reply
            // Compute size of serialized arguments
            var totalArgSize = 0;

			int arg0Size = 0;
			byte[] arg0Bytes = null;

            // Argument 0
            arg0Bytes = p_0;
arg0Size = IntSize(arg0Bytes.Length) + arg0Bytes.Length;

            totalArgSize += arg0Size;

            var wp = this.StartRPC<object>(methodIdentifier: 2 /* method identifier for M */, lengthOfSerializedArguments: totalArgSize, taskToWaitFor: out rpcTask);
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

			 await Immortal.TryTakeCheckpointContinuationAsync(currentResult);

			return;
        }

        void IJobProxy.MFork(System.Byte[] p_0)
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

            var wp = this.StartRPC<object>(2 /* method identifier for M */, totalArgSize, out rpcTask, RpcTypes.RpcType.FireAndForget);

            // Serialize arguments


            // Serialize arg0
            wp.curLength += wp.PageBytes.WriteInt(wp.curLength, arg0Bytes.Length);
Buffer.BlockCopy(arg0Bytes, 0, wp.PageBytes, wp.curLength, arg0Bytes.Length);
wp.curLength += arg0Bytes.Length;


            this.ReleaseBufferAndSend();
            return;
        }

        private object
        M_ReturnValue(byte[] buffer, int cursor)
        {
            // buffer will be an empty byte array since the method M returns void
            // so nothing to read, just getting called is the signal to return to the client
            return this;
        }
        async Task
        IJobProxy.PrintBytesReceivedAsync()
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

        void IJobProxy.PrintBytesReceivedFork()
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
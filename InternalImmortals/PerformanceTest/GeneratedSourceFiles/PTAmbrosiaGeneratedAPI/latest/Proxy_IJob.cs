
using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Ambrosia;
using static Ambrosia.StreamCommunicator;


namespace Job
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

            var wp = this.StartRPC<object>(methodIdentifier: 1 /* method identifier for PrintBytesReceived */, lengthOfSerializedArguments: totalArgSize, taskToWaitFor: out rpcTask);
			var asyncContext = new AsyncContext { SequenceNumber = Immortal.CurrentSequenceNumber };

            // Serialize arguments

            int taskId;
			lock (Immortal.DispatchTaskIdQueueLock)
            {
                while (!Immortal.DispatchTaskIdQueue.Data.TryDequeue(out taskId)) { }
            }

            ReleaseBufferAndSend();

			Immortal.StartDispatchLoop();

			var taskToWaitFor = Immortal.CallCache.Data[asyncContext.SequenceNumber].GetAwaitableTaskWithAdditionalInfoAsync();
            var currentResult = await taskToWaitFor;

			while (currentResult.AdditionalInfoType != ResultAdditionalInfoTypes.SetResult)
            {
                switch (currentResult.AdditionalInfoType)
                {
                    case ResultAdditionalInfoTypes.SaveContext:
                        await Immortal.SaveTaskContextAsync();
                        taskToWaitFor = Immortal.CallCache.Data[asyncContext.SequenceNumber].GetAwaitableTaskWithAdditionalInfoAsync();
                        break;
                    case ResultAdditionalInfoTypes.TakeCheckpoint:
                        var sequenceNumber = await Immortal.TakeTaskCheckpointAsync();
                        Immortal.StartDispatchLoop();
                        taskToWaitFor = Immortal.GetTaskToWaitForWithAdditionalInfoAsync(sequenceNumber);
                        break;
                }

                currentResult = await taskToWaitFor;
            }

            lock (Immortal.DispatchTaskIdQueueLock)
            {
                Immortal.DispatchTaskIdQueue.Data.Enqueue(taskId);
            }	
			return;
        }

        void IJobProxy.PrintBytesReceivedFork()
        {
            SerializableTaskCompletionSource rpcTask;

            // Compute size of serialized arguments
            var totalArgSize = 0;


            var wp = this.StartRPC<object>(1 /* method identifier for PrintBytesReceived */, totalArgSize, out rpcTask, RpcTypes.RpcType.FireAndForget);

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
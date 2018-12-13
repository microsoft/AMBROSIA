
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ambrosia;
using static Ambrosia.StreamCommunicator;
using LocalAmbrosiaRuntime;

namespace Job
{
    /// <summary>
    /// This class runs in the process of the object that implements the interface IJob
    /// and communicates with the local Ambrosia runtime.
    /// It is instantiated in ImmortalFactory.CreateServer when a bootstrapper registers a container
    /// that supports the interface IJob.
    /// </summary>
    class IJob_Dispatcher_Implementation : Immortal.Dispatcher
    {
        private readonly IJob instance;
		private readonly ExceptionSerializer exceptionSerializer = new ExceptionSerializer(new List<Type>());

        public IJob_Dispatcher_Implementation(Immortal z, ImmortalSerializerBase myImmortalSerializer, string serviceName, int receivePort, int sendPort, bool setupConnections)
            : base(z, myImmortalSerializer, serviceName, receivePort, sendPort, setupConnections)
        {
            this.instance = (IJob) z;
        }

        public  IJob_Dispatcher_Implementation(Immortal z, ImmortalSerializerBase myImmortalSerializer, string localAmbrosiaRuntime, Type newInterface, Type newImmortalType, int receivePort, int sendPort)
            : base(z, myImmortalSerializer, localAmbrosiaRuntime, newInterface, newImmortalType, receivePort, sendPort)
        {
            this.instance = (IJob) z;
        }

        public override async Task<bool> DispatchToMethod(int methodId, RpcTypes.RpcType rpcType, string senderOfRPC, long sequenceNumber, byte[] buffer, int cursor)
        {
            switch (methodId)
            {
                case 0:
                    // Entry point
                    this.EntryPoint();
                    break;
                case 1:
                    // PrintBytesReceivedAsync
                    {
                        // deserialize arguments
                        // call the method
						byte[] argExBytes = null;
						int argExSize = 0;
						Exception currEx = null;
						int arg0Size = 0;
						byte[] arg0Bytes = null;

						try 
						{
								await this.instance.PrintBytesReceivedAsync();
						}
						catch (Exception ex)
						{
							currEx = ex;
						}

                        if (!rpcType.IsFireAndForget())
                        {
                            // serialize result and send it back (there isn't one)
                            arg0Size = 0;
                            var wp = this.StartRPC_ReturnValue(senderOfRPC, sequenceNumber, currEx == null ? arg0Size : argExSize, currEx == null ? ReturnValueTypes.EmptyReturnValue : ReturnValueTypes.Exception);

                            this.ReleaseBufferAndSend();
                        }
                    }
                    break;
            }

            return true;
        }
    }
}
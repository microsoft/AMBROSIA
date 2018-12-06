
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ambrosia;
using static Ambrosia.StreamCommunicator;
using LocalAmbrosiaRuntime;

namespace IClient
{
    /// <summary>
    /// This class runs in the process of the object that implements the interface IClient
    /// and communicates with the local Ambrosia runtime.
    /// It is instantiated in ImmortalFactory.CreateServer when a bootstrapper registers a container
    /// that supports the interface IClient.
    /// </summary>
    class IClient_Dispatcher_Implementation : Immortal.Dispatcher
    {
        private readonly IClient instance;
		private readonly ExceptionSerializer exceptionSerializer = new ExceptionSerializer(new List<Type>());

        public IClient_Dispatcher_Implementation(Immortal z, ImmortalSerializerBase myImmortalSerializer, string serviceName, int receivePort, int sendPort, bool setupConnections)
            : base(z, myImmortalSerializer, serviceName, receivePort, sendPort, setupConnections)
        {
            this.instance = (IClient) z;
        }

        public  IClient_Dispatcher_Implementation(Immortal z, ImmortalSerializerBase myImmortalSerializer, string localAmbrosiaRuntime, Type newInterface, Type newImmortalType, int receivePort, int sendPort)
            : base(z, myImmortalSerializer, localAmbrosiaRuntime, newInterface, newImmortalType, receivePort, sendPort)
        {
            this.instance = (IClient) z;
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
                    // SendMessageAsync
                    {
                        // deserialize arguments

            // arg0: System.String
            var p_0_ValueLength = buffer.ReadBufferedInt(cursor);
cursor += IntSize(p_0_ValueLength);
var p_0_ValueBuffer = new byte[p_0_ValueLength];
Buffer.BlockCopy(buffer, cursor, p_0_ValueBuffer, 0, p_0_ValueLength);
cursor += p_0_ValueLength;
var p_0 = Ambrosia.BinarySerializer.Deserialize<System.String>(p_0_ValueBuffer);

                        // call the method
						byte[] argExBytes = null;
						int argExSize = 0;
						Exception currEx = null;
						int arg1Size = 0;
						byte[] arg1Bytes = null;

						try 
						{
								await this.instance.SendMessageAsync(p_0);
						}
						catch (Exception ex)
						{
							currEx = ex;
						}

                        if (!rpcType.IsFireAndForget())
                        {
                            // serialize result and send it back (there isn't one)
                            arg1Size = 0;
                            var wp = this.StartRPC_ReturnValue(senderOfRPC, sequenceNumber, currEx == null ? arg1Size : argExSize, currEx == null ? ReturnValueTypes.EmptyReturnValue : ReturnValueTypes.Exception);

                            this.ReleaseBufferAndSend();
                        }
                    }
                    break;
            }

            return true;
        }
    }
}
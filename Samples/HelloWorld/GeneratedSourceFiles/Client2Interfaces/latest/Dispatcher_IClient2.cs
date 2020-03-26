
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ambrosia;
using static Ambrosia.StreamCommunicator;
using LocalAmbrosiaRuntime;

namespace Client2
{
    /// <summary>
    /// This class runs in the process of the object that implements the interface IClient2
    /// and communicates with the local Ambrosia runtime.
    /// It is instantiated in ImmortalFactory.CreateServer when a bootstrapper registers a container
    /// that supports the interface IClient2.
    /// </summary>
    class IClient2_Dispatcher_Implementation : Immortal.Dispatcher
    {
        private readonly IClient2 instance;
		private readonly ExceptionSerializer exceptionSerializer = new ExceptionSerializer(new List<Type>());

        public IClient2_Dispatcher_Implementation(Immortal z, ImmortalSerializerBase myImmortalSerializer, string serviceName, int receivePort, int sendPort, bool setupConnections)
            : base(z, myImmortalSerializer, serviceName, receivePort, sendPort, setupConnections)
        {
            this.instance = (IClient2) z;
        }

        public  IClient2_Dispatcher_Implementation(Immortal z, ImmortalSerializerBase myImmortalSerializer, string localAmbrosiaRuntime, Type newInterface, Type newImmortalType, int receivePort, int sendPort)
            : base(z, myImmortalSerializer, localAmbrosiaRuntime, newInterface, newImmortalType, receivePort, sendPort)
        {
            this.instance = (IClient2) z;
        }

        public override async Task<bool> DispatchToMethod(int methodId, RpcTypes.RpcType rpcType, string senderOfRPC, long sequenceNumber, byte[] buffer, int cursor)
        {
            switch (methodId)
            {
                case 0:
                    // Entry point
                    await this.EntryPoint();
                    break;
                case 1:
                    // ReceiveKeyboardInputAsync
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
								await this.instance.ReceiveKeyboardInputAsync(p_0);
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
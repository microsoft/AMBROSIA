
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ambrosia;
using static Ambrosia.StreamCommunicator;
using LocalAmbrosiaRuntime;

namespace JobAPI
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
                    // JobContinueAsync
                    {
                        // deserialize arguments

            // arg0: System.Int32
            var p_0_ValueLength = buffer.ReadBufferedInt(cursor);
cursor += IntSize(p_0_ValueLength);
var p_0_ValueBuffer = new byte[p_0_ValueLength];
Buffer.BlockCopy(buffer, cursor, p_0_ValueBuffer, 0, p_0_ValueLength);
cursor += p_0_ValueLength;
var p_0 = Ambrosia.BinarySerializer.Deserialize<System.Int32>(p_0_ValueBuffer);


            // arg1: System.Int64
            var p_1_ValueLength = buffer.ReadBufferedInt(cursor);
cursor += IntSize(p_1_ValueLength);
var p_1_ValueBuffer = new byte[p_1_ValueLength];
Buffer.BlockCopy(buffer, cursor, p_1_ValueBuffer, 0, p_1_ValueLength);
cursor += p_1_ValueLength;
var p_1 = Ambrosia.BinarySerializer.Deserialize<System.Int64>(p_1_ValueBuffer);


            // arg2: JobAPI.BoxedDateTime
            var p_2_ValueLength = buffer.ReadBufferedInt(cursor);
cursor += IntSize(p_2_ValueLength);
var p_2_ValueBuffer = new byte[p_2_ValueLength];
Buffer.BlockCopy(buffer, cursor, p_2_ValueBuffer, 0, p_2_ValueLength);
cursor += p_2_ValueLength;
var p_2 = Ambrosia.BinarySerializer.Deserialize<JobAPI.BoxedDateTime>(p_2_ValueBuffer);

                        // call the method
						byte[] argExBytes = null;
						int argExSize = 0;
						Exception currEx = null;
						int arg3Size = 0;
						byte[] arg3Bytes = null;

						try 
						{
								await this.instance.JobContinueAsync(p_0,p_1,p_2);
						}
						catch (Exception ex)
						{
							currEx = ex;
						}

                        if (!rpcType.IsFireAndForget())
                        {
                            // serialize result and send it back (there isn't one)
                            arg3Size = 0;
                            var wp = this.StartRPC_ReturnValue(senderOfRPC, sequenceNumber, currEx == null ? arg3Size : argExSize, currEx == null ? ReturnValueTypes.EmptyReturnValue : ReturnValueTypes.Exception);

                            this.ReleaseBufferAndSend();
                        }
                    }
                    break;
                case 2:
                    // MAsync
                    {
                        // deserialize arguments

            // arg0: System.Byte[]
            var p_0_ValueLength = buffer.ReadBufferedInt(cursor);
cursor += IntSize(p_0_ValueLength);
var p_0_ValueBuffer = new byte[p_0_ValueLength];
Buffer.BlockCopy(buffer, cursor, p_0_ValueBuffer, 0, p_0_ValueLength);
cursor += p_0_ValueLength;
var p_0 = p_0_ValueBuffer;

                        // call the method
						byte[] argExBytes = null;
						int argExSize = 0;
						Exception currEx = null;
						int arg1Size = 0;
						byte[] arg1Bytes = null;

						try 
						{
								await this.instance.MAsync(p_0);
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
                case 3:
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
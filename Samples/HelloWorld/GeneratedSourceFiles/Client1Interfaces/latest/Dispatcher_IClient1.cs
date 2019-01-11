
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ambrosia;
using static Ambrosia.StreamCommunicator;
using LocalAmbrosiaRuntime;

namespace Client1
{
    /// <summary>
    /// This class runs in the process of the object that implements the interface IClient1
    /// and communicates with the local Ambrosia runtime.
    /// It is instantiated in ImmortalFactory.CreateServer when a bootstrapper registers a container
    /// that supports the interface IClient1.
    /// </summary>
    class IClient1_Dispatcher_Implementation : Immortal.Dispatcher
    {
        private readonly IClient1 instance;
		private readonly ExceptionSerializer exceptionSerializer = new ExceptionSerializer(new List<Type>());

        public IClient1_Dispatcher_Implementation(Immortal z, ImmortalSerializerBase myImmortalSerializer, string serviceName, int receivePort, int sendPort, bool setupConnections)
            : base(z, myImmortalSerializer, serviceName, receivePort, sendPort, setupConnections)
        {
            this.instance = (IClient1) z;
        }

        public  IClient1_Dispatcher_Implementation(Immortal z, ImmortalSerializerBase myImmortalSerializer, string localAmbrosiaRuntime, Type newInterface, Type newImmortalType, int receivePort, int sendPort)
            : base(z, myImmortalSerializer, localAmbrosiaRuntime, newInterface, newImmortalType, receivePort, sendPort)
        {
            this.instance = (IClient1) z;
        }

        public override async Task<bool> DispatchToMethod(int methodId, RpcTypes.RpcType rpcType, string senderOfRPC, long sequenceNumber, byte[] buffer, int cursor)
        {
            switch (methodId)
            {
                case 0:
                    // Entry point
                    await this.EntryPoint();
                    break;
            }

            return true;
        }
    }
}
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using Ambrosia;
using SharedAmbrosiaConstants;

namespace Ambrosia
{
    public interface IEmpty { }

    [DataContract]
    internal class EmptyDispatcher : Immortal.Dispatcher
    {
        public EmptyDispatcher(Immortal c, ImmortalSerializerBase myImmortalSerializer, string serviceName, Type newInterface, Type newImmortal, int receivePort, int sendPort)
            : base(c, myImmortalSerializer, serviceName, newInterface, newImmortal, receivePort, sendPort)
        {
        }
        public EmptyDispatcher(Immortal c, ImmortalSerializerBase myImmortalSerializer, string serviceName, int receivePort, int sendPort)
            : base(c, myImmortalSerializer, serviceName, receivePort, sendPort, true)
        {
        }

        public override async Task<bool> DispatchToMethod(int methodId, RpcTypes.RpcType rpcType, string senderOfRPC, long sequenceNumber, byte[] buffer, int cursor)
        {
            switch (methodId)
            {
                case 0:
                    // Entry point
                    await EntryPoint();
                    break;

                default:
                    // should never get called since it was generated from a call to Create for an Empty API
                    throw new NotImplementedException();
            }
            return true;
        }
    }

    public class AmbrosiaFactory
    {
        /// <summary>
        /// Gesture that deploys a (non-upgradeable) service. The result is a service that implements the API defined
        /// in <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The interface that defines the API of the deployed service.</typeparam>
        /// <param name="serviceName"></param>
        /// <param name="instance">The instance to deploy. It must implement <typeparamref name="T"/>.</param>
        /// <param name="receivePort">The port number on which it listens for messages from Ambrosia.</param>
        /// <param name="sendPort">The port number which the service uses to send messages to Ambrosia.</param>
        /// <returns></returns>
        public static IDisposable Deploy<T>(string serviceName, Immortal instance, int receivePort, int sendPort)
        {
            var typeOfT = typeof(T);
            if (!typeOfT.IsInterface)
            {
                throw new ArgumentException($"The type '{typeOfT.Name}' must be an interface.");
            }
            var immortalType = instance.GetType();
            if (!typeof(IEmpty).IsAssignableFrom(typeOfT) && !immortalType.GetInterfaces().Any(i => i.Equals(typeOfT) || i.IsSubclassOf(typeOfT)))
            {
                throw new ArgumentException($"The instance to be deployed is of type '{immortalType.Name}' does not implement the type {typeOfT.Name}.");
            }
            
            // Generate server Ambrosia instance and cache it. Use type parameter T to tell the generation what 
            // interface to generate a proxy for.
            Immortal.Dispatcher serverContainer;

            var serializationClass = typeOfT.Assembly.GetTypes().FirstOrDefault(p => p.IsClass && !p.IsAbstract && typeof(ImmortalSerializerBase).IsAssignableFrom(p));
            var immortalSerializer = serializationClass == null
                ? new Immortal.SimpleImmortalSerializer()
                : (ImmortalSerializerBase)Activator.CreateInstance(serializationClass);

            if (typeof(IEmpty).IsAssignableFrom(typeOfT))
            {
                serverContainer = new EmptyDispatcher(instance, immortalSerializer, serviceName, receivePort, sendPort);
            }
            else
            {
                var containerClass = typeOfT.Assembly.GetType(typeOfT.FullName + "_Dispatcher_Implementation");
                var container = Activator.CreateInstance(containerClass, instance, immortalSerializer, serviceName, receivePort, sendPort, true);
                serverContainer = (Immortal.Dispatcher)container;
            }

            serverContainer.Start();
            return serverContainer;
        }

        /// <summary>
        /// Gesture that deploys an upgradeable service. The result is a service that implements the API defined
        /// in <typeparamref name="T"/>, but when it gets a special upgrade message, turns into a service that
        /// implements the API defined by <typeparamref name="T2"/>.
        /// The upgrade happens by creating an instance of the type <typeparamref name="Z2"/> and passing to its
        /// constructor the existing service instance so it can migrate over any needed state from it.
        /// </summary>
        /// <typeparam name="T">The interface that defines the API of the deployed service.</typeparam>
        /// <typeparam name="T2">The interface that defines the API of the service after it has been upgraded.</typeparam>
        /// <typeparam name="Z2">
        /// The type of the class that implements <typeparamref name="T2"/> and which has a unary
        /// constructor that takes an argument of type <see cref="Immortal"/>.
        /// </typeparam>
        /// <param name="serviceName"></param>
        /// <param name="instance">The instance to deploy. It must implement <typeparamref name="T"/>.</param>
        /// <param name="receivePort">The port number on which it listens for messages from Ambrosia.</param>
        /// <param name="sendPort">The port number which the service uses to send messages to Ambrosia.</param>
        /// <returns></returns>
        public static IDisposable Deploy<T, T2, Z2>(string serviceName, Immortal instance, int receivePort, int sendPort)
            where T2 : T
            where Z2 : Immortal, T2 // *and* Z2 has a ctor that takes a Immortal as a parameter
        {
            var typeOfT = typeof(T);
            if (!typeOfT.IsInterface)
            {
                throw new ArgumentException($"The type '{typeOfT.Name}' must be an interface.");
            }
            if (!typeof(T2).IsInterface)
            {
                throw new ArgumentException($"The type '{typeof(T2).Name}' must be an interface.");
            }
            var immortalType = instance.GetType();
            if (!typeof(IEmpty).IsAssignableFrom(typeOfT) && !immortalType.GetInterfaces().Any(i => i.Equals(typeOfT) || i.IsSubclassOf(typeOfT)))
            {
                throw new ArgumentException($"The instance to be deployed is of type '{immortalType.Name}' does not implement the type {typeOfT.Name}.");
            }

            // This can't go in a type constraint, but Z2 must have a ctor that takes the subtype of Immortal
            // that the instance is (i.e., its dynamic type).
            var ctor = typeof(Z2).GetConstructor(new Type[] { instance.GetType(), });
            if (ctor == null)
            {
                throw new ArgumentException($"The type parameter Z2 was instantiated with the type '{typeof(Z2).Name}' that does not have a public constructor which takes a {typeof(Immortal).Name} as its only parameter.");
            }

            // Generate server Ambrosia instance and cache it. Use type parameter T to tell the generation what 
            // interface to generate a proxy for.
            Immortal.Dispatcher serverContainer;

            var immortalSerializerType = typeOfT.Assembly.GetType($"Ambrosia.ImmortalSerializer");
            var immortalSerializer = (ImmortalSerializerBase)Activator.CreateInstance(immortalSerializerType);

            if (typeof(IEmpty).IsAssignableFrom(typeOfT))
            {
                serverContainer = new EmptyDispatcher(instance, immortalSerializer, serviceName, typeof(T2), typeof(Z2), receivePort, sendPort);
            }
            else
            {
                var containerClass = typeOfT.Assembly.GetType(typeOfT.FullName + "_Dispatcher_Implementation");
                var container = Activator.CreateInstance(containerClass, instance, immortalSerializer, serviceName, typeof(T2), typeof(Z2), receivePort, sendPort);
                serverContainer = (Immortal.Dispatcher)container;
            }

            serverContainer.Start();

            return serverContainer;

        }
    }
}
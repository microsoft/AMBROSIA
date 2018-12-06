using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace Ambrosia
{
    public static class BinarySerializer
    {
        public static List<Type> KnownTypes = new List<Type> {typeof(DataContractSerializedExceptionResult), typeof(ClassicallySerializedExceptionResult) };

        public static byte[] Serialize<T>(T obj)
        {
            var serializer = new DataContractSerializer(typeof(T), KnownTypes);
            var stream = new MemoryStream();
            using (var writer = XmlDictionaryWriter.CreateBinaryWriter(stream))
            {
                serializer.WriteObject(writer, obj);
            }

            return stream.ToArray();
        }

        public static T Deserialize<T>(byte[] data)
        {
            return (T)Deserialize(typeof(T), data);
        }

        public static object Deserialize(Type type, byte[] data)
        {
            var serializer = new DataContractSerializer(type, KnownTypes);
            using (var stream = new MemoryStream(data))
            using (var reader = XmlDictionaryReader.CreateBinaryReader(stream, XmlDictionaryReaderQuotas.Max))
            {
                return serializer.ReadObject(reader);
            }
        }
    }
}
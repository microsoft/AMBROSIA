
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Xml;

using Ambrosia;
using static Ambrosia.StreamCommunicator;
using DashboardAPI;

namespace Ambrosia
{
    /// <summary>
    /// This class is the serializer that supports serialization of a Immortal and has the generated classes as a known types
    /// </summary>]
    public class ImmortalSerializer : ImmortalSerializerBase
    {		
		public ImmortalSerializer()
		{
			base.KnownTypes = new SerializableType[] 
			{
				new SerializableType(typeof(IDashboardProxy_Implementation)),
				new SerializableType(this.GetType())
			};
		}

        public override long SerializeSize(Immortal c)
        {
            var serializer = new DataContractSerializer(c.GetType(), this.KnownTypes.Select(kt => kt.Type).ToArray());
            long retVal = -1;
            using (var countStream = new CountStream())
            {
                using (var writer = XmlDictionaryWriter.CreateBinaryWriter(countStream))
                {
                    serializer.WriteObject(writer, c);
                }
                retVal = countStream.Length;
            }
            return retVal;
        }

        public override void Serialize(Immortal c, Stream writeToStream)
        {
            // nned to create
            var serializer = new DataContractSerializer(c.GetType(), this.KnownTypes.Select(kt => kt.Type).ToArray());
            using (var writer = XmlDictionaryWriter.CreateBinaryWriter(writeToStream))
            {
                serializer.WriteObject(writer, c);
            }
        }

		public override Immortal Deserialize(Type runtimeType, Stream stream)
        {
            var serializer = new DataContractSerializer(runtimeType, this.KnownTypes.Select(kt => kt.Type).ToArray());
            using (var reader = XmlDictionaryReader.CreateBinaryReader(stream, XmlDictionaryReaderQuotas.Max))
            {
                return (Immortal)serializer.ReadObject(reader);
            }
        }
    }

	public interface Empty : IEmpty 
	{

	}
}
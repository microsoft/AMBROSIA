using System;
using System.Runtime.Serialization;

namespace Ambrosia
{
    [DataContract]
    public class SerializableType
    {
        [DataMember] public string TypeFullName { get; set; }

        public Type Type { get; set; }

        public SerializableType(Type type)
        {
            this.Type = type;
        }

        [OnSerializing]
        private void SetTypeFullNameOnSerializing(StreamingContext context)
        {
            if (this.Type != null)
            {
                this.TypeFullName = this.Type.FullName;
            }
        }

        [OnDeserialized]
        private void GetTypeOnDeserialized(StreamingContext context)
        {
            if (this.TypeFullName != null)
            {
                this.Type = Type.GetType(this.TypeFullName);
            }
        }
    }
}

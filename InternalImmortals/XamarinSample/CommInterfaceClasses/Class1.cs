using System;
using System.Runtime.Serialization;

namespace CommInterfaceClasses
{
    [DataContract]
    public class Item
    {
        [DataMember]
        public string Id { get; set; }
        [DataMember]
        public string Text { get; set; }
        [DataMember]
        public string Description { get; set; }
    }
}

using System;
using System.Runtime.Serialization;
namespace JobAPI {
[DataContractAttribute]
public struct BoxedDateTime
{
    [DataMemberAttribute]
    public DateTime val;
}
}

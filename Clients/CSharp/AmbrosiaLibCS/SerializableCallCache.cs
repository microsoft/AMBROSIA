using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Ambrosia
{
    [DataContract]
    [KnownType(typeof(SerializableTaskCompletionSource))]
    public class SerializableCallCache : SerializableCache<long, SerializableTaskCompletionSource>
    {
       
    }
}

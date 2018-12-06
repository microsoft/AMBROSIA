using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Ambrosia
{
    [DataContract]
    public class SerializableCache<TKey, TValue>
    {
        public ConcurrentDictionary<TKey, TValue> Data { get; private set; }

        [DataMember]
        public List<Tuple<TKey, TValue>> SerializedData { get; set; }

        public SerializableCache()
        {
            this.Data = new ConcurrentDictionary<TKey, TValue>();
        }

        [OnSerializing]
        public void SetSerializedCallCache(StreamingContext context)
        {
            this.SerializedData = new List<Tuple<TKey, TValue>>(
                this.Data.Select(kvp => new Tuple<TKey, TValue>(kvp.Key, kvp.Value)));
        }

        [OnDeserialized]
        public void SetCallCache(StreamingContext context)
        {
            if (this.SerializedData != null)
            {
                this.Data = new ConcurrentDictionary<TKey, TValue>();
                foreach (var tup in this.SerializedData)
                {
                    this.Data.AddOrUpdate(tup.Item1, tup.Item2, (k, v) => v);
                }
            }
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Ambrosia
{
    [DataContract]
    public class SerializableQueue<T>
    {
        public ConcurrentQueue<T> Data { get; private set; }

        [DataMember]
        public List<T> SerializedData { get; set; }

        public SerializableQueue()
        {
            this.Data = new ConcurrentQueue<T>();
        }

        [OnSerializing]
        public void SetSerializedQueue(StreamingContext context)
        {
            this.SerializedData = new List<T>();
            while (!this.Data.IsEmpty)
            {
                if (this.Data.TryDequeue(out var next))
                {
                    this.SerializedData.Add(next);
                }
            }

            foreach (var next in this.SerializedData)
            {
                this.Data.Enqueue(next);
            }
        }

        [OnDeserialized]
        public void SetQueue(StreamingContext context)
        {
            if (this.SerializedData != null)
            {
                this.Data = new ConcurrentQueue<T>();
                foreach (var next in this.SerializedData)
                {
                    this.Data.Enqueue(next);
                }
            }
        }
    }
}
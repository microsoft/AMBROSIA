using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks.Dataflow;

namespace Ambrosia
{
    [DataContract]
    public class SerializableBufferBlock<T>
    {
        public BufferBlock<T> Buffer { get; }

        [DataMember]
        public T[] BufferState { get; set; }

        public SerializableBufferBlock()
        {
            this.Buffer = new BufferBlock<T>();
        }

        [OnDeserializing]
        public void SetBufferBlock(StreamingContext context)
        {
            if (this.BufferState != null)
            {
                foreach (var item in this.BufferState)
                {
                    this.Buffer.Post(item);
                }
            }
        }

        [OnSerializing]
        public void SetBufferState(StreamingContext context)
        {
            if (this.Buffer.TryReceiveAll(out var items))
            {
                this.BufferState = items.ToArray();

                foreach (var item in this.BufferState)
                {
                    this.Buffer.Post(item);
                }
            }
        }
    }
}
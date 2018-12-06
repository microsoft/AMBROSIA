using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Ambrosia
{
    [DataContract]
    public class SerializableException
    {
        [DataMember] public object SerializedException { get; set; }

        [DataMember] public string ExceptionSource { get; set; }

        public Exception Exception { get; set; }
        

        private readonly ExceptionSerializer _exceptionSerializer = new ExceptionSerializer(new List<Type>());

        public SerializableException(object serializedException) : this(serializedException, null)
        {
        }

        public SerializableException(object serializedException, string exceptionSource = null)
        {
            this._exceptionSerializer.Deserialize(serializedException, out var exception);
            this.ExceptionSource = exceptionSource;
            var exceptionMessage = exceptionSource == null
                ? "Local Ambrosia exception thrown."
                : $"Remote Ambrosia exception thrown by {exceptionSource}";
            this.Exception = new AmbrosiaException(exceptionMessage, exception);
        }

        [OnSerializing]
        public void SetException(StreamingContext context)
        {
            if (this.Exception != null)
            {
                this.SerializedException = this._exceptionSerializer.Serialize(this.Exception);
            }
        }

        [OnDeserialized]
        public void SetSerializableException(StreamingContext context)
        {
            if (this.SerializedException != null)
            {
                this._exceptionSerializer.Deserialize(this.SerializedException, out var ex);
                this.Exception = ex;
            }
        }
    }

    public class AmbrosiaException : Exception
    {
        public AmbrosiaException()
        {
        }

        public AmbrosiaException(string message)
            : base(message)
        {
        }

        public AmbrosiaException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Ambrosia
{
    public class ExceptionSerializer
    {
        private readonly DataContractSerializer _exceptionSerializer;

        public ExceptionSerializer(IEnumerable<Type> knownTypes)
        {
            this._exceptionSerializer = new DataContractSerializer(typeof(Exception), knownTypes);
        }

        public object Serialize(Exception e)
        {
            // does best-effort on exception serialization
            // if exception cannot be serialized successfully, we will serialize
            // just the description string
            try
            {
                using (var stream = new MemoryStream())
                {
                    this._exceptionSerializer.WriteObject(stream, e);
                    return new DataContractSerializedExceptionResult { Content = stream.ToArray() };
                }
            }
            catch
            {
                try
                {
                    using (var stream = new MemoryStream())
                    {
                        IFormatter formatter = new BinaryFormatter();
                        formatter.Serialize(stream, e);
                        stream.Flush();
                        return new ClassicallySerializedExceptionResult { Content = stream.ToArray() };
                    }
                }
                catch
                {
                    return new NonserializedExceptionResult { Description = e.ToString() };
                }
            }
        }

        public bool Deserialize(Object o, out Exception e)
        {
            if (o == null || !(o is ExceptionResult er))
            {
                e = null;
                return false;
            }

            if (o is DataContractSerializedExceptionResult dse)
            {
                try
                {
                    var stream = new MemoryStream(dse.Content);
                    e = (Exception)_exceptionSerializer.ReadObject(stream);
                    return true;
                }
                catch
                {
                }
            }
            else if (o is ClassicallySerializedExceptionResult cse)
            {
                try
                {
                    var stream = new MemoryStream(cse.Content);
                    IFormatter formatter = new BinaryFormatter();
                    e = (Exception)formatter.Deserialize(stream);
                    return true;
                }
                catch
                {
                }
            }

            e = new ApplicationException($"the application threw: {er.Description}");
            return true;
        }
    }


    [Serializable]
    public class ApplicationException : Exception
    {
        public ApplicationException(string msg) : base(msg) { }
        public ApplicationException(string msg, Exception inner) : base(msg, inner) { }
    }

    [DataContract]
    public abstract class ExceptionResult
    {
        [DataMember]
        public string Description;
    }

    [DataContract]
    public class DataContractSerializedExceptionResult : ExceptionResult
    {
        [DataMember]
        public byte[] Content;
    }

    [DataContract]
    public class ClassicallySerializedExceptionResult : ExceptionResult
    {
        [DataMember]
        public byte[] Content;
    }

    [DataContract]
    public class NonserializedExceptionResult : ExceptionResult
    {
    }
}

using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace JobAPI
{
    [DataContract]
    public struct BoxedDateTime {[DataMember] public DateTime val; }

    // Hand-written Ambrosia interface.
    // Written in C# for now. Could be a new IDL or re-use Protobuf or whatever.
    // Should also indicate the data serialization format for each parameter and return value.
    public interface IJob
    {
        void JobContinue(int numRPCBytes,
                         long rep,
                         BoxedDateTime startTimeOfRound);
        void M(byte[] arg);
        void PrintBytesReceived();
    }
}

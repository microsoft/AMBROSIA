using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ambrosia
{
    public class Serializer
    {
        private const int messageTypeSize = 1;

        public static void SerializeAncestorMessage(Stream stream, byte messageType, long[] ancestors)
        {
            var numAncestors = ancestors.Length;
            var messageSize = messageTypeSize + StreamCommunicator.LongSize(numAncestors);
            foreach (var ancestor in ancestors)
            {
                messageSize += StreamCommunicator.LongSize(ancestor);
            }

            // Write message size
            stream.WriteInt(messageSize);
            // Write message type
            stream.WriteByte(messageType);
            // Write number of ancestors
            stream.WriteInt(numAncestors);
            // Write ancestors
            foreach(var ancestor in ancestors)
            {
                stream.WriteLong(ancestor);
            }
        }

        public static async Task<long[]> DeserializeAncestorMessageAsync(Stream stream, CancellationToken ct)
        {
            var inputFlexBuffer = new FlexReadBuffer();
            await FlexReadBuffer.DeserializeAsync(stream, inputFlexBuffer, ct);
            var sizeBytes = inputFlexBuffer.LengthLength;
            // Get the seqNo of the replay/filter point
            var offset = messageTypeSize + sizeBytes;
            var numAncestors = StreamCommunicator.ReadBufferedInt(inputFlexBuffer.Buffer, offset);
            offset += StreamCommunicator.IntSize(numAncestors);
            var ancestors = new long[numAncestors];
            for (int i = 0; i < numAncestors; i++)
            {
                ancestors[i] = StreamCommunicator.ReadBufferedLong(inputFlexBuffer.Buffer, offset);
                offset += StreamCommunicator.LongSize(ancestors[i]);
            }
            return ancestors;
        }

        public static void SerializeReplayMessage(Stream stream,
                                                  byte messageType,
                                                  long lastProcessedID,
                                                  long lastProcessedReplayableID,
                                                  ConcurrentDictionary<long, ConcurrentDictionary<long, Tuple<long, long>>> ancestorsToIDs)
        {
            var dictCount = ancestorsToIDs.Count;
            var messageSize = messageTypeSize + StreamCommunicator.LongSize(lastProcessedID) +
                              StreamCommunicator.LongSize(lastProcessedReplayableID) +
                              StreamCommunicator.LongSize(dictCount);

            foreach (var peerID in ancestorsToIDs.Keys)
            {
                messageSize += StreamCommunicator.LongSize(peerID);
                messageSize += StreamCommunicator.LongSize(ancestorsToIDs[peerID].Count);
                foreach (var shardID in ancestorsToIDs[peerID].Keys)
                {
                    messageSize += StreamCommunicator.LongSize(shardID);
                    messageSize += StreamCommunicator.LongSize(ancestorsToIDs[peerID][shardID].Item1 + 1);
                    messageSize += StreamCommunicator.LongSize(ancestorsToIDs[peerID][shardID].Item2 + 1);
                }
            }

            // Write message size
            stream.WriteInt(messageSize);
            // Write message type
            stream.WriteByte(messageType);
            // Write the output filter seqNo for the other side
            stream.WriteLong(lastProcessedID);
            stream.WriteLong(lastProcessedReplayableID);

            // For the sharded case, send replay values for other shards
            stream.WriteInt(dictCount);
            foreach (var peerID in ancestorsToIDs.Keys)
            {
                stream.WriteLong(peerID);
                stream.WriteInt(ancestorsToIDs[peerID].Count);
                foreach (var shardID in ancestorsToIDs[peerID].Keys)
                {
                    stream.WriteLong(shardID);
                    stream.WriteLong(ancestorsToIDs[peerID][shardID].Item1);
                    stream.WriteLong(ancestorsToIDs[peerID][shardID].Item2);
                }
            }
        }

        public static async Task<Tuple<long, long, ConcurrentDictionary<long, ConcurrentDictionary<long, Tuple<long, long>>>>> DeserializeReplayMessageAsync(Stream stream, CancellationToken ct)
        {
            var inputFlexBuffer = new FlexReadBuffer();
            await FlexReadBuffer.DeserializeAsync(stream, inputFlexBuffer, ct);
            var sizeBytes = inputFlexBuffer.LengthLength;
            // Get the seqNo of the replay/filter point
            var offset = messageTypeSize + sizeBytes;
            var lastProcessedID = StreamCommunicator.ReadBufferedLong(inputFlexBuffer.Buffer, offset);
            offset += StreamCommunicator.LongSize(lastProcessedID);
            var lastProcessedReplayableID = StreamCommunicator.ReadBufferedLong(inputFlexBuffer.Buffer, offset);
            offset += StreamCommunicator.LongSize(lastProcessedReplayableID);
            var numPeers = StreamCommunicator.ReadBufferedInt(inputFlexBuffer.Buffer, offset);
            offset += StreamCommunicator.IntSize(numPeers);
            var ancestorsToIDs = new ConcurrentDictionary<long, ConcurrentDictionary<long, Tuple<long, long>>>();
            for (int i = 0; i < numPeers; i++)
            {
                long peerID = StreamCommunicator.ReadBufferedLong(inputFlexBuffer.Buffer, offset);
                offset += StreamCommunicator.LongSize(peerID);
                int numShards = StreamCommunicator.ReadBufferedInt(inputFlexBuffer.Buffer, offset);
                offset += StreamCommunicator.IntSize(numShards);
                ancestorsToIDs[peerID] = new ConcurrentDictionary<long, Tuple<long, long>>();
                for (int j = 0; j < numShards; j++)
                {
                    long shardID = StreamCommunicator.ReadBufferedLong(inputFlexBuffer.Buffer, offset);
                    offset += StreamCommunicator.LongSize(shardID);
                    long shardLastProcessedID = StreamCommunicator.ReadBufferedLong(inputFlexBuffer.Buffer, offset);
                    offset += StreamCommunicator.LongSize(shardLastProcessedID);
                    long shardLastProcessedReplayableID = StreamCommunicator.ReadBufferedLong(inputFlexBuffer.Buffer, offset);
                    offset += StreamCommunicator.LongSize(shardLastProcessedReplayableID);
                    ancestorsToIDs[peerID][shardID] = new Tuple<long, long>(shardLastProcessedID, shardLastProcessedReplayableID);
                }
            }
            inputFlexBuffer.ResetBuffer();
            return Tuple.Create(lastProcessedID, lastProcessedReplayableID, ancestorsToIDs);
        }

        public static void SerializeShardTrimMessage(Stream stream, byte messageType)
        {
            var messageSize = messageTypeSize;

            // Write message size
            stream.WriteInt(messageSize);

            // Write message type
            stream.WriteByte(messageType);
        }

        public static async Task DeserializeShardTrimMessageAsync(Stream stream, CancellationToken ct)
        {
            var inputFlexBuffer = new FlexReadBuffer();
            await FlexReadBuffer.DeserializeAsync(stream, inputFlexBuffer, ct);
        }
    }
}

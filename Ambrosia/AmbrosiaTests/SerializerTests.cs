using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ambrosia;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace AmbrosiaTests
{
    [TestClass]
    public class SerializerTests
    {
        [TestMethod]
        public void TestNonShardReplayMessage()
        {
            long expectedLastProcessedID = 14;
            long expectedLastProcessedReplayableID = 9;
            var ancestorsToIDs = new ConcurrentDictionary<long, ConcurrentDictionary<long, Tuple<long, long>>>();

            using (var stream = new MemoryStream())
            {
                Serializer.SerializeReplayMessage(
                    stream,
                    AmbrosiaRuntime.replayFromByte,
                    expectedLastProcessedID,
                    expectedLastProcessedReplayableID,
                    ancestorsToIDs
                );
                stream.Flush();
                stream.Position = 0;
                var result = Serializer.DeserializeReplayMessageAsync(stream, new CancellationToken()).GetAwaiter().GetResult();
                Assert.AreEqual(expectedLastProcessedID, result.Item1);
                Assert.AreEqual(expectedLastProcessedReplayableID, result.Item2);
                var actualShardToLastID = result.Item3;
                Assert.AreEqual(actualShardToLastID.Count, 0);
            }
        }

        [TestMethod]
        public void TestShardReplayMessage()
        {
            long expectedLastProcessedID = 14;
            long expectedLastProcessedReplayableID = 9;
            var ancestorsToIDs = new ConcurrentDictionary<long, ConcurrentDictionary<long, Tuple<long, long>>>();
            ancestorsToIDs[2] = new ConcurrentDictionary<long, Tuple<long, long>>();
            ancestorsToIDs[2][1] = new Tuple<long, long>(11, 7);
            ancestorsToIDs[2][5] = new Tuple<long, long>(9, 4);
            ancestorsToIDs[4] = new ConcurrentDictionary<long, Tuple<long, long>>();
            ancestorsToIDs[4][3] = new Tuple<long, long>(8, 6);
            ancestorsToIDs[4][7] = new Tuple<long, long>(10, 5);

            using (var stream = new MemoryStream())
            {
                Serializer.SerializeReplayMessage(
                    stream,
                    AmbrosiaRuntime.replayFromByte,
                    expectedLastProcessedID,
                    expectedLastProcessedReplayableID,
                    ancestorsToIDs
                );
                stream.Flush();
                stream.Position = 0;
                var result = Serializer.DeserializeReplayMessageAsync(stream, new CancellationToken()).GetAwaiter().GetResult();
                Assert.AreEqual(expectedLastProcessedID, result.Item1);
                Assert.AreEqual(expectedLastProcessedReplayableID, result.Item2);
                var actualShardToLastID = result.Item3;
                Assert.AreEqual(actualShardToLastID.Count, ancestorsToIDs.Count);

                foreach (var peerID in ancestorsToIDs.Keys)
                {
                    Assert.IsTrue(actualShardToLastID.ContainsKey(peerID));
                    Assert.AreEqual(actualShardToLastID[peerID].Count, ancestorsToIDs[peerID].Count);
                    foreach (var shardID in ancestorsToIDs[peerID].Keys)
                    {
                        Assert.IsTrue(actualShardToLastID[peerID].ContainsKey(shardID));
                        Assert.AreEqual(actualShardToLastID[peerID][shardID], ancestorsToIDs[peerID][shardID]);
                    }
                }
            }
        }
    }
}

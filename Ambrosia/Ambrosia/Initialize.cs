using CRA.ClientLibrary;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Ambrosia
{
    public class AmbrosiaRuntimeParams
    {
        public int serviceReceiveFromPort;
        public int serviceSendToPort;
        public string serviceName;
        public string AmbrosiaBinariesLocation;
        public string serviceLogPath;
        public bool? createService;
        public bool pauseAtStart;
        public bool persistLogs;
        public bool activeActive;
        public long logTriggerSizeMB;
        public string storageConnectionString;
        public long currentVersion;
        public long upgradeToVersion;
        public long shardID;
        public string oldShards;
        public string newShards;
    }

    public static class AmbrosiaRuntimeParms
    {
        public static bool _looseAttach = false;
    }

    public class AmbrosiaNonShardedRuntime : VertexBase
    {
        private AmbrosiaRuntime Runtime { get; set; }
        public AmbrosiaNonShardedRuntime()
        {
            Runtime = new AmbrosiaRuntime();
        }

        private long[] ParseLongs(string s)
        {
            if (s.Trim() == "")
            {
                return new long[] { };
            }
            string[] shards = s.Split(',');
            long[] ids = new long[shards.Length];

            for (int i = 0; i < shards.Length; i++)
            {
                ids[i] = long.Parse(shards[i]);
            }
            return ids;

        }

        public override async Task InitializeAsync(object param)
        {
            // Workaround because of parameter type limitation in CRA
            AmbrosiaRuntimeParams p = new AmbrosiaRuntimeParams();
            XmlSerializer xmlSerializer = new XmlSerializer(p.GetType());
            using (StringReader textReader = new StringReader((string)param))
            {
                p = (AmbrosiaRuntimeParams)xmlSerializer.Deserialize(textReader);
            }

            bool sharded = false;

            Runtime.Initialize(
                p.serviceReceiveFromPort,
                p.serviceSendToPort,
                p.serviceName,
                p.serviceLogPath,
                p.createService,
                p.pauseAtStart,
                p.persistLogs,
                p.activeActive,
                p.logTriggerSizeMB,
                p.storageConnectionString,
                p.currentVersion,
                p.upgradeToVersion,
                ClientLibrary,
                AddAsyncInputEndpoint,
                AddAsyncOutputEndpoint,
                ParseLongs(p.oldShards),
                ParseLongs(p.newShards)
            );

            return;
        }
    }

    public class AmbrosiaShardedRuntime : ShardedVertexBase
    {
        private AmbrosiaRuntime Runtime { get; set; }
        public AmbrosiaShardedRuntime()
        {
            Runtime = new AmbrosiaRuntime();
        }

        public override async Task InitializeAsync(int shardId, ShardingInfo shardingInfo, object param)
        {
            // Workaround because of parameter type limitation in CRA
            AmbrosiaRuntimeParams p = new AmbrosiaRuntimeParams();
            XmlSerializer xmlSerializer = new XmlSerializer(p.GetType());
            using (StringReader textReader = new StringReader((string)param))
            {
                p = (AmbrosiaRuntimeParams)xmlSerializer.Deserialize(textReader);
            }

            bool sharded = true;

            Runtime.Initialize(
                p.serviceReceiveFromPort,
                p.serviceSendToPort,
                p.serviceName,
                p.serviceLogPath,
                p.createService,
                p.pauseAtStart,
                p.persistLogs,
                p.activeActive,
                p.logTriggerSizeMB,
                p.storageConnectionString,
                p.currentVersion,
                p.upgradeToVersion,
                ClientLibrary,
                AddAsyncInputEndpoint,
                AddAsyncOutputEndpoint,
                new long[0],
                new long[0]
            );

            return;
        }
    }
}

using CRA.ClientLibrary;
using System.IO;
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
        public long[] oldShards;
        public long[] newShards;
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

        public override void Initialize(object param)
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
                sharded,
                ClientLibrary,
                AddAsyncInputEndpoint,
                AddAsyncOutputEndpoint
            );
        }
    }

    public class AmbrosiaShardedRuntime : ShardedVertexBase
    {
        private AmbrosiaRuntime Runtime { get; set; }
        public AmbrosiaShardedRuntime() {
            Runtime = new AmbrosiaRuntime();
        }

        public override void Initialize(int shardId, ShardingInfo shardingInfo, object vertexParameter)
        {
            // Workaround because of parameter type limitation in CRA
            AmbrosiaRuntimeParams p = new AmbrosiaRuntimeParams();
            XmlSerializer xmlSerializer = new XmlSerializer(p.GetType());
            using (StringReader textReader = new StringReader((string)vertexParameter))
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
                sharded,
                ClientLibrary,
                AddAsyncInputEndpoint,
                AddAsyncOutputEndpoint
            );
        }
    }
}

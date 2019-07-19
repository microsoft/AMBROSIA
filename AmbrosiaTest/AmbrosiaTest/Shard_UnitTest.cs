using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Configuration;
using System.Threading;

namespace AmbrosiaTest
{
    [TestClass]
    public class Shard_UnitTest
    {
        //************* Init Code *****************
        // NOTE: Need this bit of code at the top of every "[TestClass]" (per .cs test file) to get context \ details of the current test running
        // NOTE: Make sure all names be "Azure Safe". No capital letters and no underscore.
        [TestInitialize()]
        public void Initialize()
        {
            Utilities MyUtils = new Utilities();
            MyUtils.TestInitialize();
        }
        //************* Init Code *****************

        [TestMethod]
        public void Shard_UnitTest_BasicEndtoEnd_Test()
        {
            // Test that one shard per server and client works
            string testName = "shardunitendtoendtest";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "1073741824";

            Utilities MyUtils = new Utilities();

            // AMB1 - Job
            string logOutputFileName_AMB1 = testName + "_AMB1.log";
            AMB_Settings AMB1 = new AMB_Settings
            {
                AMB_ServiceName = clientJobName,
                AMB_PortAppReceives = "1000",
                AMB_PortAMBSends = "1001",
                AMB_ServiceLogPath = ambrosiaLogDir,
                AMB_CreateService = "A",
                AMB_PauseAtStart = "N",
                AMB_PersistLogs = "Y",
                AMB_NewLogTriggerSize = "1000",
                AMB_ActiveActive = "N",
                AMB_Version = "0",
                AMB_ShardID = "1",
            };
            MyUtils.CallAMB(AMB1, logOutputFileName_AMB1, AMB_ModeConsts.RegisterInstance);

            // AMB2 - Shard 1
            string logOutputFileName_AMB2 = testName + "_AMB2.log";
            AMB_Settings AMB2 = new AMB_Settings
            {
                AMB_ServiceName = serverName,
                AMB_PortAppReceives = "2000",
                AMB_PortAMBSends = "2001",
                AMB_ServiceLogPath = ambrosiaLogDir,
                AMB_CreateService = "A",
                AMB_PauseAtStart = "N",
                AMB_PersistLogs = "Y",
                AMB_NewLogTriggerSize = "1000",
                AMB_ActiveActive = "N",
                AMB_Version = "0",
                AMB_ShardID = "1",
            };
            MyUtils.CallAMB(AMB2, logOutputFileName_AMB2, AMB_ModeConsts.RegisterInstance);

            // ImmCoord1
            string logOutputFileName_ImmCoord1 = testName + "_ImmCoord1.log";
            int ImmCoordProcessID1 = MyUtils.StartImmCoord(clientJobName, 1500, logOutputFileName_ImmCoord1, shardID: 1);

            // ImmCoord2
            string logOutputFileName_ImmCoord2 = testName + "_ImmCoord2.log";
            int ImmCoordProcessID2 = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2, shardID: 1);

            // Client
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "1024", "1", logOutputFileName_ClientJob);

            // Give it a few seconds to start
            Thread.Sleep(2000);

            // Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false);

            // Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 5, false, testName, true); // Number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server, byteSize, 5, false, testName, true);

            // Stop things to file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(serverProcessID);
            MyUtils.KillProcess(ImmCoordProcessID1);
            MyUtils.KillProcess(ImmCoordProcessID2);

            // Verify AMB
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_AMB1);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_AMB2);

            // Verify Client
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);

            // Verify Server
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server);

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version, shardID: 1);
        }
/*
        [TestMethod]
        public void Shard_UnitTest_MultiShardEndToEnd_Test()
        {
            // Test that multi-shards per server and client behaves the same as the single shard case
            string testName = "shardunitendtoendtest";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "1073741824";

            Utilities MyUtils = new Utilities();

            // AMB1 - Job
            string logOutputFileName_AMB1 = testName + "_AMB1.log";
            AMB_Settings AMB1 = new AMB_Settings
            {
                AMB_ServiceName = clientJobName,
                AMB_PortAppReceives = "1000",
                AMB_PortAMBSends = "1001",
                AMB_ServiceLogPath = ambrosiaLogDir,
                AMB_CreateService = "A",
                AMB_PauseAtStart = "N",
                AMB_PersistLogs = "Y",
                AMB_NewLogTriggerSize = "1000",
                AMB_ActiveActive = "N",
                AMB_Version = "0"
            };
            MyUtils.CallAMB(AMB1, logOutputFileName_AMB1, AMB_ModeConsts.RegisterInstance);

            int numShards = 3;
            string[] logOutputFileName_AMB = new string[numShards];
            for (int i = 0; i < numShards; i++)
            {
                int shard_id = i + 1;
                logOutputFileName_AMB[i] = testName + shard_id.ToString() + "_AMB.log";
                string portPrefix = (i + 2).ToString();
                AMB_Settings AMB_shard = new AMB_Settings
                {
                    AMB_ServiceName = serverName,
                    AMB_PortAppReceives = portPrefix + "000",
                    AMB_PortAMBSends = portPrefix + "001",
                    AMB_ServiceLogPath = ambrosiaLogDir,
                    AMB_CreateService = "A",
                    AMB_PauseAtStart = "N",
                    AMB_PersistLogs = "Y",
                    AMB_NewLogTriggerSize = "1000",
                    AMB_ActiveActive = "N",
                    AMB_Version = "0",
                    AMB_ShardID = shard_id.ToString(),
                };
                MyUtils.CallAMB(AMB_shard, logOutputFileName_AMB[i], AMB_ModeConsts.RegisterInstance);
            }

            string logOutputFileName_ImmCoord1 = testName + "_ImmCoord1.log";
            int ImmCoordProcessID1 = MyUtils.StartImmCoord(clientJobName, 1500, logOutputFileName_ImmCoord1);

            int[] immCoordProcesses = new int[numShards];
            for (int i = 0; i < numShards; i++)
            {
                int shard_id = i + 1;
                int port = (i + 2) * 1000 + 500;
                string logOutputFileName_ImmCoord = testName + shard_id.ToString() + "_ImmCoord.log";
                immCoordProcesses[i] = MyUtils.StartImmCoord(serverName, port, logOutputFileName_ImmCoord);
            }

            // Client Job Call
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "1024", "1", logOutputFileName_ClientJob);

            // Give it a few seconds to start
            Thread.Sleep(2000);

            int[] serverProcesses = new int[numShards];
            string[] logOutputFileName_Server = new string[numShards];
            for (int i = 0; i < numShards; i++)
            {
                int shard_id = i + 1;
                string portPrefix = (i + 2).ToString();
                logOutputFileName_Server[i] = testName + shard_id.ToString() + "_Server.log";
                serverProcesses[i] = MyUtils.StartPerfServer(portPrefix + "001", portPrefix + "000", clientJobName, serverName, logOutputFileName_Server[i], 1, false);
            }

            // Delay until client is done - also check servers just to make sure
            MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 5, false, testName, true); // number of bytes processed
            for (int i = 0; i < numShards; i++)
            {
                MyUtils.WaitForProcessToFinish(logOutputFileName_Server[i], byteSize, 5, false, testName, true);
            }

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(ImmCoordProcessID1);
            for (int i = 0; i < numShards; i++)
            {
                MyUtils.KillProcess(immCoordProcesses[i]);
                MyUtils.KillProcess(serverProcesses[i]);
            }

            // Verify AMB
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_AMB1);
            for (int i = 0; i < numShards; i++)
            {
                MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_AMB[i]);
            }

            // Verify Client
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);

            // Verify Server
            for (int i = 0; i < numShards; i++)
            {
                MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server[i]);
            }

            // Verify integrity of Ambrosia logs by replaying
            // TODO: Need to add shard logic
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }*/
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Configuration;
using System.Threading;
using System.Windows.Forms;

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
        [TestMethod]
        public void Shard_UnitTest_SingleReshardEndtoEnd_Test()
        {
            // Test that one shard per server and client works
            string testName = "shardunitsinglereshardendtoendtest";
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

            // AMB 3 - Shard 2
            string logOutputFileName_AMB3 = testName + "_AMB3.log";
            AMB_Settings AMB3 = new AMB_Settings
            {
                AMB_ServiceName = serverName,
                AMB_PortAppReceives = "3000",
                AMB_PortAMBSends = "3001",
                AMB_ServiceLogPath = ambrosiaLogDir,
                AMB_CreateService = "A",
                AMB_PauseAtStart = "N",
                AMB_PersistLogs = "Y",
                AMB_NewLogTriggerSize = "1000",
                AMB_ActiveActive = "N",
                AMB_Version = "0",
                AMB_ShardID = "2",
                AMB_OldShards = "1",
                AMB_NewShards = "2"
            };
            MyUtils.CallAMB(AMB3, logOutputFileName_AMB3, AMB_ModeConsts.AddShard);

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

            // First Server Call
            string logOutputFileName_Server1 = testName + "_Server1.log";
            int serverProcessID1 = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server1, 1, false);

            // Delay until client is done - also check Server just to make sure
            //bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 5, false, testName, true); // Number of bytes processed
            //pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server1, byteSize, 5, false, testName, true);

            // Give it 2 seconds to do something before killing it
            //Thread.Sleep(2000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            // ImmCoord3
            string logOutputFileName_ImmCoord3 = testName + "_ImmCoord3.log";
            int ImmCoordProcessID3 = MyUtils.StartImmCoord(serverName, 3500, logOutputFileName_ImmCoord3, shardID: 2);

            // Second Server Call
            string logOutputFileName_Server2 = testName + "_Server2.log";
            int serverProcessID2 = MyUtils.StartPerfServer("3001", "3000", clientJobName, serverName, logOutputFileName_Server2, 1, false);
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 10, false, testName, true); // Number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server2, byteSize, 10, false, testName, true);

            // Stop things to file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(serverProcessID1);
            MyUtils.KillProcess(serverProcessID2);
            MyUtils.KillProcess(ImmCoordProcessID1);
            MyUtils.KillProcess(ImmCoordProcessID2);
            MyUtils.KillProcess(ImmCoordProcessID3);

            // Verify AMB
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_AMB1);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_AMB2);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_AMB3);

            // Verify Client
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);
            
            // Verify Server 1
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server2);
            /*
            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version, shardID: 1);*/
        }
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Configuration;
using System.Threading;
using System.Windows.Forms; // need this to handle threading issue on sleeps


namespace AmbrosiaTest
{
    /// <summary>
    /// Async tests are the same as other AMB tests but instead of calling PerformanceTestInterruptible, it is calling 
    /// PerformaceTest binaries as those are the exes that run async. 
    /// </summary>
    [TestClass]
    public class AsyncTests
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

        //** Basic end to end test starts job and server and runs a bunch of bytes through
        [TestMethod]
        public void AMB_Async_Basic_Test()
        {
            //NOTE - the Cleanup has this hard coded so if this changes, update Cleanup section too
            string testName = "asyncbasic";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "3221225472";

            Utilities MyUtils = new Utilities();

                        //AMB1 - Job
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

            //AMB2
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
                AMB_Version = "0"
            };
            MyUtils.CallAMB(AMB2, logOutputFileName_AMB2, AMB_ModeConsts.RegisterInstance);

            //ImmCoord1
            string logOutputFileName_ImmCoord1 = testName + "_ImmCoord1.log";
            int ImmCoordProcessID1 = MyUtils.StartImmCoord(clientJobName, 1500, logOutputFileName_ImmCoord1);

            //ImmCoord2
            string logOutputFileName_ImmCoord2 = testName + "_ImmCoord2.log";
            int ImmCoordProcessID2 = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2);

            //Client Job Call
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartAsyncPerfClientJob("1001", "1000", clientJobName, serverName, "3",logOutputFileName_ClientJob);

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartAsyncPerfServer("2001", "2000", serverName, logOutputFileName_Server);

            //Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 20, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server, byteSize, 10, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(serverProcessID);
            MyUtils.KillProcess(ImmCoordProcessID1);
            MyUtils.KillProcess(ImmCoordProcessID2);

            //Verify AMB 
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_AMB1);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_AMB2);

            // Verify Client
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);

            // Verify Server
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server);

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version,"",true);
        }

        //** The replay / recovery of this basic test uses the latest log file instead of the first
        [TestMethod]
        public void AMB_Async_ReplayLatest_Test()
        {
            //NOTE - the Cleanup has this hard coded so if this changes, update Cleanup section too
            string testName = "asyncreplaylatest";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "3221225472";

            Utilities MyUtils = new Utilities();

            //AMB1 - Job
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

            //AMB2
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
                AMB_Version = "0"
            };
            MyUtils.CallAMB(AMB2, logOutputFileName_AMB2, AMB_ModeConsts.RegisterInstance);

            //ImmCoord1
            string logOutputFileName_ImmCoord1 = testName + "_ImmCoord1.log";
            int ImmCoordProcessID1 = MyUtils.StartImmCoord(clientJobName, 1500, logOutputFileName_ImmCoord1);

            //ImmCoord2
            string logOutputFileName_ImmCoord2 = testName + "_ImmCoord2.log";
            int ImmCoordProcessID2 = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2);

            //Client Job Call
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartAsyncPerfClientJob("1001", "1000", clientJobName, serverName, "3", logOutputFileName_ClientJob);

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartAsyncPerfServer("2001", "2000", serverName, logOutputFileName_Server);

            //Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 20, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server, byteSize, 10, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(serverProcessID);
            MyUtils.KillProcess(ImmCoordProcessID1);
            MyUtils.KillProcess(ImmCoordProcessID2);

            // No need to verify cmp files as the test is basically same as basic test

            // Verify integrity of Ambrosia logs by replaying from the Latest one
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true,false, AMB1.AMB_Version, "", true);
        }

        [TestMethod]
        public void AMB_Async_KillJob_Test()
        {
            //NOTE - the Cleanup has test name hard coded so if this changes, update Cleanup section too
            string testName = "asynckilljobtest";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "3221225472";

            Utilities MyUtils = new Utilities();

            //AMB1 - Job
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

            //AMB2
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
                AMB_Version = "0"
            };
            MyUtils.CallAMB(AMB2, logOutputFileName_AMB2, AMB_ModeConsts.RegisterInstance);

            //ImmCoord1
            string logOutputFileName_ImmCoord1 = testName + "_ImmCoord1.log";
            int ImmCoordProcessID1 = MyUtils.StartImmCoord(clientJobName, 1500, logOutputFileName_ImmCoord1);

            //ImmCoord2
            string logOutputFileName_ImmCoord2 = testName + "_ImmCoord2.log";
            int ImmCoordProcessID2 = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2);

            //Client Job Call
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartAsyncPerfClientJob("1001", "1000", clientJobName, serverName, "3", logOutputFileName_ClientJob);

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartAsyncPerfServer("2001", "2000", serverName, logOutputFileName_Server);

            // Give it 5 seconds to do something before killing it
            Thread.Sleep(5000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            //Kill job at this point as well as ImmCoord1
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(ImmCoordProcessID1);

            //Restart ImmCoord1
            string logOutputFileName_ImmCoord1_Restarted = testName + "_ImmCoord1_Restarted.log";
            int ImmCoordProcessID1_Restarted = MyUtils.StartImmCoord(clientJobName, 1500, logOutputFileName_ImmCoord1_Restarted);

            // Restart Job Process
            string logOutputFileName_ClientJob_Restarted = testName + "_ClientJob_Restarted.log";
            int clientJobProcessID_Restarted = MyUtils.StartAsyncPerfClientJob("1001", "1000", clientJobName, serverName, "3", logOutputFileName_ClientJob_Restarted);

            //Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob_Restarted, byteSize, 15, false, testName, true); // Total bytes received
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server, byteSize, 15, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID_Restarted);
            MyUtils.KillProcess(serverProcessID);
            MyUtils.KillProcess(ImmCoordProcessID1_Restarted);
            MyUtils.KillProcess(ImmCoordProcessID2);

            // Verify Client (before and after restart)
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob_Restarted);

            // Verify Server
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server);

            // Give it a few seconds to make sure everything is started fine
            Thread.Sleep(3000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version, "", true);
        }

        [TestMethod]
        public void AMB_Async_KillServer_Test()
        {
            //NOTE - the Cleanup has test name hard coded so if this changes, update Cleanup section too
            string testName = "asynckillservertest";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "3221225472";

            Utilities MyUtils = new Utilities();

            //AMB1 - Job
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

            //AMB2
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
                AMB_ActiveActive = "N",  // NOTE: if put this to "Y" then when kill it, it will become a checkpointer which never becomes primary
                AMB_Version = "0"
            };
            MyUtils.CallAMB(AMB2, logOutputFileName_AMB2, AMB_ModeConsts.RegisterInstance);

            //ImmCoord1
            string logOutputFileName_ImmCoord1 = testName + "_ImmCoord1.log";
            int ImmCoordProcessID1 = MyUtils.StartImmCoord(clientJobName, 1500, logOutputFileName_ImmCoord1);

            //ImmCoord2
            string logOutputFileName_ImmCoord2 = testName + "_ImmCoord2.log";
            int ImmCoordProcessID2 = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2);

            //Client Job Call
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartAsyncPerfClientJob("1001", "1000", clientJobName, serverName, "3", logOutputFileName_ClientJob);

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartAsyncPerfServer("2001", "2000", serverName, logOutputFileName_Server);

            // Give it 10 seconds to do something before killing it
            Thread.Sleep(10000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            //Kill Server at this point as well as ImmCoord2
            MyUtils.KillProcess(serverProcessID);
            MyUtils.KillProcess(ImmCoordProcessID2);

            //Restart ImmCoord2
            string logOutputFileName_ImmCoord2_Restarted = testName + "_ImmCoord2_Restarted.log";
            int ImmCoordProcessID2_Restarted = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2_Restarted);

            // Restart Server Process
            string logOutputFileName_Server_Restarted = testName + "_Server_Restarted.log";
            int serverProcessID_Restarted = MyUtils.StartAsyncPerfServer("2001", "2000", serverName, logOutputFileName_Server_Restarted);

            //Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server_Restarted, byteSize, 25, false, testName, true);  // Total Bytes received needs to be accurate
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 15, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(serverProcessID_Restarted);
            MyUtils.KillProcess(ImmCoordProcessID1);
            MyUtils.KillProcess(ImmCoordProcessID2_Restarted);

            // Verify Server (before and after restart)
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server_Restarted);

            // Verify Client
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version, "", true);
        }

        //****************************
        // The basic test of Active Active where kill ASYNC primary server
        // 1 client 
        // 3 servers - primary, checkpointing secondary and active secondary (can become primary)
        //
        // killing first server (primary) will then have active secondary become primary
        // restarting first server will make it the active secondary
        //  
        //****************************
        [TestMethod]
        public void AMB_Async_ActiveActive_BasicTest()
        {
            string testName = "asyncactiveactivebasic";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "3221225472";
            string newPrimary = "NOW I'm Primary";

            Utilities MyUtils = new Utilities();

            //AMB1 - primary
            string logOutputFileName_AMB1 = testName + "_AMB1.log";
            AMB_Settings AMB1 = new AMB_Settings
            {
                AMB_ServiceName = serverName,
                AMB_PortAppReceives = "1000",
                AMB_PortAMBSends = "1001",
                AMB_ServiceLogPath = ambrosiaLogDir,
                AMB_CreateService = "A",
                AMB_PauseAtStart = "N",
                AMB_PersistLogs = "Y",
                AMB_NewLogTriggerSize = "1000",
                AMB_ActiveActive = "Y",
                AMB_Version = "0"
            };
            MyUtils.CallAMB(AMB1, logOutputFileName_AMB1, AMB_ModeConsts.RegisterInstance);


            //AMB2 - check pointer
            string logOutputFileName_AMB2 = testName + "_AMB2.log";
            AMB_Settings AMB2 = new AMB_Settings
            {
                AMB_ReplicaNumber = "1",
                AMB_ServiceName = serverName,
                AMB_PortAppReceives = "2000",
                AMB_PortAMBSends = "2001",
                AMB_ServiceLogPath = ambrosiaLogDir,
                AMB_CreateService = "A",
                AMB_PauseAtStart = "N",
                AMB_PersistLogs = "Y",
                AMB_NewLogTriggerSize = "1000",
                AMB_ActiveActive = "Y",
                AMB_Version = "0"
            };
            MyUtils.CallAMB(AMB2, logOutputFileName_AMB2, AMB_ModeConsts.AddReplica);

            //AMB3 - active secondary
            string logOutputFileName_AMB3 = testName + "_AMB3.log";
            AMB_Settings AMB3 = new AMB_Settings
            {
                AMB_ReplicaNumber = "2",
                AMB_ServiceName = serverName,
                AMB_PortAppReceives = "3000",
                AMB_PortAMBSends = "3001",
                AMB_ServiceLogPath = ambrosiaLogDir,
                AMB_CreateService = "A",
                AMB_PauseAtStart = "N",
                AMB_PersistLogs = "Y",
                AMB_NewLogTriggerSize = "1000",
                AMB_ActiveActive = "Y",
                AMB_Version = "0"
            };
            MyUtils.CallAMB(AMB3, logOutputFileName_AMB3, AMB_ModeConsts.AddReplica);

            //AMB4 - Job
            string logOutputFileName_AMB4 = testName + "_AMB4.log";
            AMB_Settings AMB4 = new AMB_Settings
            {
                AMB_ServiceName = clientJobName,
                AMB_PortAppReceives = "4000",
                AMB_PortAMBSends = "4001",
                AMB_ServiceLogPath = ambrosiaLogDir,
                AMB_CreateService = "A",
                AMB_PauseAtStart = "N",
                AMB_PersistLogs = "Y",
                AMB_NewLogTriggerSize = "1000",
                AMB_ActiveActive = "N",
                AMB_Version = "0"
            };
            MyUtils.CallAMB(AMB4, logOutputFileName_AMB4, AMB_ModeConsts.RegisterInstance);

            //ImmCoord1
            string logOutputFileName_ImmCoord1 = testName + "_ImmCoord1.log";
            int ImmCoordProcessID1 = MyUtils.StartImmCoord(serverName, 1500, logOutputFileName_ImmCoord1, true, 0);

            //ImmCoord2
            string logOutputFileName_ImmCoord2 = testName + "_ImmCoord2.log";
            int ImmCoordProcessID2 = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2, true, 1);

            //ImmCoord3
            string logOutputFileName_ImmCoord3 = testName + "_ImmCoord3.log";
            int ImmCoordProcessID3 = MyUtils.StartImmCoord(serverName, 3500, logOutputFileName_ImmCoord3, true, 2);

            //ImmCoord4
            string logOutputFileName_ImmCoord4 = testName + "_ImmCoord4.log";
            int ImmCoordProcessID4 = MyUtils.StartImmCoord(clientJobName, 4500, logOutputFileName_ImmCoord4);

            //Server Call - primary
            string logOutputFileName_Server1 = testName + "_Server1.log";
            int serverProcessID1 = MyUtils.StartAsyncPerfServer("1001", "1000", serverName, logOutputFileName_Server1);
            Thread.Sleep(1000); // give a second to make it a primary

            //Server Call - checkpointer
            string logOutputFileName_Server2 = testName + "_Server2.log";
            int serverProcessID2 = MyUtils.StartAsyncPerfServer("2001", "2000", serverName, logOutputFileName_Server2);
            Thread.Sleep(1000); // give a second

            //Server Call - active secondary
            string logOutputFileName_Server3 = testName + "_Server3.log";
            int serverProcessID3 = MyUtils.StartAsyncPerfServer("3001", "3000", serverName, logOutputFileName_Server3);

            //start Client Job 
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartAsyncPerfClientJob("4001", "4000", clientJobName, serverName, "3", logOutputFileName_ClientJob);

            // Give it 10 seconds to do something before killing it
            Thread.Sleep(10000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            //Kill Primary Server (server1) at this point as well as ImmCoord1
            MyUtils.KillProcess(serverProcessID1);
            MyUtils.KillProcess(ImmCoordProcessID1);

            // at this point, server3 (active secondary) becomes primary 
            Thread.Sleep(1000);

            //Restart server1 (ImmCoord1 and server) ... this will become active secondary now
            string logOutputFileName_ImmCoord1_Restarted = testName + "_ImmCoord1_Restarted.log";
            int ImmCoordProcessID1_Restarted = MyUtils.StartImmCoord(serverName, 1500, logOutputFileName_ImmCoord1_Restarted, true, 0);
            string logOutputFileName_Server1_Restarted = testName + "_Server1_Restarted.log";
            int serverProcessID_Restarted1 = MyUtils.StartAsyncPerfServer("1001", "1000", serverName, logOutputFileName_Server1_Restarted);

            //Delay until finished ... looking at the most recent primary (server3) but also verify others hit done too
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server3, byteSize, 30, false, testName, true);  // Total Bytes received needs to be accurate
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 15, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server2, byteSize, 15, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server1_Restarted, byteSize, 15, false, testName, true);

            // Also verify ImmCoord has the string to show it is primary
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ImmCoord3, newPrimary, 5, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(serverProcessID2);
            MyUtils.KillProcess(serverProcessID3);
            MyUtils.KillProcess(serverProcessID_Restarted1);
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(ImmCoordProcessID2);
            MyUtils.KillProcess(ImmCoordProcessID3);
            MyUtils.KillProcess(ImmCoordProcessID1_Restarted);
            MyUtils.KillProcess(ImmCoordProcessID4);

            // Verify cmp files for client and 3 servers
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server1);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server1_Restarted);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server2);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server3);

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version, "", true);
        }



        //****************************
        // Most complex test of Active Active for client and server - Async version of it
        // 3 clients - primary, checkpointing secondary and active secondary 
        // 3 servers - primary, checkpointing secondary and active secondary
        //
        // Kill all aspects of the system and restart
        //  
        //****************************
        [TestMethod]
        public void AMB_Async_ActiveActive_FullTest()
        {
            string testName = "asyncactiveactivefull";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "3221225472";
            string newPrimary = "NOW I'm Primary";

            // If failures in queue, set a flag to not run tests or clean up - helps debug tests that failed by keeping in proper state
            Utilities MyUtils = new Utilities();

            //AMB1 - primary server
            string logOutputFileName_AMB1 = testName + "_AMB1.log";
            AMB_Settings AMB1 = new AMB_Settings
            {
                AMB_ServiceName = serverName,
                AMB_PortAppReceives = "1000",
                AMB_PortAMBSends = "1001",
                AMB_ServiceLogPath = ambrosiaLogDir,
                AMB_CreateService = "A",
                AMB_PauseAtStart = "N",
                AMB_PersistLogs = "Y",
                AMB_NewLogTriggerSize = "1000",
                AMB_ActiveActive = "Y",
                AMB_Version = "0"
            };

            MyUtils.CallAMB(AMB1, logOutputFileName_AMB1, AMB_ModeConsts.RegisterInstance);

            //AMB2 - check pointer server
            string logOutputFileName_AMB2 = testName + "_AMB2.log";
            AMB_Settings AMB2 = new AMB_Settings
            {
                AMB_ServiceName = serverName,
                AMB_ReplicaNumber = "1",
                AMB_PortAppReceives = "2000",
                AMB_PortAMBSends = "2001",
                AMB_ServiceLogPath = ambrosiaLogDir,
                AMB_CreateService = "A",
                AMB_PauseAtStart = "N",
                AMB_PersistLogs = "Y",
                AMB_NewLogTriggerSize = "1000",
                AMB_ActiveActive = "Y",
                AMB_Version = "0"
            };
            MyUtils.CallAMB(AMB2, logOutputFileName_AMB2, AMB_ModeConsts.AddReplica);

            //AMB3 - active secondary server
            string logOutputFileName_AMB3 = testName + "_AMB3.log";
            AMB_Settings AMB3 = new AMB_Settings
            {
                AMB_ServiceName = serverName,
                AMB_ReplicaNumber = "2",
                AMB_PortAppReceives = "3000",
                AMB_PortAMBSends = "3001",
                AMB_ServiceLogPath = ambrosiaLogDir,
                AMB_CreateService = "A",
                AMB_PauseAtStart = "N",
                AMB_PersistLogs = "Y",
                AMB_NewLogTriggerSize = "1000",
                AMB_ActiveActive = "Y",
                AMB_Version = "0"
            };
            MyUtils.CallAMB(AMB3, logOutputFileName_AMB3, AMB_ModeConsts.AddReplica);

            //AMB4 - Job primary
            string logOutputFileName_AMB4 = testName + "_AMB4.log";
            AMB_Settings AMB4 = new AMB_Settings
            {
                AMB_ServiceName = clientJobName,
                AMB_PortAppReceives = "4000",
                AMB_PortAMBSends = "4001",
                AMB_ServiceLogPath = ambrosiaLogDir,
                AMB_CreateService = "A",
                AMB_PauseAtStart = "N",
                AMB_PersistLogs = "Y",
                AMB_NewLogTriggerSize = "1000",
                AMB_ActiveActive = "Y",
                AMB_Version = "0"
            };
            MyUtils.CallAMB(AMB4, logOutputFileName_AMB4, AMB_ModeConsts.RegisterInstance);

            //AMB5 - Job checkpoint
            string logOutputFileName_AMB5 = testName + "_AMB5.log";
            AMB_Settings AMB5 = new AMB_Settings
            {
                AMB_ServiceName = clientJobName,
                AMB_ReplicaNumber = "1",
                AMB_PortAppReceives = "5000",
                AMB_PortAMBSends = "5001",
                AMB_ServiceLogPath = ambrosiaLogDir,
                AMB_CreateService = "A",
                AMB_PauseAtStart = "N",
                AMB_PersistLogs = "Y",
                AMB_NewLogTriggerSize = "1000",
                AMB_ActiveActive = "Y",
                AMB_Version = "0"
            };
            MyUtils.CallAMB(AMB5, logOutputFileName_AMB5, AMB_ModeConsts.AddReplica);

            //AMB6 - Job secondary
            string logOutputFileName_AMB6 = testName + "_AMB6.log";
            AMB_Settings AMB6 = new AMB_Settings
            {
                AMB_ServiceName = clientJobName,
                AMB_ReplicaNumber = "2",
                AMB_PortAppReceives = "6000",
                AMB_PortAMBSends = "6001",
                AMB_ServiceLogPath = ambrosiaLogDir,
                AMB_CreateService = "A",
                AMB_PauseAtStart = "N",
                AMB_PersistLogs = "Y",
                AMB_NewLogTriggerSize = "1000",
                AMB_ActiveActive = "Y",
                AMB_Version = "0"
            };
            MyUtils.CallAMB(AMB6, logOutputFileName_AMB6, AMB_ModeConsts.AddReplica);

            //Server 1
            string logOutputFileName_ImmCoord1 = testName + "_ImmCoord1.log";
            int ImmCoordProcessID1 = MyUtils.StartImmCoord(serverName, 1500, logOutputFileName_ImmCoord1, true, 0);
            Thread.Sleep(1000);
            string logOutputFileName_Server1 = testName + "_Server1.log";
            int serverProcessID1 = MyUtils.StartAsyncPerfServer("1001", "1000", serverName, logOutputFileName_Server1);

            //Server 2
            string logOutputFileName_ImmCoord2 = testName + "_ImmCoord2.log";
            int ImmCoordProcessID2 = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2, true, 1);
            Thread.Sleep(1000); // give a second
            string logOutputFileName_Server2 = testName + "_Server2.log";
            int serverProcessID2 = MyUtils.StartAsyncPerfServer("2001", "2000", serverName, logOutputFileName_Server2);

            //Server 3
            string logOutputFileName_ImmCoord3 = testName + "_ImmCoord3.log";
            int ImmCoordProcessID3 = MyUtils.StartImmCoord(serverName, 3500, logOutputFileName_ImmCoord3, true, 2);
            string logOutputFileName_Server3 = testName + "_Server3.log";
            int serverProcessID3 = MyUtils.StartAsyncPerfServer("3001", "3000", serverName, logOutputFileName_Server3);

            //Client 1
            string logOutputFileName_ImmCoord4 = testName + "_ImmCoord4.log";
            int ImmCoordProcessID4 = MyUtils.StartImmCoord(clientJobName, 4500, logOutputFileName_ImmCoord4, true, 0);
            Thread.Sleep(1000); // give a second
            string logOutputFileName_ClientJob1 = testName + "_ClientJob1.log";
            int clientJobProcessID1 = MyUtils.StartAsyncPerfClientJob("4001", "4000", clientJobName, serverName, "1", logOutputFileName_ClientJob1);

            //Client 2
            string logOutputFileName_ImmCoord5 = testName + "_ImmCoord5.log";
            int ImmCoordProcessID5 = MyUtils.StartImmCoord(clientJobName, 5500, logOutputFileName_ImmCoord5, true, 1);
            Thread.Sleep(1000); // give a second
            string logOutputFileName_ClientJob2 = testName + "_ClientJob2.log";
            int clientJobProcessID2 = MyUtils.StartAsyncPerfClientJob("5001", "5000", clientJobName, serverName, "1", logOutputFileName_ClientJob2);

            //Client 3
            string logOutputFileName_ImmCoord6 = testName + "_ImmCoord6.log";
            int ImmCoordProcessID6 = MyUtils.StartImmCoord(clientJobName, 6500, logOutputFileName_ImmCoord6, true, 2);
            Thread.Sleep(1000); // give a second
            string logOutputFileName_ClientJob3 = testName + "_ClientJob3.log";
            int clientJobProcessID3 = MyUtils.StartAsyncPerfClientJob("6001", "6000", clientJobName, serverName, "1", logOutputFileName_ClientJob3);

            // Give it 10 seconds to do something before killing it
            Thread.Sleep(10000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            //Kill all aspects - kill primary of each last
            MyUtils.KillProcess(serverProcessID2);
            MyUtils.KillProcess(ImmCoordProcessID2);

            MyUtils.KillProcess(serverProcessID3);
            MyUtils.KillProcess(ImmCoordProcessID3);

            MyUtils.KillProcess(serverProcessID1);
            MyUtils.KillProcess(ImmCoordProcessID1);

            MyUtils.KillProcess(clientJobProcessID2);
            MyUtils.KillProcess(ImmCoordProcessID5);

            MyUtils.KillProcess(clientJobProcessID3);
            MyUtils.KillProcess(ImmCoordProcessID6);

            MyUtils.KillProcess(clientJobProcessID1);
            MyUtils.KillProcess(ImmCoordProcessID4);

            // at this point, the system is dead - restart 
            Thread.Sleep(5000);

            //Restart servers
            string logOutputFileName_ImmCoord1_Restarted = testName + "_ImmCoord1_Restarted.log";
            int ImmCoordProcessID1_Restarted = MyUtils.StartImmCoord(serverName, 1500, logOutputFileName_ImmCoord1_Restarted, true, 0);
            string logOutputFileName_Server1_Restarted = testName + "_Server1_Restarted.log";
            int serverProcessID_Restarted1 = MyUtils.StartAsyncPerfServer("1001", "1000", serverName, logOutputFileName_Server1_Restarted);
            string logOutputFileName_ImmCoord2_Restarted = testName + "_ImmCoord2_Restarted.log";
            int ImmCoordProcessID2_Restarted = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2_Restarted, true, 1);
            string logOutputFileName_Server2_Restarted = testName + "_Server2_Restarted.log";
            int serverProcessID_Restarted2 = MyUtils.StartAsyncPerfServer("2001", "2000", serverName, logOutputFileName_Server2_Restarted);
            string logOutputFileName_ImmCoord3_Restarted = testName + "_ImmCoord3_Restarted.log";
            int ImmCoordProcessID3_Restarted = MyUtils.StartImmCoord(serverName, 3500, logOutputFileName_ImmCoord3_Restarted, true, 2);
            string logOutputFileName_Server3_Restarted = testName + "_Server3_Restarted.log";
            int serverProcessID_Restarted3 = MyUtils.StartAsyncPerfServer("3001", "3000", serverName, logOutputFileName_Server3_Restarted);

            //Restart clients
            string logOutputFileName_ImmCoord4_Restarted = testName + "_ImmCoord4_Restarted.log";
            int ImmCoordProcessID4_Restarted = MyUtils.StartImmCoord(clientJobName, 4500, logOutputFileName_ImmCoord4_Restarted, true, 0);
            string logOutputFileName_ClientJob1_Restarted = testName + "_ClientJob1_Restarted.log";
                        int clientJobProcessID_Restarted1 = MyUtils.StartAsyncPerfClientJob("4001", "4000", clientJobName, serverName, "1", logOutputFileName_ClientJob1_Restarted);
            //int clientJobProcessID_Restarted1 = 0;
            //*#*#* At this point attach debugger to PT process

            string logOutputFileName_ImmCoord5_Restarted = testName + "_ImmCoord5_Restarted.log";
            int ImmCoordProcessID5_Restarted = MyUtils.StartImmCoord(clientJobName, 5500, logOutputFileName_ImmCoord5_Restarted, true, 1);
            string logOutputFileName_ClientJob2_Restarted = testName + "_ClientJob2_Restarted.log";
            int clientJobProcessID_Restarted2 = MyUtils.StartAsyncPerfClientJob("5001", "5000", clientJobName, serverName, "1", logOutputFileName_ClientJob2_Restarted);
            string logOutputFileName_ImmCoord6_Restarted = testName + "_ImmCoord6_Restarted.log";
            int ImmCoordProcessID6_Restarted = MyUtils.StartImmCoord(clientJobName, 6500, logOutputFileName_ImmCoord6_Restarted, true, 2);
            string logOutputFileName_ClientJob3_Restarted = testName + "_ClientJob3_Restarted.log";
            int clientJobProcessID_Restarted3 = MyUtils.StartAsyncPerfClientJob("6001", "6000", clientJobName, serverName, "1", logOutputFileName_ClientJob3_Restarted);

            //Delay until finished ... looking at the primary (server1) but also verify others hit done too
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server1_Restarted, byteSize, 45, false, testName, true);  // Total Bytes received needs to be accurate
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server2_Restarted, byteSize, 15, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server3_Restarted, byteSize, 15, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob1_Restarted, byteSize, 15, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob2_Restarted, byteSize, 15, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob3_Restarted, byteSize, 15, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(serverProcessID_Restarted1);
            MyUtils.KillProcess(serverProcessID_Restarted2);
            MyUtils.KillProcess(serverProcessID_Restarted3);
            MyUtils.KillProcess(clientJobProcessID_Restarted1);
            MyUtils.KillProcess(clientJobProcessID_Restarted2);
            MyUtils.KillProcess(clientJobProcessID_Restarted3);
            MyUtils.KillProcess(ImmCoordProcessID1_Restarted);
            MyUtils.KillProcess(ImmCoordProcessID2_Restarted);
            MyUtils.KillProcess(ImmCoordProcessID3_Restarted);
            MyUtils.KillProcess(ImmCoordProcessID4_Restarted);
            MyUtils.KillProcess(ImmCoordProcessID5_Restarted);
            MyUtils.KillProcess(ImmCoordProcessID6_Restarted);

            // Verify cmp files for client and 3 servers
            // the timing is a bit off when have so many processes so cmp files not 
            // really reliable. As long as they get through whole thing, that is what counts.

            // Verify ImmCoord has the string to show it is primary for both server and client
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ImmCoord2_Restarted, newPrimary, 5, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ImmCoord5_Restarted, newPrimary, 5, false, testName, true);

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version, "", true);
        }


        [TestCleanup()]
        public void Cleanup()
        {
            // Kill all ImmortalCoordinators, Job and Server exes
            Utilities MyUtils = new Utilities();
            MyUtils.AsyncTestCleanup();
        }


    }
}

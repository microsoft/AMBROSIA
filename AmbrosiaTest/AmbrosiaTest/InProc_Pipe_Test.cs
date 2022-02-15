using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Configuration;

namespace AmbrosiaTest
{
    /// <summary>
    /// Summary description for InProc_Test
    /// </summary>
    [TestClass]
    public class InProc_Pipe_Test
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


        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }


        //** Simple end to end where Client is InProc Pipe and Server is two proc
        [TestMethod]
        public void AMB_InProc_Pipe_ClientOnly_Test()
        {

            Utilities MyUtils = new Utilities();
            
            //NOTE - the Cleanup has this hard coded so if this changes, update Cleanup section too
            string testName = "inprocpipeclientonly";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = MyUtils.baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "1073741824";

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

            //ImmCoord2
            string logOutputFileName_ImmCoord2 = testName + "_ImmCoord2.log";
            int ImmCoordProcessID2 = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2);

            //Client Job Call
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "1024", "1", logOutputFileName_ClientJob, MyUtils.deployModeInProc, "1500");

            // Give it a few seconds to start
            MyUtils.TestDelay(2000);

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false);

            //Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server, byteSize, 5, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(serverProcessID);
            MyUtils.KillProcess(ImmCoordProcessID2);

            // Verify Client
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);

            // Verify Server
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server);

            // Verify integrity of Ambrosia logs by replaying 
            // To verify Server in one location and client in another would take bigger code change
            // Not that crucial to do ... but TO DO: make it so verify log in two different places.
            //MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }


        //** Simple end to end where Server is InProc Pipe and Client is two proc
        [TestMethod]
        public void AMB_InProc_Pipe_ServerOnly_Test()
        {
            Utilities MyUtils = new Utilities();

            //NOTE - the Cleanup has this hard coded so if this changes, update Cleanup section too
            string testName = "inprocpipeserveronly";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = MyUtils.baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "1073741824";

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

            //Client Job Call
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "1024", "1", logOutputFileName_ClientJob, MyUtils.deployModeSecondProc);

            // Give it a few seconds to start
            MyUtils.TestDelay(2000);

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false,0,MyUtils.deployModeInProc, "2500");

            //Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server, byteSize, 5, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(serverProcessID);
            MyUtils.KillProcess(ImmCoordProcessID1);

            // Verify Client
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);

            // Verify Server
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server);

            // Verify integrity of Ambrosia logs by replaying 
            // To verify Server in one location and client in another would take bigger code change
            // Not that crucial to do ... but TO DO: make it so verify log in two different places.
            //MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }

        //** Basic end to end test starts job and server and runs a bunch of bytes through
        //** Only a few rounds but more extensive then unit tests
        [TestMethod]
        public void AMB_InProc_Basic_Test()
        {
            Utilities MyUtils = new Utilities();

            //NOTE - the Cleanup has this hard coded so if this changes, update Cleanup section too
            string testName = "inprocbasictest";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = MyUtils.baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "3221225472";

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
                AMB_NewLogTriggerSize = "500",
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
                AMB_NewLogTriggerSize = "500",
                AMB_ActiveActive = "N",
                AMB_Version = "0"
            };
            MyUtils.CallAMB(AMB2, logOutputFileName_AMB2, AMB_ModeConsts.RegisterInstance);

            //Client Job Call
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "32768", "3", logOutputFileName_ClientJob,MyUtils.deployModeInProc,"1500");

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false,0,MyUtils.deployModeInProc,"2500");

            //Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 15, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server, byteSize, 15, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(serverProcessID);

            // Verify Client
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);

            // Verify Server
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server);

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }


        //** Similar to Double Kill restart but it doesn't actually kill it. It just restarts it and it
        //** takes on the new restarted process and original process dies.  It is a way to do client migration
        [TestMethod]
        public void AMB_InProc_MigrateClient_Test()
        {
            Utilities MyUtils = new Utilities();

            //NOTE - the Cleanup has this hard coded so if this changes, update Cleanup section too
            string testName = "inprocmigrateclient";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = MyUtils.baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "13958643712";
           // string killJobMessage = "Migrating or upgrading. Must commit suicide since I'm the primary";

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

            //Client Job Call
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob,MyUtils.deployModeInProc,"1500");

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false,0,MyUtils.deployModeInProc, "2500");

            // Give it 3 seconds to do something before killing it
            MyUtils.TestDelay(3000);

            // DO NOT Kill both Job and Server 
            // This is main part of test - get it to have Job and Server take over and run
            // Orig Job and Server stop then
            //            MyUtils.KillProcess(clientJobProcessID);
            //          MyUtils.KillProcess(serverProcessID);

            // Restart Job
            string logOutputFileName_ClientJob_Restarted = testName + "_ClientJob_Restarted.log";
            int clientJobProcessID_Restarted = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob_Restarted,MyUtils.deployModeInProc,"3500");

            // just give a rest 
            MyUtils.TestDelay(2000);

            // Restart Server
            string logOutputFileName_Server_Restarted = testName + "_Server_Restarted.log";
            int serverProcessID_Restarted = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server_Restarted, 1, false,0,MyUtils.deployModeInProc,"4500");

            //Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob_Restarted, byteSize, 20, false, testName, true); // Total bytes received
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server_Restarted, byteSize, 20, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID_Restarted);
            MyUtils.KillProcess(serverProcessID_Restarted);

            // don't do cmp file checks because changes to much between machine to machine

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }


        //** Basically same as the basic test but using large check points - change is in the call to server
        //** See memory usage spike when checkpoint size is bigger
        [TestMethod]
        public void AMB_InProc_GiantCheckPoint_Test()
        {
            Utilities MyUtils = new Utilities();

            //NOTE - the Cleanup has this hard coded so if this changes, update Cleanup section too
            string testName = "inprocgiantcheckpointtest";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = MyUtils.baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "1073741824";
            long giantCheckpointSize = 2000483648;// 2147483648; 

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

            //Client Job Call
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "65536", "10", logOutputFileName_ClientJob,MyUtils.deployModeInProc,"1500");

            // Give it a few seconds to start
            MyUtils.TestDelay(2000);

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false, giantCheckpointSize, MyUtils.deployModeInProc, "2500");

            //Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 15, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server, byteSize, 15, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(serverProcessID);

            // Verify Client
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);

            // Verify Server
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server);
            
            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }

        //** This test does 5 rounds of messages starting with 64MB and cutting in half each time
        //** Basically same as the basic test but passing giant message - the difference is in the job.exe call and that is it
        [TestMethod]
        public void AMB_InProc_GiantMessage_Test()
        {
            Utilities MyUtils = new Utilities();

            //NOTE - the Cleanup has this hard coded so if this changes, update Cleanup section too
            string testName = "inprocgiantmessagetest";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = MyUtils.baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "5368709120";

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

            //Client Job Call
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "67108864", "5", logOutputFileName_ClientJob,MyUtils.deployModeInProc,"1500");

            // Give it a few seconds to start
            MyUtils.TestDelay(2000);

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false,0, MyUtils.deployModeInProc, "2500");

            //Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 15, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server, byteSize, 15, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(serverProcessID);

            // Verify Client
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);

            // Verify Server
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server);

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }

        //** Test starts Job and Server then kills both Job and Server 
        //  restarts both with JOB restarted first
        [TestMethod]
        public void AMB_InProc_DoubleKill_RestartJOBFirst_Test()
        {
            Utilities MyUtils = new Utilities();

            //NOTE - the Cleanup has test name hard coded so if this changes, update Cleanup section too
            string testName = "inprocdoublekilljob";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = MyUtils.baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "13958643712";

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

            //Client Job Call
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob,MyUtils.deployModeInProc,"1500");

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false,0,MyUtils.deployModeInProc,"2500");

            // Give it 5 seconds to do something before killing it
            MyUtils.TestDelay(5000);

            // Kill both Job and Server 
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(serverProcessID);

            // Actual test part here -- restarting JOB first before restarting Server
            // Restart Job
            string logOutputFileName_ClientJob_Restarted = testName + "_ClientJob_Restarted.log";
            int clientJobProcessID_Restarted = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob_Restarted,MyUtils.deployModeInProc,"1500");

            // just give a rest 
            MyUtils.TestDelay(3000);

            // Restart Server
            string logOutputFileName_Server_Restarted = testName + "_Server_Restarted.log";
            int serverProcessID_Restarted = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server_Restarted, 1, false,0,MyUtils.deployModeInProc,"2500");

            //Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob_Restarted, byteSize, 20, false, testName, true); // Total bytes received
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server_Restarted, byteSize, 20, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID_Restarted);
            MyUtils.KillProcess(serverProcessID_Restarted);

            // don't do cmp files for server and client as they change too much between different machines

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }

        //** Test starts Job and Server then kills both Job and Server 
        //  restarts both with SERVER restarted first
        [TestMethod]
        public void AMB_InProc_DoubleKill_RestartSERVERFirst_Test()
        {
            Utilities MyUtils = new Utilities();

            //NOTE - the Cleanup has test name hard coded so if this changes, update Cleanup section too
            string testName = "inprocdoublekillserver";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = MyUtils.baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "13958643712";

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

            //Client Job Call
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob,MyUtils.deployModeInProc,"1500");

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false,0,MyUtils.deployModeInProc,"2500");

            // Give it 5 seconds to do something before killing it
            MyUtils.TestDelay(5000);

            // Kill both Job and Server
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(serverProcessID);

            // Actual test part here -- restarting SERVER first before restarting Job
            // Restart Server 
            string logOutputFileName_Server_Restarted = testName + "_Server_Restarted.log";
            int serverProcessID_Restarted = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server_Restarted, 1, false,0,MyUtils.deployModeInProc,"2500");

            // just give a rest 
            MyUtils.TestDelay(3000);

            // Restart Job
            string logOutputFileName_ClientJob_Restarted = testName + "_ClientJob_Restarted.log";
            int clientJobProcessID_Restarted = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob_Restarted,MyUtils.deployModeInProc,"1500");

            //Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob_Restarted, byteSize, 20, false, testName, true); // Total bytes received
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server_Restarted, byteSize, 20, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID_Restarted);
            MyUtils.KillProcess(serverProcessID_Restarted);

            // don't use cmp files here as output from client and server vary too much from machine to machine

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }


        //** Test starts job and server then kills the job and restarts it and runs to completion
        //** NOTE - this actually kills job once, restarts it, kills again and then restarts it again
        [TestMethod]
        public void AMB_InProc_KillJob_Test()
        {
            Utilities MyUtils = new Utilities();

            //NOTE - the Cleanup has test name hard coded so if this changes, update Cleanup section too
            string testName = "inprockilljobtest";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = MyUtils.baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "13958643712";

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

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false, 0, MyUtils.deployModeInProc, "2500");

            //Client Job Call
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob, MyUtils.deployModeInProc, "1500");

            // Give it 5 seconds to do something before killing it
            MyUtils.TestDelay(5000);

            //Kill job at this point
            MyUtils.KillProcess(clientJobProcessID);

            // Restart Job Process
            string logOutputFileName_ClientJob_Restarted = testName + "_ClientJob_Restarted.log";
            int clientJobProcessID_Restarted = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob_Restarted,MyUtils.deployModeInProc,"1500");

            // Give it 5 seconds to do something before killing it again
            MyUtils.TestDelay(5000);

            //Kill job at this point 
            MyUtils.KillProcess(clientJobProcessID_Restarted);

            // Restart Job Process Again
            string logOutputFileName_ClientJob_Restarted_Again = testName + "_ClientJob_Restarted_Again.log";
            int clientJobProcessID_Restarted_Again = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob_Restarted_Again,MyUtils.deployModeInProc,"1500");

            //Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob_Restarted_Again, byteSize, 15, false, testName, true); // Total bytes received
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server, byteSize, 15, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID_Restarted_Again);
            MyUtils.KillProcess(serverProcessID);

            // Verify Client (before and after restart) -- don't do client cmp files because varies too much on machine to machine
            string basicHeader = "Bytes per RPC	Throughput (GB/sec)";
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, basicHeader, 5, false, testName, true,false);

            // Verify Server
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server);

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }

        //** Test starts job and server then kills the server and restarts it and runs to completion
        [TestMethod]
        public void AMB_InProc_KillServer_Test()
        {
            Utilities MyUtils = new Utilities();

            //NOTE - the Cleanup has test name hard coded so if this changes, update Cleanup section too
            string testName = "inprockillservertest";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = MyUtils.baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "13958643712";  

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

            //Client Job Call
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob,MyUtils.deployModeInProc,"1500");

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false,0,MyUtils.deployModeInProc,"2500");

            // Give it 10 seconds to do something before killing it
            MyUtils.TestDelay(10000);

            //Kill Server at this point as well as ImmCoord2
            MyUtils.KillProcess(serverProcessID);

            // Restart Server Process
            string logOutputFileName_Server_Restarted = testName + "_Server_Restarted.log";
            int serverProcessID_Restarted = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server_Restarted, 1, false,0,MyUtils.deployModeInProc,"2500");

            //Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server_Restarted, byteSize, 15, false, testName, true);  // Total Bytes received needs to be accurate
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 15, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(serverProcessID_Restarted);

            // Verify Server did something - don't use cmp files as changes from machine to machine too much on where state of the output file
            string checkpointReceived = "*X* At checkpoint, received";
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server, checkpointReceived, 15, false, testName, true,false);

            // Verify Client
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }

        //** Multiple clientscenario where many clients connect to a server
        [TestMethod]
        public void AMB_InProc_MultipleClientsPerServer_Test()
        {

            Utilities MyUtils = new Utilities();

            //NOTE - the Cleanup has this hard coded so if this changes, update Cleanup section too
            string testName = "inprocmultipleclientsperserver";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = MyUtils.baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "12884901888";

            //AMB1 - Server
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
                AMB_ActiveActive = "N",
                AMB_Version = "0"
            };

            MyUtils.CallAMB(AMB1, logOutputFileName_AMB1, AMB_ModeConsts.RegisterInstance);

            //AMB2 - Job 1
            string logOutputFileName_AMB2 = testName + "_AMB2.log";
            AMB_Settings AMB2 = new AMB_Settings
            {
                AMB_ServiceName = clientJobName + "0",
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

            //AMB3 - Job 2
            string logOutputFileName_AMB3 = testName + "_AMB3.log";
            AMB_Settings AMB3 = new AMB_Settings
            {
                AMB_ServiceName = clientJobName + "1",
                AMB_PortAppReceives = "3000",
                AMB_PortAMBSends = "3001",
                AMB_ServiceLogPath = ambrosiaLogDir,
                AMB_CreateService = "A",
                AMB_PauseAtStart = "N",
                AMB_PersistLogs = "Y",
                AMB_NewLogTriggerSize = "1000",
                AMB_ActiveActive = "N",
                AMB_Version = "0"
            };
            MyUtils.CallAMB(AMB3, logOutputFileName_AMB3, AMB_ModeConsts.RegisterInstance);

            //AMB4 - Job 3
            string logOutputFileName_AMB4 = testName + "_AMB4.log";
            AMB_Settings AMB4 = new AMB_Settings
            {
                AMB_ServiceName = clientJobName + "2",
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

            //AMB5 - job 4
            string logOutputFileName_AMB5 = testName + "_AMB5.log";
            AMB_Settings AMB5 = new AMB_Settings
            {
                AMB_ServiceName = clientJobName + "3",
                AMB_PortAppReceives = "5000",
                AMB_PortAMBSends = "5001",
                AMB_ServiceLogPath = ambrosiaLogDir,
                AMB_CreateService = "A",
                AMB_PauseAtStart = "N",
                AMB_PersistLogs = "Y",
                AMB_NewLogTriggerSize = "1000",
                AMB_ActiveActive = "N",
                AMB_Version = "0"
            };
            MyUtils.CallAMB(AMB5, logOutputFileName_AMB5, AMB_ModeConsts.RegisterInstance);

            // Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("1001", "1000", clientJobName, serverName, logOutputFileName_Server, 4, false,0, MyUtils.deployModeInProc, "1500");

            // Client call
            // For multiple clients, you have a "root" name and each of the client names are then root name + instance number starting at 0
            string logOutputFileName_ClientJob0 = testName + "_ClientJob0.log";
            int clientJobProcessID0 = MyUtils.StartPerfClientJob("2001", "2000", clientJobName + "0", serverName, "65536", "3", logOutputFileName_ClientJob0,MyUtils.deployModeInProc,"2500");

            string logOutputFileName_ClientJob1 = testName + "_ClientJob1.log";
            int clientJobProcessID1 = MyUtils.StartPerfClientJob("3001", "3000", clientJobName + "1", serverName, "65536", "3", logOutputFileName_ClientJob1, MyUtils.deployModeInProc, "3500");

            string logOutputFileName_ClientJob2 = testName + "_ClientJob2.log";
            int clientJobProcessID2 = MyUtils.StartPerfClientJob("4001", "4000", clientJobName + "2", serverName, "65536", "3", logOutputFileName_ClientJob2, MyUtils.deployModeInProc, "4500");

            string logOutputFileName_ClientJob3 = testName + "_ClientJob3.log";
            int clientJobProcessID3 = MyUtils.StartPerfClientJob("5001", "5000", clientJobName + "3", serverName, "65536", "3", logOutputFileName_ClientJob3, MyUtils.deployModeInProc, "5500");

            //Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob0, byteSize, 25, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob1, byteSize, 15, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob2, byteSize, 15, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob3, byteSize, 15, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server, byteSize, 15, false, testName, true,false);   // don't check for DONE sometimes not getting it ... not big deal

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(serverProcessID);

            MyUtils.KillProcess(clientJobProcessID0);
            MyUtils.KillProcess(clientJobProcessID1);
            MyUtils.KillProcess(clientJobProcessID2);
            MyUtils.KillProcess(clientJobProcessID3);

            // Verify Client
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob0);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob1);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob2);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob3);

            // Verify Server
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server);

            // Verify log files
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version, "4",false,false);

        }


        //** Upgrade scenario where the server is upgraded after server is finished  - all done InProc
        [TestMethod]
        public void AMB_InProc_UpgradeServerAFTERServerDone_Test()
        {
            Utilities MyUtils = new Utilities();

            //NOTE - the Cleanup has this hard coded so if this changes, update Cleanup section too
            string testName = "inprocupgradeafterserverdone";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = MyUtils.baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "4294967296";
            string newUpgradedPrimary = "becoming upgraded primary";

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
                AMB_Version = "0"  // client is always 0
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
                AMB_Version = "9"
            };
            MyUtils.CallAMB(AMB2, logOutputFileName_AMB2, AMB_ModeConsts.RegisterInstance);

            //Client Job Call
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "65536", "4", logOutputFileName_ClientJob,MyUtils.deployModeInProc,"1500");

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false,0,MyUtils.deployModeInProc,"2500");

            // Wait for client job to finish
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 30, false, testName, true); // number of bytes processed

            // kill Server 
            MyUtils.KillProcess(serverProcessID);

            // Run AMB again with new version # upped by 9 (10)
            string logOutputFileName_AMB2_Upgraded = testName + "_AMB2_Upgraded.log";
            AMB_Settings AMB2_Upgraded = new AMB_Settings
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
                AMB_Version = "9",
                AMB_UpgradeToVersion = "10"
            };
            MyUtils.CallAMB(AMB2_Upgraded, logOutputFileName_AMB2_Upgraded, AMB_ModeConsts.RegisterInstance);

            // start server again but with Upgrade = true
            string logOutputFileName_Server_upgraded = testName + "_Server_upgraded.log";
            int serverProcessID_upgraded = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server_upgraded, 1, true,0,MyUtils.deployModeInProc,"2500");

            //Delay until server upgrade is done
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server_upgraded, byteSize, 30, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server_upgraded, newUpgradedPrimary, 5, false, testName, true, false);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(serverProcessID_upgraded);

            // Verify Client
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);

            // Verify Server
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server_upgraded);

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB2.AMB_Version);
        }


        //** Upgrade scenario where the server is upgraded server before client is finished - all done InProc
        [TestMethod]
        public void AMB_InProc_UpgradeServerBEFOREServerDone_Test()
        {

            Utilities MyUtils = new Utilities();

            //NOTE - the Cleanup has this hard coded so if this changes, update Cleanup section too
            string testName = "inprocupgradebeforeserverdone";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = MyUtils.baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "13958643712";

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
                AMB_Version = "0"  // client is always 0
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
                AMB_Version = "10"
            };
            MyUtils.CallAMB(AMB2, logOutputFileName_AMB2, AMB_ModeConsts.RegisterInstance);

            //Client Job Call
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob,MyUtils.deployModeInProc,"1500");

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false,0,MyUtils.deployModeInProc,"2500");

            // Give it 5 seconds to do something before killing it
            MyUtils.TestDelay(5000);

            // kill Server 
            MyUtils.KillProcess(serverProcessID);

            // Run AMB again with new version # upped by 1 (11)
            string logOutputFileName_AMB2_Upgraded = testName + "_AMB2_Upgraded.log";
            AMB_Settings AMB2_Upgraded = new AMB_Settings
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
                AMB_Version = "10",
                AMB_UpgradeToVersion = "11"
            };
            MyUtils.CallAMB(AMB2_Upgraded, logOutputFileName_AMB2_Upgraded, AMB_ModeConsts.RegisterInstance);

            // start server again but with Upgrade = true
            string logOutputFileName_Server_upgraded = testName + "_Server_upgraded.log";
            int serverProcessID_upgraded = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server_upgraded, 1, true,0,MyUtils.deployModeInProc,"2500");

            //Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 25, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server_upgraded, byteSize, 15, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(serverProcessID_upgraded);

            // Verify Client
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);

            // don't do cmp for Server as it is changing too much between machines

            // Verify integrity of Ambrosia logs by replaying and TTD
            // Do not verify log file through replay / ttd - doesn't work when log files span different versions
            // MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB2.AMB_Version);

        }


        [TestCleanup()]
        public void Cleanup()
        {
            // Kill all ImmortalCoordinators, Job and Server exes
            Utilities MyUtils = new Utilities();
            MyUtils.InProcPipeTestCleanup();
        }


    }
}

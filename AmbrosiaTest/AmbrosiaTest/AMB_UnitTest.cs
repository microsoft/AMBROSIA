using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms; // need this to handle threading issue on sleeps
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Configuration;

namespace AmbrosiaTest
{
    /// <summary>
    /// Summary description for AMB_UnitTest
    /// </summary>
    [TestClass]
    public class AMB_UnitTest
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

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        //** Basic end to end test with minimal rounds and message size of 1GB ... could make it smaller and it would be faster.
        [TestMethod]
        public void UnitTest_BasicEndtoEnd_Test()
        {
            //NOTE - the Cleanup has this hard coded so if this changes, update Cleanup section too
            string testName = "unitendtoendtest";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "1073741824";

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
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "1024", "1", logOutputFileName_ClientJob, MyUtils.deployModeSecondProc);

            // Give it a few seconds to start
            Thread.Sleep(2000);

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false);

            //Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server, byteSize, 5, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(serverProcessID);
            MyUtils.KillProcess(ImmCoordProcessID1);
            MyUtils.KillProcess(ImmCoordProcessID2);

            // .netcore has slightly different cmp file - not crucial to try to have separate files
            if (MyUtils.NetFrameworkTestRun)
            {
                //Verify AMB 
                MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_AMB1);
                MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_AMB2);
            }

            // Verify Client
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);

            // Verify Server
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server);

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }


        //** Basic end to end test where something dies and restart it and works still
        [TestMethod]
        public void UnitTest_BasicRestartEndtoEnd_Test()
        {
            //NOTE - the Cleanup has this hard coded so if this changes, update Cleanup section too
            string testName = "unitendtoendrestarttest";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "1073741824";

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
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "1024", "1", logOutputFileName_ClientJob);

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false);

            // Give it 2 seconds to do something before killing it
            Thread.Sleep(2000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            //Kill Server at this point as well as ImmCoord2
            MyUtils.KillProcess(serverProcessID);
            MyUtils.KillProcess(ImmCoordProcessID2);

            //Restart ImmCoord2
            string logOutputFileName_ImmCoord2_Restarted = testName + "_ImmCoord2_Restarted.log";
            int ImmCoordProcessID2_Restarted = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2_Restarted);

            // Restart Server Process
            string logOutputFileName_Server_Restarted = testName + "_Server_Restarted.log";
            int serverProcessID_Restarted = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server_Restarted, 1, false);

            //Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 8, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server_Restarted, byteSize, 8, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(serverProcessID_Restarted);
            MyUtils.KillProcess(ImmCoordProcessID1);
            MyUtils.KillProcess(ImmCoordProcessID2_Restarted);

            // .netcore has slightly different cmp file - not crucial to try to have separate files
            if (MyUtils.NetFrameworkTestRun)
            {
                //Verify AMB 
                MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_AMB1);
                MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_AMB2);
            }

            // Verify Client
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);

            // Verify Server
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server_Restarted);

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }


        //****************************
        // The basic test of Active Active where kill primary server
        // 1 client 
        // 3 servers - primary, checkpointing secondary and active secondary (can become primary)
        //
        // killing first server (primary) will then have active secondary become primary
        // restarting first server will make it the active secondary
        //  
        //****************************
        [TestMethod]
        public void UnitTest_BasicActiveActive_KillPrimary_Test()
        {
            string testName = "unittestactiveactivekillprimary";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";

            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "2147481250";
            string newPrimary = "NOW I'm Primary";

            Utilities MyUtils = new Utilities();

            //AMB1 - primary -- in actuality, this is replica #0
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
            int ImmCoordProcessID1 = MyUtils.StartImmCoord(serverName, 1500, logOutputFileName_ImmCoord1,true, 0);

            //ImmCoord2
            string logOutputFileName_ImmCoord2 = testName + "_ImmCoord2.log";
            int ImmCoordProcessID2 = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2,true,1);

            //ImmCoord3
            string logOutputFileName_ImmCoord3 = testName + "_ImmCoord3.log";
            int ImmCoordProcessID3 = MyUtils.StartImmCoord(serverName, 3500, logOutputFileName_ImmCoord3,true,2);

            //ImmCoord4
            string logOutputFileName_ImmCoord4 = testName + "_ImmCoord4.log";
            int ImmCoordProcessID4 = MyUtils.StartImmCoord(clientJobName, 4500, logOutputFileName_ImmCoord4);

            //Server Call - primary
            string logOutputFileName_Server1 = testName + "_Server1.log";
            int serverProcessID1 = MyUtils.StartPerfServer("1001", "1000", clientJobName, serverName, logOutputFileName_Server1, 1, false);
            Thread.Sleep(1000); // give a second to make it a primary

            //Server Call - checkpointer
            string logOutputFileName_Server2 = testName + "_Server2.log";
            int serverProcessID2 = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server2, 1, false);
            Thread.Sleep(1000); // give a second

            //Server Call - active secondary
            string logOutputFileName_Server3 = testName + "_Server3.log";
            int serverProcessID3 = MyUtils.StartPerfServer("3001", "3000", clientJobName, serverName, logOutputFileName_Server3, 1, false);

            //Client Job Call
            // Pass in packet that isn't base 2
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("4001", "4000", clientJobName, serverName, "2500", "2", logOutputFileName_ClientJob);

            // Give it 3 seconds to do something before killing it
            Thread.Sleep(3000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            //Kill Primary Server (server1) at this point as well as ImmCoord1
            MyUtils.KillProcess(serverProcessID1);
            MyUtils.KillProcess(ImmCoordProcessID1);

            // at this point, server3 (active secondary) becomes primary 
            Thread.Sleep(1000);

            //Restart server1 (ImmCoord1 and server) ... this will become active secondary now
            string logOutputFileName_ImmCoord1_Restarted = testName + "_ImmCoord1_Restarted.log";
            int ImmCoordProcessID1_Restarted = MyUtils.StartImmCoord(serverName, 1500, logOutputFileName_ImmCoord1_Restarted,true,0);
            string logOutputFileName_Server1_Restarted = testName + "_Server1_Restarted.log";
            int serverProcessID_Restarted1 = MyUtils.StartPerfServer("1001", "1000", clientJobName, serverName, logOutputFileName_Server1_Restarted, 1, false);

            //Delay until finished ... looking at the most recent primary (server3) but also verify others hit done too
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server3, byteSize, 5, false, testName, true);  // Total Bytes received needs to be accurate
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 5, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server2, byteSize, 5, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server1_Restarted, byteSize, 5, false, testName, true);

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
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }

        //** Basic end to end test for the InProc TCP feature with minimal rounds and message size of 1GB ... could make it smaller and it would be faster.
        [TestMethod]
        public void UnitTest_BasicInProcTCPEndtoEnd_Test()
        {
            //NOTE - the Cleanup has this hard coded so if this changes, update Cleanup section too
            string testName = "unittestinproctcp";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "1073741824";

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

            //Client Job Call
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "1024", "1", logOutputFileName_ClientJob,MyUtils.deployModeInProcManual,"1500");

            // Give it a few seconds to start
            Thread.Sleep(2000);

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false,0, MyUtils.deployModeInProcManual,"2500");

            //Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server, byteSize, 5, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(serverProcessID);

            // .netcore has slightly different cmp file - not crucial to try to have separate files
            if (MyUtils.NetFrameworkTestRun)
            {
                //Verify AMB 
                MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_AMB1);
                MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_AMB2);

                // Verify Client - NetCore CLR bug causes extra info in the output for this so do not check for core run
                MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);

            }

            // Verify Server
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server);

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }


        //** Basic end to end test for the InProc TCP feature with minimal rounds and message size of 1GB ... could make it smaller and it would be faster.
        [TestMethod]
        public void UnitTest_BasicInProcPipeEndtoEnd_Test()
        {
            //NOTE - the Cleanup has this hard coded so if this changes, update Cleanup section too
            string testName = "unittestinprocpipe";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "1073741824";

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

            //Client Job Call
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "1024", "1", logOutputFileName_ClientJob,MyUtils.deployModeInProc,"1500");

            // Give it a few seconds to start
            Thread.Sleep(2000);

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false,0, MyUtils.deployModeInProc, "2500");

            //Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server, byteSize, 5, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(serverProcessID);

            // .netcore has slightly different cmp file - not crucial to try to have separate files
            if (MyUtils.NetFrameworkTestRun)
            {
                MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_AMB1);
                MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_AMB2);
            }

            // Verify Client
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);

            // Verify Server
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server);

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }


        [TestCleanup()]
        public void Cleanup()
        {
            // Kill all ImmCoord.Workers, Job and Server exes
            Utilities MyUtils = new Utilities();
            MyUtils.UnitTestCleanup();
        }


    }
}

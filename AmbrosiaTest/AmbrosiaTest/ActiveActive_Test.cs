using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Configuration;
using System.Threading;
using System.Windows.Forms; // need this to handle threading issue on sleeps

namespace AmbrosiaTest
{
    [TestClass]
    public class ActiveActive_Test
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
        public void AMB_ActiveActive_KillPrimary_Test()
        {
            string testName = "activeactivekillprimary";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "13958643712";
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
            int ImmCoordProcessID1 = MyUtils.StartImmCoord(serverName, 1500, logOutputFileName_ImmCoord1,true, 0);

            //ImmCoord2
            string logOutputFileName_ImmCoord2 = testName + "_ImmCoord2.log";
            int ImmCoordProcessID2 = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2, true, 1);

            //ImmCoord3
            string logOutputFileName_ImmCoord3 = testName + "_ImmCoord3.log";
            int ImmCoordProcessID3 = MyUtils.StartImmCoord(serverName, 3500, logOutputFileName_ImmCoord3,true,2);

            //ImmCoord4
            string logOutputFileName_ImmCoord4 = testName + "_ImmCoord4.log";
            int ImmCoordProcessID4 = MyUtils.StartImmCoord(clientJobName, 4500, logOutputFileName_ImmCoord4);

            //Server Call - primary
            string logOutputFileName_Server1 = testName + "_Server1.log";
            int serverProcessID1 = MyUtils.StartPerfServer("1001", "1000", clientJobName, serverName, logOutputFileName_Server1,1,false);
            Thread.Sleep(1000); // give a second to make it a primary

            //Server Call - checkpointer
            string logOutputFileName_Server2 = testName + "_Server2.log";
            int serverProcessID2 = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server2, 1, false);
            Thread.Sleep(1000); // give a second

            //Server Call - active secondary
            string logOutputFileName_Server3 = testName + "_Server3.log";
            int serverProcessID3 = MyUtils.StartPerfServer("3001", "3000", clientJobName, serverName, logOutputFileName_Server3, 1, false);

            //start Client Job 
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("4001", "4000", clientJobName, serverName, "65536","13",logOutputFileName_ClientJob);

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
            int serverProcessID_Restarted1 = MyUtils.StartPerfServer("1001", "1000", clientJobName, serverName, logOutputFileName_Server1_Restarted, 1, false);

            //Delay until finished ... looking at the most recent primary (server3) but also verify others hit done too
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server3, byteSize, 90, false, testName, true);  // Total Bytes received needs to be accurate
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 15, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server2, byteSize, 15, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server1_Restarted, byteSize, 15, false, testName, true);

            // Also verify ImmCoord has the string to show it is primary
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ImmCoord3, newPrimary, 5, false, testName, true, false);

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


        //****************************
        // The basic test of Active Active but killing check pointer
        // 1 client 
        // 3 servers - primary, checkpointing secondary and active secondary (can become primary)
        //
        // killing first check point leaves it without a check pointer, so others should work ok ... 
        // when a new instance is started, that new one will be the check pointer
        //  
        //****************************
        [TestMethod]
        public void AMB_ActiveActive_KillCheckPointer_Test()
        {
            string testName = "activeactivekillcheckpoint";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "5368709120";

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
            int ImmCoordProcessID1 = MyUtils.StartImmCoord(serverName, 1500, logOutputFileName_ImmCoord1,true,0);

            //ImmCoord2
            string logOutputFileName_ImmCoord2 = testName + "_ImmCoord2.log";
            int ImmCoordProcessID2 = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2,true,1);

            //ImmCoord3
            string logOutputFileName_ImmCoord3 = testName + "_ImmCoord3.log";
            int ImmCoordProcessID3 = MyUtils.StartImmCoord(serverName, 3500, logOutputFileName_ImmCoord3,true,2);

            //ImmCoord4
            string logOutputFileName_ImmCoord4 = testName + "_ImmCoord4.log";
            int ImmCoordProcessID4 = MyUtils.StartImmCoord(clientJobName, 4500, logOutputFileName_ImmCoord4);

            //start Client Job first ... to mix it up a bit (other tests has client start after server)
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("4001", "4000", clientJobName, serverName, "65536", "5", logOutputFileName_ClientJob);

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

            // Give it 10 seconds to do something before killing it
            Thread.Sleep(10000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            //Kill check point Server (server2) and ImmCoord
            MyUtils.KillProcess(serverProcessID2);
            MyUtils.KillProcess(ImmCoordProcessID2);

            // at this point, there isn't a check point
            Thread.Sleep(1000);

            //Restart server2 (ImmCoord2 and server) ... this will become check point again
            string logOutputFileName_ImmCoord2_Restarted = testName + "_ImmCoord2_Restarted.log";
            int ImmCoordProcessID2_Restarted = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2_Restarted,true, 1);
            string logOutputFileName_Server2_Restarted = testName + "_Server2_Restarted.log";
            int serverProcessID_Restarted2 = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server2_Restarted, 1, false);

            //Delay until finished ... looking at the primary (server1) but also verify others hit done too
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server1, byteSize, 30, false, testName, true);  // Total Bytes received needs to be accurate
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 15, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server2_Restarted, byteSize, 15, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server3, byteSize, 15, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(serverProcessID1);
            MyUtils.KillProcess(serverProcessID_Restarted2);
            MyUtils.KillProcess(serverProcessID3);
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(ImmCoordProcessID1);
            MyUtils.KillProcess(ImmCoordProcessID2_Restarted);
            MyUtils.KillProcess(ImmCoordProcessID3);
            MyUtils.KillProcess(ImmCoordProcessID4);

            // Verify cmp files for client and 3 servers
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server1);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server2_Restarted);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server3);

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }


        //****************************
        // The basic test of Active Active but killing active secondary
        // 1 client 
        // 3 servers - primary, checkpointing secondary and active secondary (can become primary)
        //
        // killing secondary leaves without secondary, so others should work ok ... 
        // when a new instance is started, that new one will be the secondary
        //  
        //****************************
        [TestMethod]
        public void AMB_ActiveActive_KillSecondary_Test()
        {
            string testName = "activeactivekillsecondary";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "6442450944";

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
            int ImmCoordProcessID1 = MyUtils.StartImmCoord(serverName, 1500, logOutputFileName_ImmCoord1,true,0);

            //ImmCoord2
            string logOutputFileName_ImmCoord2 = testName + "_ImmCoord2.log";
            int ImmCoordProcessID2 = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2,true, 1);

            //ImmCoord3
            string logOutputFileName_ImmCoord3 = testName + "_ImmCoord3.log";
            int ImmCoordProcessID3 = MyUtils.StartImmCoord(serverName, 3500, logOutputFileName_ImmCoord3,true, 2);

            //ImmCoord4
            string logOutputFileName_ImmCoord4 = testName + "_ImmCoord4.log";
            int ImmCoordProcessID4 = MyUtils.StartImmCoord(clientJobName, 4500, logOutputFileName_ImmCoord4);

            //start Client Job first ... to mix it up a bit (other tests has client start after server)
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("4001", "4000", clientJobName, serverName, "65536", "6", logOutputFileName_ClientJob);

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

            // Give it 5 seconds to do something before killing it
            Thread.Sleep(5000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            //Kill active secondary (server3 and ImmCoord)
            MyUtils.KillProcess(serverProcessID3);
            MyUtils.KillProcess(ImmCoordProcessID3);

            // at this point, there isn't a check point
            Thread.Sleep(1000);

            //Restart server3 (ImmCoord3 and server) ... this will become active secondary again
            string logOutputFileName_ImmCoord3_Restarted = testName + "_ImmCoord3_Restarted.log";
            int ImmCoordProcessID3_Restarted = MyUtils.StartImmCoord(serverName, 3500, logOutputFileName_ImmCoord3_Restarted,true, 2);
            string logOutputFileName_Server3_Restarted = testName + "_Server3_Restarted.log";
            int serverProcessID_Restarted3 = MyUtils.StartPerfServer("3001", "3000", clientJobName, serverName, logOutputFileName_Server3_Restarted, 1, false);

            //Delay until finished ... looking at the primary (server1) but also verify others hit done too
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server1, byteSize, 30, false, testName, true);  // Total Bytes received needs to be accurate
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 15, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server3_Restarted, byteSize, 15, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(serverProcessID2);
            MyUtils.KillProcess(serverProcessID_Restarted3);
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(serverProcessID1);
            MyUtils.KillProcess(ImmCoordProcessID2);
            MyUtils.KillProcess(ImmCoordProcessID3_Restarted);
            MyUtils.KillProcess(ImmCoordProcessID4);
            MyUtils.KillProcess(ImmCoordProcessID1);

            // Verify cmp files for client and 3 servers
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server1);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server2);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server3);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server3_Restarted);

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }


        //****************************
        // The basic test of Active Active but killing check pointer and Secondary
        // 1 client 
        // 3 servers - primary, checkpointing secondary and active secondary
        //
        // killing first check point leaves it without a check pointer and killing secondary leaves without 
        // secondary but starting a new instance will start with check pointer then second new one would be 
        // secondary
        //  
        //****************************
        [TestMethod]
        public void AMB_ActiveActive_KillSecondaryAndCheckPointer_Test()
        {
            string testName = "activeactivekillsecondaryandcheckpoint";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";

            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "13958643712";

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

            //AMB3 - active secondary
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
            int ImmCoordProcessID1 = MyUtils.StartImmCoord(serverName, 1500, logOutputFileName_ImmCoord1,true,0);

            //ImmCoord2
            string logOutputFileName_ImmCoord2 = testName + "_ImmCoord2.log";
            int ImmCoordProcessID2 = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2,true,1);

            //ImmCoord3
            string logOutputFileName_ImmCoord3 = testName + "_ImmCoord3.log";
            int ImmCoordProcessID3 = MyUtils.StartImmCoord(serverName, 3500, logOutputFileName_ImmCoord3,true,2);

            //ImmCoord4
            string logOutputFileName_ImmCoord4 = testName + "_ImmCoord4.log";
            int ImmCoordProcessID4 = MyUtils.StartImmCoord(clientJobName, 4500, logOutputFileName_ImmCoord4);

            //start Client Job 
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("4001", "4000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob);

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

            // Give it 5 seconds to do something before killing it
            Thread.Sleep(5000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            //Kill check point Server (server2) and ImmCoord
            MyUtils.KillProcess(serverProcessID2);
            MyUtils.KillProcess(ImmCoordProcessID2);

            // at this point, there isn't a check point
            Thread.Sleep(1000);

            //Now kill secondary Server (server3) and ImmCoord
            MyUtils.KillProcess(serverProcessID3);
            MyUtils.KillProcess(ImmCoordProcessID3);

            // at this point, there isn't a secondary
            Thread.Sleep(1000);

            //Restart server2 (ImmCoord2 and server) ... this will become check point again because checkpoint has priority
            string logOutputFileName_ImmCoord2_Restarted = testName + "_ImmCoord2_Restarted.log";
            int ImmCoordProcessID2_Restarted = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2_Restarted,true, 1);
            string logOutputFileName_Server2_Restarted = testName + "_Server2_Restarted.log";
            int serverProcessID_Restarted2 = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server2_Restarted, 1, false);

            //Restart server3 (ImmCoord3 and server) ... this will become active secondary again
            string logOutputFileName_ImmCoord3_Restarted = testName + "_ImmCoord3_Restarted.log";
            int ImmCoordProcessID3_Restarted = MyUtils.StartImmCoord(serverName, 3500, logOutputFileName_ImmCoord3_Restarted,true,2);
            string logOutputFileName_Server3_Restarted = testName + "_Server3_Restarted.log";
            int serverProcessID_Restarted3 = MyUtils.StartPerfServer("3001", "3000", clientJobName, serverName, logOutputFileName_Server3_Restarted, 1, false);

            //Delay until finished ... looking at the primary (server1) but also verify others hit done too
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server1, byteSize, 30, false, testName, true);  // Total Bytes received needs to be accurate
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 15, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server2_Restarted, byteSize, 15, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server3_Restarted, byteSize, 15, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(serverProcessID1);
            MyUtils.KillProcess(serverProcessID_Restarted2);
            MyUtils.KillProcess(serverProcessID_Restarted3);
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(ImmCoordProcessID1);
            MyUtils.KillProcess(ImmCoordProcessID2_Restarted);
            MyUtils.KillProcess(ImmCoordProcessID3_Restarted);
            MyUtils.KillProcess(ImmCoordProcessID4);

            // Verify cmp files for client and 3 servers
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server1);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server2_Restarted);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server3_Restarted);

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }


        //****************************
        // More complex test of Active Active for client and server
        // 3 clients - primary, checkpointing secondary and active secondary 
        // 3 servers - primary, checkpointing secondary and active secondary
        //
        // Kill primary of client and primary of server and then restart both
        //  
        //****************************
        [TestMethod]
        public void AMB_ActiveActive_Kill_Client_And_Server_Test()
        {
            string testName = "activeactivekillclientandserver";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "13958643712";
            string newPrimary = "NOW I'm Primary";

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


            //ImmCoord1
            string logOutputFileName_ImmCoord1 = testName + "_ImmCoord1.log";
            int ImmCoordProcessID1 = MyUtils.StartImmCoord(serverName, 1500, logOutputFileName_ImmCoord1,true,0);

            //ImmCoord2
            string logOutputFileName_ImmCoord2 = testName + "_ImmCoord2.log";
            int ImmCoordProcessID2 = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2,true,1);

            //ImmCoord3
            string logOutputFileName_ImmCoord3 = testName + "_ImmCoord3.log";
            int ImmCoordProcessID3 = MyUtils.StartImmCoord(serverName, 3500, logOutputFileName_ImmCoord3,true,2);

            //ImmCoord4
            string logOutputFileName_ImmCoord4 = testName + "_ImmCoord4.log";
            int ImmCoordProcessID4 = MyUtils.StartImmCoord(clientJobName, 4500, logOutputFileName_ImmCoord4,true,0);

            //ImmCoord5
            string logOutputFileName_ImmCoord5 = testName + "_ImmCoord5.log";
            int ImmCoordProcessID5 = MyUtils.StartImmCoord(clientJobName, 5500, logOutputFileName_ImmCoord5,true, 1);

            //ImmCoord6
            string logOutputFileName_ImmCoord6 = testName + "_ImmCoord6.log";
            int ImmCoordProcessID6 = MyUtils.StartImmCoord(clientJobName, 6500, logOutputFileName_ImmCoord6,true, 2);

            //start Client Job - primary
            string logOutputFileName_ClientJob1 = testName + "_ClientJob1.log";
            int clientJobProcessID1 = MyUtils.StartPerfClientJob("4001", "4000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob1);

            //start Client Job - checkpoint
            string logOutputFileName_ClientJob2 = testName + "_ClientJob2.log";
            int clientJobProcessID2 = MyUtils.StartPerfClientJob("5001", "5000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob2);

            //start Client Job - secondary
            string logOutputFileName_ClientJob3 = testName + "_ClientJob3.log";
            int clientJobProcessID3 = MyUtils.StartPerfClientJob("6001", "6000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob3);

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

            // Give it 10 seconds to do something before killing it
            Thread.Sleep(10000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            //Kill primary Server and ImmCoord
            MyUtils.KillProcess(serverProcessID1);
            MyUtils.KillProcess(ImmCoordProcessID1);

            // at this point, secondary is becoming primary server
            Thread.Sleep(1000);

            //Now kill primary client and ImmCoord (ImmCoord4)
            MyUtils.KillProcess(clientJobProcessID1);
            MyUtils.KillProcess(ImmCoordProcessID4);

            // at this point, secondary client is becoming primary client
            Thread.Sleep(1000);

            //Restart server1 (ImmCoord1 and server1) ... this will become secondary server
            string logOutputFileName_ImmCoord1_Restarted = testName + "_ImmCoord1_Restarted.log";
            int ImmCoordProcessID1_Restarted = MyUtils.StartImmCoord(serverName, 1500, logOutputFileName_ImmCoord1_Restarted,true, 0);
            string logOutputFileName_Server1_Restarted = testName + "_Server1_Restarted.log";
            int serverProcessID_Restarted1 = MyUtils.StartPerfServer("1001", "1000", clientJobName, serverName, logOutputFileName_Server1_Restarted, 1, false);

            //Restart client1 (CR4 and client1) ... this will become active secondary again
            string logOutputFileName_ImmCoord4_Restarted = testName + "_ImmCoord4_Restarted.log";
            int ImmCoordProcessID4_Restarted = MyUtils.StartImmCoord(clientJobName, 4500, logOutputFileName_ImmCoord4_Restarted,true, 0);
            string logOutputFileName_ClientJob1_Restarted = testName + "_ClientJob1_Restarted.log";
            int clientJobProcessID_Restarted1 = MyUtils.StartPerfClientJob("4001", "4000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob1_Restarted);

            //Delay until finished ... looking at the primary (server1) but also verify others hit done too
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server1_Restarted, byteSize, 40, false, testName, true);  // Total Bytes received needs to be accurate
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server2, byteSize, 15, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server3, byteSize, 15, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob1_Restarted, byteSize, 20, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob2, byteSize, 15, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob3, byteSize, 15, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(serverProcessID_Restarted1);
            MyUtils.KillProcess(serverProcessID2);
            MyUtils.KillProcess(serverProcessID3);
            MyUtils.KillProcess(clientJobProcessID_Restarted1);
            MyUtils.KillProcess(clientJobProcessID2);
            MyUtils.KillProcess(clientJobProcessID3);
            MyUtils.KillProcess(ImmCoordProcessID1_Restarted);
            MyUtils.KillProcess(ImmCoordProcessID2);
            MyUtils.KillProcess(ImmCoordProcessID3);
            MyUtils.KillProcess(ImmCoordProcessID4_Restarted);
            MyUtils.KillProcess(ImmCoordProcessID5);
            MyUtils.KillProcess(ImmCoordProcessID6);

            // Verify cmp files for client and 3 servers
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob1_Restarted);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob2);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob3);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server1_Restarted);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server2);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server3);

            // Also verify ImmCoord has the string to show it is primary for both server and client
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ImmCoord3, newPrimary, 5, false, testName, true,false);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ImmCoord6, newPrimary, 5, false, testName, true,false);

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }

        //****************************
        // Most complex test of Active Active for client and server
        // 3 clients - primary, checkpointing secondary and active secondary 
        // 3 servers - primary, checkpointing secondary and active secondary
        //
        // Kill all aspects of the system and restart
        //  
        //****************************
        [TestMethod]
        public void AMB_ActiveActive_Kill_All_Test()
        {
            string testName = "activeactivekillall";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "13958643712";
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
            int ImmCoordProcessID1 = MyUtils.StartImmCoord(serverName, 1500, logOutputFileName_ImmCoord1,true, 0);
            Thread.Sleep(1000); 
            string logOutputFileName_Server1 = testName + "_Server1.log";
            int serverProcessID1 = MyUtils.StartPerfServer("1001", "1000", clientJobName, serverName, logOutputFileName_Server1, 1, false);

            //Server 2
            string logOutputFileName_ImmCoord2 = testName + "_ImmCoord2.log";
            int ImmCoordProcessID2 = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2,true, 1);
            Thread.Sleep(1000); // give a second
            string logOutputFileName_Server2 = testName + "_Server2.log";
            int serverProcessID2 = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server2, 1, false);

            //Server 3
            string logOutputFileName_ImmCoord3 = testName + "_ImmCoord3.log";
            int ImmCoordProcessID3 = MyUtils.StartImmCoord(serverName, 3500, logOutputFileName_ImmCoord3,true, 2);
            string logOutputFileName_Server3 = testName + "_Server3.log";
            int serverProcessID3 = MyUtils.StartPerfServer("3001", "3000", clientJobName, serverName, logOutputFileName_Server3, 1, false);

            //Client 1
            string logOutputFileName_ImmCoord4 = testName + "_ImmCoord4.log";
            int ImmCoordProcessID4 = MyUtils.StartImmCoord(clientJobName, 4500, logOutputFileName_ImmCoord4, true, 0);
            Thread.Sleep(1000); // give a second
            string logOutputFileName_ClientJob1 = testName + "_ClientJob1.log";
            int clientJobProcessID1 = MyUtils.StartPerfClientJob("4001", "4000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob1);

            //Client 2
            string logOutputFileName_ImmCoord5 = testName + "_ImmCoord5.log";
            int ImmCoordProcessID5 = MyUtils.StartImmCoord(clientJobName, 5500, logOutputFileName_ImmCoord5,true, 1);
            Thread.Sleep(1000); // give a second
            string logOutputFileName_ClientJob2 = testName + "_ClientJob2.log";
            int clientJobProcessID2 = MyUtils.StartPerfClientJob("5001", "5000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob2);

            //Client 3
            string logOutputFileName_ImmCoord6 = testName + "_ImmCoord6.log";
            int ImmCoordProcessID6 = MyUtils.StartImmCoord(clientJobName, 6500, logOutputFileName_ImmCoord6,true, 2);
            Thread.Sleep(1000); // give a second
            string logOutputFileName_ClientJob3 = testName + "_ClientJob3.log";
            int clientJobProcessID3 = MyUtils.StartPerfClientJob("6001", "6000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob3);

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
            int ImmCoordProcessID1_Restarted = MyUtils.StartImmCoord(serverName, 1500, logOutputFileName_ImmCoord1_Restarted,true, 0);
            string logOutputFileName_Server1_Restarted = testName + "_Server1_Restarted.log";
            int serverProcessID_Restarted1 = MyUtils.StartPerfServer("1001", "1000", clientJobName, serverName, logOutputFileName_Server1_Restarted, 1, false);
            string logOutputFileName_ImmCoord2_Restarted = testName + "_ImmCoord2_Restarted.log";
            int ImmCoordProcessID2_Restarted = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2_Restarted,true, 1);
            string logOutputFileName_Server2_Restarted = testName + "_Server2_Restarted.log";
            int serverProcessID_Restarted2 = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server2_Restarted, 1, false);
            string logOutputFileName_ImmCoord3_Restarted = testName + "_ImmCoord3_Restarted.log";
            int ImmCoordProcessID3_Restarted = MyUtils.StartImmCoord(serverName, 3500, logOutputFileName_ImmCoord3_Restarted,true, 2);
            string logOutputFileName_Server3_Restarted = testName + "_Server3_Restarted.log";
            int serverProcessID_Restarted3 = MyUtils.StartPerfServer("3001", "3000", clientJobName, serverName, logOutputFileName_Server3_Restarted, 1, false);

            //Restart clients
            string logOutputFileName_ImmCoord4_Restarted = testName + "_ImmCoord4_Restarted.log";
            int ImmCoordProcessID4_Restarted = MyUtils.StartImmCoord(clientJobName, 4500, logOutputFileName_ImmCoord4_Restarted, true, 0);
            string logOutputFileName_ClientJob1_Restarted = testName + "_ClientJob1_Restarted.log";
            int clientJobProcessID_Restarted1 = MyUtils.StartPerfClientJob("4001", "4000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob1_Restarted);
            string logOutputFileName_ImmCoord5_Restarted = testName + "_ImmCoord5_Restarted.log";
            int ImmCoordProcessID5_Restarted = MyUtils.StartImmCoord(clientJobName, 5500, logOutputFileName_ImmCoord5_Restarted, true, 1);
            string logOutputFileName_ClientJob2_Restarted = testName + "_ClientJob2_Restarted.log";
            int clientJobProcessID_Restarted2 = MyUtils.StartPerfClientJob("5001", "5000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob2_Restarted);
            string logOutputFileName_ImmCoord6_Restarted = testName + "_ImmCoord6_Restarted.log";
            int ImmCoordProcessID6_Restarted = MyUtils.StartImmCoord(clientJobName, 6500, logOutputFileName_ImmCoord6_Restarted, true, 2);
            string logOutputFileName_ClientJob3_Restarted = testName + "_ClientJob3_Restarted.log";
            int clientJobProcessID_Restarted3 = MyUtils.StartPerfClientJob("6001", "6000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob3_Restarted);
            
            //Delay until finished ... looking at the primary (server1) but also verify others hit done too
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server1_Restarted, byteSize, 75, false, testName, true);  // Total Bytes received needs to be accurate
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
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }

        //****************************
        // The test where add node to the active active before killing primary
        // 1 client 
        // 3 servers - primary, checkpointing secondary and active secondary 
        // 
        // Then add a 4th server which is an active secondary to the active secondary
        // Kill Primary which makes active secondary the primary and 4th the secondary
        // Kill the new primary (which was originally the secondary)
        // Now Server4 becomes the primary
        //
        //****************************
        [TestMethod]
        public void AMB_ActiveActive_AddNodeBeforeKillPrimary_Test()
        {
            string testName = "activeactiveaddnotekillprimary";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "13958643712";

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

            //AMB5 - Job -- make it #5 as server 4 will be created later which will use ImmCoord4
            string logOutputFileName_AMB5 = testName + "_AMB5.log";
            AMB_Settings AMB5 = new AMB_Settings
            {
                AMB_ServiceName = clientJobName,
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

            //ImmCoord1
            string logOutputFileName_ImmCoord1 = testName + "_ImmCoord1.log";
            int ImmCoordProcessID1 = MyUtils.StartImmCoord(serverName, 1500, logOutputFileName_ImmCoord1,true, 0);

            //ImmCoord2
            string logOutputFileName_ImmCoord2 = testName + "_ImmCoord2.log";
            int ImmCoordProcessID2 = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2,true, 1);

            //ImmCoord3
            string logOutputFileName_ImmCoord3 = testName + "_ImmCoord3.log";
            int ImmCoordProcessID3 = MyUtils.StartImmCoord(serverName, 3500, logOutputFileName_ImmCoord3,true, 2);

            //ImmCoord4
            string logOutputFileName_ImmCoord5 = testName + "_ImmCoord5.log";
            int ImmCoordProcessID5 = MyUtils.StartImmCoord(clientJobName, 5500, logOutputFileName_ImmCoord5);

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

            //start Client Job 
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("5001", "5000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob);

            // At this point everything running, so add another server node (server4) - AMB, ImmCoord and Server
            string logOutputFileName_AMB4 = testName + "_AMB4.log";
            AMB_Settings AMB4 = new AMB_Settings
            {
                AMB_ReplicaNumber = "3",
                AMB_ServiceName = serverName,
                AMB_ImmCoordName = testName + "immcoord4",
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
            MyUtils.CallAMB(AMB4, logOutputFileName_AMB4, AMB_ModeConsts.AddReplica);

            //ImmCoord4 - which is server 4  
            string logOutputFileName_ImmCoord4 = testName + "_ImmCoord4.log";
            int ImmCoordProcessID4 = MyUtils.StartImmCoord(serverName, 4500, logOutputFileName_ImmCoord4,true, 3);

            //Server Call - active secondary to the active secondary -- there isn't a server
            string logOutputFileName_Server4 = testName + "_Server4.log";
            int serverProcessID4 = MyUtils.StartPerfServer("4001", "4000", clientJobName, serverName, logOutputFileName_Server4, 1, false);

            // Give it 10 seconds to do something before killing it
            Thread.Sleep(10000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            //Kill Primary Server (server1) at this point as well as ImmCoord1
            MyUtils.KillProcess(serverProcessID1);
            MyUtils.KillProcess(ImmCoordProcessID1);

            // at this point, server3 (active secondary) becomes primary and server4 becomes active secondary
            Thread.Sleep(10000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            //Kill new Primary Server (server3) at this point as well as ImmCoord3
            MyUtils.KillProcess(serverProcessID3);
            MyUtils.KillProcess(ImmCoordProcessID3);

            // at this point, server4 which was active secondary to active secondary then became 
            // active secondary because server3 became primary
            // but when server3 (new primary) died, server4 became new primary
            Thread.Sleep(2000);

            // Do nothing with Server1 and server3 as they were killed as part of the process

            //Delay until finished ... looking at the most recent primary (server4) but also verify others hit done too
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server4, byteSize, 30, false, testName, true);  // Total Bytes received needs to be accurate
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 15, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server2, byteSize, 15, false, testName, true);

            // Also verify ImmCoord has the string to show server3 was primary then server4 became primary
            //*** Note - can't verify which one will be primary because both Server3 and Server4 are secondary
            //** They both are trying to take over primary if it dies. No way of knowing which one is.
            //pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ImmCoord3, newPrimary, 1, false, testName, true,false);
            //pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ImmCoord4, newPrimary, 1, false, testName, true,false);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(serverProcessID2);
            MyUtils.KillProcess(serverProcessID4);
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(ImmCoordProcessID2);
            MyUtils.KillProcess(ImmCoordProcessID4);
            MyUtils.KillProcess(ImmCoordProcessID5);

            // Verify cmp files for client and servers that weren't killed
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server4);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server2);

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }



        [TestCleanup()]
        public void Cleanup()
        {
            // Kill all ImmortalCoordinators, Job and Server exes
            Utilities MyUtils = new Utilities();
            MyUtils.TestCleanup();
        }
    }
}

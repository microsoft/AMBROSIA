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



//**** Tests (TO DO) ***
/*
 ** In Proc - TCP  (TO DO) **
- Kill Job
- Kill Server
* Save Logs to File and Blob
* Save Logs to Blob
* Upgrade Server
  
*/

/*
 1.	The usual way with an external IC. Note that the storage type (logs or files) is determined by the ImmortalCoordinator command line when the IC is started as a second process:
 
Server.exe -j=jgjob -s=jgserver -rp=2001 -sp=2000      or
Server.exe -j=jgjob -s=jgserver -rp=2001 -sp=2000 -d=secondproc
 
2.	IC in proc using the new Deploy gesture. Note that there are no longer rp and sp ports since we are using pipes instead of TCP. Also, you SHOULDN’T start an IC process since it is started in proc. Also note the icp flag, which is moved to here since there is no separate ImmortalCoordinator process. Also, this automatically chooses files as the log store, and picks up the log directory from the instance registration parameter (no override allowed right now in Server and Job command lines, but we could add if desired):
 
Server.exe -j=jgjob -s=jgserver -nbd -d=inprocdeploy -icp=2500
 
3.	IC in proc using a more manual gesture. You DO need to specify an rp and sp here since we are not using pipes with this gesture. This is something I did for O365 to make what they’re doing easier. I would expect everyone else to use approach #2 for in proc:
Server.exe -j=jgjob -s=jgserver -rp=2001 -sp=2000 -nbd -d=inprocmanual -icp=2500
 */


namespace AmbrosiaTest
{
    /// <summary>
    /// Summary description for InProc_Test
    /// </summary>
    [TestClass]
    public class InProc_TCP_Test
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


        //** Basic end to end test for the InProc TCP feature where Client is InProc and Server is Two Proc
        [TestMethod]
        public void AMB_InProc_TCP_ClientOnly_Test()
        {
            //NOTE - the Cleanup has this hard coded so if this changes, update Cleanup section too
            string testName = "inproctcpclientonly";
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

            //ImmCoord2
            string logOutputFileName_ImmCoord2 = testName + "_ImmCoord2.log";
            int ImmCoordProcessID2 = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2);

            //Client Job Call
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "1024", "1", logOutputFileName_ClientJob, MyUtils.deployModeInProcManual, "1500");

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
            MyUtils.KillProcess(ImmCoordProcessID2);

            // Verify Client
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);

            // Verify Server
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server);

            // Verify integrity of Ambrosia logs by replaying
            // Unable to verify when client files in different location than server log - TO DO: modify method to do this
            //          MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }



        //** Basic end to end test for the InProc TCP feature where Server is InProc and Client is Two Proc
        [TestMethod]
        public void AMB_InProc_TCP_ServerOnly_Test()
        {
            //NOTE - the Cleanup has this hard coded so if this changes, update Cleanup section too
            string testName = "inproctcpserveronly";
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

            //Client Job Call
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "1024", "1", logOutputFileName_ClientJob, MyUtils.deployModeSecondProc);

            // Give it a few seconds to start
            Thread.Sleep(2000);

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false, 0, MyUtils.deployModeInProcManual, "2500");

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
            // Unable to verify when client files in different location than server log - TO DO: modify method to do this
            //          MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }


        //** Basic end to end test for the InProc where client is Pipe and Server is TCP.
        [TestMethod]
        public void AMB_InProc_ClientTCP_ServerPipe_Test()
        {
            //NOTE - the Cleanup has this hard coded so if this changes, update Cleanup section too
            string testName = "inprocclienttcpserverpipe";
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
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "1024", "1", logOutputFileName_ClientJob, MyUtils.deployModeInProcManual, "1500");

            // Give it a few seconds to start
            Thread.Sleep(2000);

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false, 0, MyUtils.deployModeInProc, "2500");

            //Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server, byteSize, 5, false, testName, true);

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

        //** Basic end to end test for the InProc where client is Pipe and Server is TCP.
        [TestMethod]
        public void AMB_InProc_ClientPipe_ServerTCP_Test()
        {
            //NOTE - the Cleanup has this hard coded so if this changes, update Cleanup section too
            string testName = "inprocclientpipeservertcp";
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
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "1024", "1", logOutputFileName_ClientJob, MyUtils.deployModeInProc, "1500");

            // Give it a few seconds to start
            Thread.Sleep(2000);

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false, 0, MyUtils.deployModeInProcManual, "2500");

            //Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server, byteSize, 5, false, testName, true);

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

        //** Test starts job and server then kills the job and restarts it and runs to completion
        //** NOTE - this actually kills job once, restarts it, kills again and then restarts it again
        [TestMethod]
        public void AMB_InProc_TCP_KillJob_Test()
        {
            //NOTE - the Cleanup has test name hard coded so if this changes, update Cleanup section too
            string testName = "inproctcpkilljobtest";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "13958643712";

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
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob, MyUtils.deployModeInProcManual, "1500");

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false, 0, MyUtils.deployModeInProcManual, "2500");

            // Give it 5seconds to do something before killing it
            Thread.Sleep(5000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            //Kill job at this point
            MyUtils.KillProcess(clientJobProcessID);

            // Restart Job Process
            string logOutputFileName_ClientJob_Restarted = testName + "_ClientJob_Restarted.log";
            int clientJobProcessID_Restarted = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob_Restarted, MyUtils.deployModeInProcManual, "1500");

            // Give it 5seconds to do something before killing it again
            Thread.Sleep(5000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            //Kill job at this point 
            MyUtils.KillProcess(clientJobProcessID_Restarted);

            // Restart Job Process Again
            string logOutputFileName_ClientJob_Restarted_Again = testName + "_ClientJob_Restarted_Again.log";
            int clientJobProcessID_Restarted_Again = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob_Restarted_Again, MyUtils.deployModeInProcManual, "1500");

            //Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob_Restarted_Again, byteSize, 15, false, testName, true); // Total bytes received
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server, byteSize, 15, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID_Restarted_Again);
            MyUtils.KillProcess(serverProcessID);

            // Verify Client (before and after restart)
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob_Restarted);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob_Restarted_Again);

            // Verify Server
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server);

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }

        //** Test starts job and server then kills the server and restarts it and runs to completion
        [TestMethod]
        public void AMB_InProc_TCP_KillServer_Test()
        {
            //NOTE - the Cleanup has test name hard coded so if this changes, update Cleanup section too
            string testName = "inproctcpkillservertest";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string byteSize = "13958643712";

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

            //Client Job Call
            string logOutputFileName_ClientJob = testName + "_ClientJob.log";
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob, MyUtils.deployModeInProcManual, "1500");

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false, 0, MyUtils.deployModeInProcManual, "2500");

            // Give it 10 seconds to do something before killing it
            Thread.Sleep(10000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            //Kill Server at this point as well as ImmCoord2
            MyUtils.KillProcess(serverProcessID);

            // Restart Server Process
            string logOutputFileName_Server_Restarted = testName + "_Server_Restarted.log";
            int serverProcessID_Restarted = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server_Restarted, 1, false, 0, MyUtils.deployModeInProcManual, "2500");

            //Delay until client is done - also check Server just to make sure
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server_Restarted, byteSize, 25, false, testName, true);  // Total Bytes received needs to be accurate
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, byteSize, 15, false, testName, true);

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(serverProcessID_Restarted);

            // Verify Server (before and after restart)
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server);
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_Server_Restarted);

            // Verify Client
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob);

            // Verify integrity of Ambrosia logs by replaying
            MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, AMB1.AMB_Version);
        }



        [TestCleanup()]
        public void Cleanup()
        {

            // Kill all ImmortalCoordinators, Job and Server exes
            Utilities MyUtils = new Utilities();
            MyUtils.InProcTCPTestCleanup();
        }


    }
}

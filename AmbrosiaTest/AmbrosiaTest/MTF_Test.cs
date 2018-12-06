using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Configuration;
using System.Threading;
using System.Windows.Forms; // need this to handle threading issue on sleeps

namespace AmbrosiaTest
{
    [TestClass]
    public class MTF_Test
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
        // The basic test of job and server just running for very long time
        // This has Persist Logs = Y for both Job and Server
        // Set Server \ Job to exchange random sized 
        //****************************
        [TestMethod]
        public void AMB_MTF_KILL_PERSIST_Test()
        {
            string testName = "mtfkillpersist";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            Random rnd = new Random();
            bool pass = false;
            int minsBetweenKills = 2;

            //****************** MTF Settings ***************
            // ** Total bytes received can vary a bit so can't use the totalNumBytesReceived 
            int numRounds = 5; long totalNumBytesReceived = 5368709120;
            //int numRounds = 25; long totalNumBytesReceived = 26843545600;
            //int numRounds = 100; long totalNumBytesReceived = 107374182400;
            //int numRounds = 500; long totalNumBytesReceived = 536870912000; 
            //int numRounds = 1000; long totalNumBytesReceived = 1073741824000; 
            //************************

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
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "65536", numRounds.ToString(), logOutputFileName_ClientJob);

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false);

            //Loop here of kill and restart server / client 
            while (pass == false)
            {

                // Check for it to be done - just return value and don't pop exception
                // Do this check here instead of at the end to give the delay before killing processes
                pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, "DONE", minsBetweenKills, true, testName, false);

                // if passes we don't want to restart things and lose the logs
                if (pass == false)
                {
                    // set the random seed so it is reproducible
                    // randomly decide between killing job, server or both
                    int target = rnd.Next(1, 4);

                    string logInfo = "<DEBUG> Random kill (1=C, 2=S, 3=B) " + target.ToString();
                    MyUtils.LogDebugInfo(logInfo);

                    switch (target)
                    {
                        case 1:
                            // Kill client and restart it 
                            MyUtils.KillProcess(clientJobProcessID);
                            MyUtils.KillProcess(ImmCoordProcessID1);

                            Thread.Sleep(2000);

                            // restart ImmCoord and job.exe
                            ImmCoordProcessID1 = MyUtils.StartImmCoord(clientJobName, 1500, logOutputFileName_ImmCoord1);
                            //Client Job Call
                            logOutputFileName_ClientJob = testName + "_ClientJob.log";
                            clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "65536", numRounds.ToString(), logOutputFileName_ClientJob);
                            break;
                        case 2:
                            // Kill Server and restart it 
                            MyUtils.KillProcess(serverProcessID);
                            MyUtils.KillProcess(ImmCoordProcessID2);

                            Thread.Sleep(2000);

                            // Restart ImmCoord and server
                            ImmCoordProcessID2 = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2);
                            serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false);

                            break;
                        case 3:
                            // Kill client and server and restart them
                            MyUtils.KillProcess(clientJobProcessID);
                            MyUtils.KillProcess(serverProcessID);
                            MyUtils.KillProcess(ImmCoordProcessID1);
                            MyUtils.KillProcess(ImmCoordProcessID2);

                            Thread.Sleep(2000);

                            ImmCoordProcessID1 = MyUtils.StartImmCoord(clientJobName, 1500, logOutputFileName_ImmCoord1);
                            clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "65536", numRounds.ToString(), logOutputFileName_ClientJob);
                            ImmCoordProcessID2 = MyUtils.StartImmCoord(serverName, 2500, logOutputFileName_ImmCoord2);
                            serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false);
                            break;
                    }
                }

                //*#*#*#* TO DO ... put some kind of check here that if it gets stuck, then kick out maybe compare start time to current time
            }

            // Stop things 
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(serverProcessID);
            MyUtils.KillProcess(ImmCoordProcessID1);
            MyUtils.KillProcess(ImmCoordProcessID2);

            // Now that we got out of the loop do a check of server and check total bytes - will pop exception if values not correct
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server, "DONE", 1, true, testName, true); // Wait for Done

            // Verify client / server have proper bytes
            MyUtils.VerifyBytesRecievedInTwoLogFiles(logOutputFileName_ClientJob, logOutputFileName_Server);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, totalNumBytesReceived.ToString(), 1, false, testName, true); // Total bytes received
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server, totalNumBytesReceived.ToString(), 1, false, testName, true); // Total bytes received

            // Verify integrity of Ambrosia logs by replaying - do NOT check cmp files because MTF can change run to run
            MyUtils.VerifyAmbrosiaLogFile(testName, totalNumBytesReceived, false, false, AMB1.AMB_Version);
        }
        

        //****************************
        // The basic test of job and server just running for very long time
        // This has Persist Logs = Y for both Job and Server
        // Set Server \ Job to exchange random sized 
        //****************************
        [TestMethod]
        public void AMB_MTF_NoKill_PERSIST_Test()
        {
            //NOTE - the Cleanup has this hard coded so if this changes, update Cleanup section too
            string testName = "mtfnokillpersist";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";

            //****************** MTF Settings ***************
            int numRounds = 5; long totalNumBytesReceived = 5368709120;  int maxMminsToWaitToFinish = 5;
            //int numRounds = 25; long totalNumBytesReceived = 26843545600; int maxMminsToWaitToFinish = 30;
            //int numRounds = 50; long totalNumBytesReceived = 53687091200;  int maxMminsToWaitToFinish = 60;
            //int numRounds = 100; long totalNumBytesReceived = 107374182400; int maxMminsToWaitToFinish = 120;
            //int numRounds = 500; long totalNumBytesReceived = 536870912000; int maxMminsToWaitToFinish = 360; 
            //int numRounds = 1000; long totalNumBytesReceived = 1073741824000; int maxMminsToWaitToFinish = 700; 
            //************************

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
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "65536", numRounds.ToString(), logOutputFileName_ClientJob);

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false);

            // Can't really delay until it is done so have to put a delay in here for ever
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, "DONE", maxMminsToWaitToFinish, true, testName, true); // Wait for Done
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server, "DONE", maxMminsToWaitToFinish, true, testName, true); // Wait for Done

            // Now do check to make sure total bytes is correct - log should be done so no need to do more than a minute for max wait
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, totalNumBytesReceived.ToString(), 1, false, testName, true); // Total bytes received
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server, totalNumBytesReceived.ToString(), 1, false, testName, true); // Total bytes received
            //** TO DO:  Need to do health checks while it is waiting

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(serverProcessID);
            MyUtils.KillProcess(ImmCoordProcessID1);
            MyUtils.KillProcess(ImmCoordProcessID2);

            // Verify integrity of Ambrosia logs by replaying but DO NOT check cmp file
            MyUtils.VerifyAmbrosiaLogFile(testName, totalNumBytesReceived, false, false, AMB1.AMB_Version);
        }


        //****************************
        // The basic test of job and server just running for very long time
        // Since running long time, can't have logging so PersistLog = N
        // Set Server \ Job to exchange random sized 
        //****************************
        [TestMethod]
        public void AMB_MTF_NoKill_Test()
        {
            //NOTE - the Cleanup has this hard coded so if this changes, update Cleanup section too
            string testName = "mtfnokill";
            string clientJobName = testName + "clientjob";
            string serverName = testName + "server";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";

            //****************** MTF Settings ***************
            //int numRounds = 5; long totalNumBytesReceived = 5368709120;  int maxMminsToWaitToFinish = 5;
            int numRounds = 25; long totalNumBytesReceived = 26843545600;  int maxMminsToWaitToFinish = 30;
            //int numRounds = 100; long totalNumBytesReceived = 107374182400; int maxMminsToWaitToFinish = 80; // 15 mins
            //int numRounds = 500; long totalNumBytesReceived = 536870912000; int maxMminsToWaitToFinish = 160; // about 1.5 hrs
            //int numRounds = 1000; long totalNumBytesReceived = 1073741824000; int maxMminsToWaitToFinish = 320; // 3 hrs or so
            //************************

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
                AMB_PersistLogs = "N",   //*** Don't log as this would be large file
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
                AMB_PersistLogs = "N",   //*** Don't log as this would be large file
                AMB_NewLogTriggerSize = "1000",
                AMB_ActiveActive = "Y",
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
            int clientJobProcessID = MyUtils.StartPerfClientJob("1001", "1000", clientJobName, serverName, "65536", numRounds.ToString(), logOutputFileName_ClientJob);

            //Server Call
            string logOutputFileName_Server = testName + "_Server.log";
            int serverProcessID = MyUtils.StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server, 1, false);

            // Can't really delay until it is done so have to put a delay in here for ever
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, "DONE", maxMminsToWaitToFinish, false, testName, true); // Wait for Done
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server, "DONE", maxMminsToWaitToFinish, false, testName, true); // Wait for Done

            // Now do check to make sure total bytes is correct - log should be done so no need to do more than a minute for max wait
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_ClientJob, totalNumBytesReceived.ToString(), 1, false, testName, true); // Total bytes received
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Server, totalNumBytesReceived.ToString(), 1, false, testName, true); // Total bytes received
            //** TO DO:  Need to do health checks while it is waiting

            // Stop things so file is freed up and can be opened in verify
            MyUtils.KillProcess(clientJobProcessID);
            MyUtils.KillProcess(serverProcessID);
            MyUtils.KillProcess(ImmCoordProcessID1);
            MyUtils.KillProcess(ImmCoordProcessID2);

        }

        [TestCleanup()]
        public void Cleanup()
        {
            // Kill all ImmCoord.Workers, Job and Server exes
            Utilities MyUtils = new Utilities();
            MyUtils.TestCleanup();
        }

    }
}

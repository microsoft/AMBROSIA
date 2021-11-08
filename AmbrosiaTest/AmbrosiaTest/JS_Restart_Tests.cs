using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Windows.Forms; // need this to handle threading issue on sleeps
using System.Configuration;
using System.IO;


namespace AmbrosiaTest
{
    [TestClass]
    public class JS_Restart_Tests
    {
        //************* Init Code *****************
        // NOTE: Make sure all names be "Azure Safe". No capital letters and no underscore.
        [TestInitialize()]
        public void Initialize()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            // generic Ambrosia init 
            MyUtils.TestInitialize();

            // Set config file back to the way it was 
            JSUtils.JS_RestoreJSConfigFile();
        }
        //************* Init Code *****************


        [TestCleanup()]
        public void Cleanup()
        {
            // Kill all exes associated with tests
            JS_Utilities JSUtils = new JS_Utilities();
            JSUtils.JS_TestCleanup_Restart();
        }

        //** Test that restarts after the run finishes. Test to show that it can start up on log files that "completed"
        [TestMethod]
        public void JS_PTI_RestartAfterFinishes_BiDi_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 5;
            long totalBytes = 640;
            long totalEchoBytes = 640;
            int bytesPerRound = 128;
            int maxMessageSize = 64;
            int batchSizeCutoff = 32;
            int messagesSent = 10;
            bool bidi = true;

            string testName = "jsptirestartafterfinishesbiditest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";
            string logOutputFileNameRestarted_TestApp = testName + "_TestApp_Restarted.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_LBOpt_deleteLogs, "false");   // default is false but ok to specifically state in case default changes

            // Start it once
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp);

            // Wait until it finishes
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed

            // Restart it and make sure it runs ok
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileNameRestarted_TestApp);

            // Verify the data in the restarted output file
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "Checkpoint loaded", 1, false, testName, true);  // since it is restarted, it loads the check point
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true);

            // Verify integrity of Ambrosia logs by replaying
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true);
        }


        //**  End to End for Two Proc that is NOT bidirectional and where the Client is stopped and restarted 
        [TestMethod]
        public void JS_PTI_RestartTwoProcKillClient_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 10;
            long totalBytes = 163840;
            long totalEchoBytes = 163840;
            int bytesPerRound = 16384;
            int maxMessageSize = 64;
            int batchSizeCutoff = 16384;
            int messagesSent = 2560;
            bool bidi = false;

            string testName = "jsptirestartkillclient";
            string clientInstanceName = testName + "client";
            string serverInstanceName = testName + "server";
            string logOutputClientFileName_TestApp = testName + "Client_TestApp.log";
            string logOutputServerFileName_TestApp = testName + "Server_TestApp.log";
            string logOutputClientRestartedFileName_TestApp = testName + "Client_TestApp_Restarted.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName, JSUtils.JSPTI_CombinedInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);

            // Start it once - Launch the client and the server as separate procs 
            //*** NOTE - The first call (Server in this case) starts 4 nodes and sometimes it doesn't give proper process id 
            //*** However, the second call only starts one node, so make sure client is second so then know that PID is correct for killing it
            int serverProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);
            int clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Give it 5 seconds where it tries to connect but doesn't
            Thread.Sleep(5000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            // Kill client
            MyUtils.KillProcess(clientProcessID);

            // Restart the client and make sure it continues
            clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientRestartedFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Verify the data in the restarted output file
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);

            // Verify that echo is NOT part of the output - won't pop assert on fail so check return value
            pass = MyUtils.WaitForProcessToFinish(logOutputClientRestartedFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 0, true, testName, false, false);
            if (pass == true)
            {
                Assert.Fail("<JS_PTI_RestartTwoProcKillClient_Test> Echoed string should NOT have been found in the output but it was.");
            }
            pass = MyUtils.WaitForProcessToFinish(logOutputClientRestartedFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true,false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientRestartedFileName_TestApp, "[IC] Connected!", 1, false, testName, true,false);

            // Verify integrity of Ambrosia logs by replaying 
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true,"", JSUtils.JSPTI_ServerInstanceRole);
        }

        //**  End to End for Two Proc that is bidirectional and where the Client is stopped and restarted 
        [TestMethod]
        public void JS_PTI_RestartTwoProcKillClient_BiDi_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 10;
            long totalBytes = 163840;
            long totalEchoBytes = 163840;
            int bytesPerRound = 16384;
            int maxMessageSize = 64;
            int batchSizeCutoff = 16384;
            int messagesSent = 2560;
            bool bidi = true;

            string testName = "jsptirestartkillclientbidi";
            string clientInstanceName = testName + "client";
            string serverInstanceName = testName + "server";
            string logOutputClientFileName_TestApp = testName + "Client_TestApp.log";
            string logOutputServerFileName_TestApp = testName + "Server_TestApp.log";
            string logOutputClientRestartedFileName_TestApp = testName + "Client_TestApp_Restarted.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName, JSUtils.JSPTI_CombinedInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);

            // Start it once - Launch the client and the server as separate procs 
            //*** NOTE - The first call (server in this case) starts 4 nodes and sometimes it doesn't give proper process id 
            //*** However, the second call only starts one node, so make sure client is second so then know that PID is correct for killing it
            int serverProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);
            int clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Give it 15 seconds where it tries to connect but doesn't
            Thread.Sleep(15000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            // Kill client
            MyUtils.KillProcess(clientProcessID);

            // Restart the client and make sure it continues
            clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientRestartedFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Verify the data in the restarted output file
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);

            // Verify that echo is part of the output
            pass = MyUtils.WaitForProcessToFinish(logOutputClientRestartedFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 1, false, testName, true,false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientRestartedFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientRestartedFileName_TestApp, "[IC] Connected!", 1, false, testName, true, false);

            // Verify integrity of Ambrosia logs by replaying 
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, "", JSUtils.JSPTI_ServerInstanceRole);
        }

        //**  End to End for Two Proc that is NOT bidirectional and where the Server is stopped and restarted 
        [TestMethod]
        public void JS_PTI_RestartTwoProcKillServer_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 3;
            long totalBytes = 3221225472;
            long totalEchoBytes = 3221225472;
            int bytesPerRound = 0;
            int maxMessageSize = 0;
            int batchSizeCutoff = 0;
            bool bidi = false;

            string testName = "jsptirestartkillserver";
            string clientInstanceName = testName + "client";
            string serverInstanceName = testName + "server";
            string logOutputClientFileName_TestApp = testName + "Client_TestApp.log";
            string logOutputServerFileName_TestApp = testName + "Server_TestApp.log";
            string logOutputServerRestartedFileName_TestApp = testName + "Server_TestApp_Restarted.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName, JSUtils.JSPTI_CombinedInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);

            // Start it once - Launch the client and the server as separate procs 
            int clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);
            int serverProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Give it 5 seconds where it tries to connect but doesn't
            Thread.Sleep(5000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            // Kill server
            MyUtils.KillProcess(serverProcessID);

            // Restart the server and make sure it continues
            serverProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerRestartedFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Verify the data in the restarted output file
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartedFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartedFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);

            // Verify that echo is NOT part of the output - won't pop assert on fail so check return value
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 0, true, testName, false, false);
            if (pass == true)
            {
                Assert.Fail("<JS_PTI_RestartTwoProcKillClient_Test> Echoed string should NOT have been found in the output but it was.");
            }
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "All rounds complete", 1, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "[IC] Connected!", 1, false, testName, true, false);

            // Verify integrity of Ambrosia logs by replaying 
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, "", JSUtils.JSPTI_ServerInstanceRole);
        }

        //**  End to End for Two Proc that is bidirectional and where the Server is stopped and restarted 
        [TestMethod]
        public void JS_PTI_RestartTwoProcKillServer_BiDi_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 10;
            long totalBytes = 163840;
            long totalEchoBytes = 163840;
            int bytesPerRound = 16384;
            int maxMessageSize = 64;
            int batchSizeCutoff = 16384;
            int messagesSent = 2560;
            bool bidi = true;

            string testName = "jsptirestartkillserverbidi";
            string clientInstanceName = testName + "client";
            string serverInstanceName = testName + "server";
            string logOutputClientFileName_TestApp = testName + "Client_TestApp.log";
            string logOutputServerFileName_TestApp = testName + "Server_TestApp.log";
            string logOutputServerRestartedFileName_TestApp = testName + "Server_TestApp_Restarted.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName, JSUtils.JSPTI_CombinedInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);

            // Start it once - Launch the client and the server as separate procs 
            //*** NOTE - The first call (client in this case) starts 4 nodes and sometimes it doesn't give proper process id 
            //*** However, the second call only starts one node, so make sure Server is second so then know that PID is correct for killing it
            int clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);
            int serverProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Give it 5 seconds where it tries to connect but doesn't
            Thread.Sleep(5000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            // Kill server
            MyUtils.KillProcess(serverProcessID);

            // Restart the server and make sure it continues
            serverProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerRestartedFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Verify the data in the restarted output file
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartedFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartedFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName,true,false);

            // Verify that echo is part of the output
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 1, false, testName, true,false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "[IC] Connected!", 1, false, testName, true, false);

            // Verify integrity of Ambrosia logs by replaying 
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, "", JSUtils.JSPTI_ServerInstanceRole);
        }

        //**  End to End for Two Proc that is NOT bidirectional and where the Server and Client are stopped and restarted 
        [TestMethod]
        public void JS_PTI_RestartTwoProcKillBoth_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 20;
            long totalBytes = 327680;
            long totalEchoBytes = 327680;
            int bytesPerRound = 16384;
            int maxMessageSize = 64;
            int batchSizeCutoff = 16384;
            int messagesSent = 5120;
            bool bidi = false;

            string testName = "jsptirestartkillboth";
            string clientInstanceName = testName + "client";
            string serverInstanceName = testName + "server";
            string logOutputClientFileName_TestApp = testName + "Client_TestApp.log";
            string logOutputServerFileName_TestApp = testName + "Server_TestApp.log";
            string logOutputClientRestartedFileName_TestApp = testName + "Client_TestApp_Restarted.log";
            string logOutputServerRestartedFileName_TestApp = testName + "Server_TestApp_Restarted.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName, JSUtils.JSPTI_CombinedInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);

            // Start it once - Launch the client and the server as separate procs 
            int serverProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);
            int clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Give it 5 seconds where it tries to connect but doesn't
            Thread.Sleep(5000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            // Kill server and client
            MyUtils.StopAllAmbrosiaProcesses();

            // Restart the server and client and make sure it continues
            serverProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerRestartedFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);
            clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientRestartedFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Verify the data in the restarted output file
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartedFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartedFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);

            // Verify that echo is NOT part of the output - won't pop assert on fail so check return value
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 0, true, testName, false, false);
            if (pass == true)
            {
                Assert.Fail("<JS_PTI_RestartTwoProcKillClient_Test> Echoed string should NOT have been found in the output but it was.");
            }
            pass = MyUtils.WaitForProcessToFinish(logOutputClientRestartedFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientRestartedFileName_TestApp, "[IC] Connected!", 1, false, testName, true, false);

            // Verify integrity of Ambrosia logs by replaying 
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, "", JSUtils.JSPTI_ServerInstanceRole);
        }

        //**  End to End for Two Proc that is bidirectional and where the Server and Client are stopped and restarted 
        [TestMethod]
        public void JS_PTI_RestartTwoProcKillBoth_BiDi_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 3;
            long totalBytes = 3221225472;
            long totalEchoBytes = 3221225472;
            int bytesPerRound = 0;
            int maxMessageSize = 0;
            int batchSizeCutoff = 0; bool bidi = true;

            string testName = "jsptirestartkillbothbidi";
            string clientInstanceName = testName + "client";
            string serverInstanceName = testName + "server";
            string logOutputClientFileName_TestApp = testName + "Client_TestApp.log";
            string logOutputServerFileName_TestApp = testName + "Server_TestApp.log";
            string logOutputClientRestartedFileName_TestApp = testName + "Client_TestApp_Restarted.log";
            string logOutputServerRestartedFileName_TestApp = testName + "Server_TestApp_Restarted.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName, JSUtils.JSPTI_CombinedInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);

            // Start it once - Launch the client and the server as separate procs 
            int clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);
            int serverProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Give it 5 seconds where it tries to connect but doesn't
            Thread.Sleep(5000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            // Kill server and client
            MyUtils.StopAllAmbrosiaProcesses();

            // Restart the server and client and make sure it continues
            serverProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerRestartedFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);
            clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientRestartedFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Verify the data in the restarted output file
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartedFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartedFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true, false);

            // Verify that echo is part of the output
            pass = MyUtils.WaitForProcessToFinish(logOutputClientRestartedFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 1, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientRestartedFileName_TestApp, "All rounds complete", 1, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientRestartedFileName_TestApp, "[IC] Connected!", 1, false, testName, true, false);

            // Verify integrity of Ambrosia logs by replaying 
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, "", JSUtils.JSPTI_ServerInstanceRole);
        }



    }
}

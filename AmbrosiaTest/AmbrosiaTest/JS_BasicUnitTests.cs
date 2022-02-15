using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AmbrosiaTest
{
    [TestClass]
    public class JS_BasicUnitTests
    {

        public string uniqueTestNameID = "000";

        //************* Init Code *****************
        // NOTE: Make sure all names be "Azure Safe". No capital letters and no underscore.
        [TestInitialize()]
        public void Initialize()
        {
            Random rand = new Random();

            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            // generic Ambrosia init 
            MyUtils.TestInitialize(true);

            // Set config file back to the way it was 
            JSUtils.JS_RestoreJSConfigFile();

            // create three digit number that will be used to make the test name unique
            uniqueTestNameID = rand.Next(100, 999).ToString();

        }
        //************* Init Code *****************


        [TestCleanup()]
        public void Cleanup()
        {
            // Kill all exes associated with tests
            JS_Utilities JSUtils = new JS_Utilities();
            JSUtils.JS_TestCleanup_Basic();
        }

        //** Basic End to End that is bidirectional where ehoing the 'doWork' method call back to the client
        [TestMethod]
        [TestCategory("JSADO")]
        public void JS_PTI_BasicEndToEnd_BiDi_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 2;
            long totalBytes = 2147483648;
            long totalEchoBytes = 2147483648;
            int bytesPerRound = 0;
            int maxMessageSize = 0;
            int batchSizeCutoff = 0;
            int messagesSent = 49152;
            bool bidi = true;

            string logTriggerSize = "1024";  // just set a test to have 1024 which is the default of C#
            string originaltestName = "jsptibidiendtoendtest"; 
            string testName = originaltestName + uniqueTestNameID;  // this is the version to make it unqiue so other CI tests don't affect it
            string logOutputFileName_TestApp = testName + "_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize);  
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp);

            // Verify the data in the output file - too many changing rows in output to do a cmp file so verify some of the key lines
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: "+ totalBytes.ToString(), 10, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of bytes ("+ totalBytes.ToString() + ") have been received", 1, false, testName, true); 
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of echoed bytes ("+ totalEchoBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "All rounds complete ("+ messagesSent.ToString()+ " messages sent)", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "[IC] Connected!", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "round #"+ numRounds.ToString(), 1, false, testName, true);
            
            // Verify integrity of Ambrosia logs by replaying
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds,totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize,batchSizeCutoff, bidi, true);
        }


        //** Basic End to End that is NOT bidirectional
        //** A zero value is flag to take default value
        [TestMethod]
        public void JS_PTI_BasicEndToEnd_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 4;
            long totalBytes = 4294967296;
            long totalEchoBytes = 4294967296;
            int bytesPerRound = 0;
            int maxMessageSize = 0;
            int batchSizeCutoff = 0; 
            int messagesSent = 245760;
            bool bidi = false;

            string logTriggerSize = "256";  // set a test to have 256 which is different from 1024 which is set in the BasieEndToEndBiDi

            string originaltestName = "jsptiendtoendtest"; 
            string testName = originaltestName + uniqueTestNameID;  // this is the version to make it unqiue so other CI tests don't affect it
            string logOutputFileName_TestApp = testName + "_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize);  
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp);

            // Verify the data in the output file - too many changing rows in output to do a cmp file so verify some of the key lines
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);

            // Verify that echo is NOT part of the output - won't pop assert on fail so check return value
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 0, true, testName,false,false);
            if (pass == true)
            {
                Assert.Fail("<JS_PTI_BasicEndToEnd_Test> Echoed string should NOT have been found in the output but it was.");
            }
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "[IC] Connected!", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true);

            // Verify integrity of Ambrosia logs by replaying
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true);
        }

        //** Basic End to End that is NOT bidirectional and that is stopped and restarted 
        [TestMethod]
        public void JS_PTI_BasicRestartEndToEnd_Test()
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

            string originaltestName = "jsptirestartendtoendtest"; 
            string testName = originaltestName + uniqueTestNameID;  // this is the version to make it unqiue so other CI tests don't affect it

            string logOutputFileName_TestApp = testName + "_TestApp.log";
            string logOutputFileNameRestarted_TestApp = testName + "_TestApp_Restarted.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_LBOpt_deleteLogs, "false");   // default is false but ok to specifically state in case default changes

            // Start it once
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp);

            // Give it 2 seconds where it tries to connect but doesn't
            MyUtils.TestDelay(2000);

            // Kill it 
            MyUtils.StopAllAmbrosiaProcesses();

            // Restart it and make sure it continues
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileNameRestarted_TestApp);

            // Verify the data in the restarted output file
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);

            // Verify that echo is NOT part of the output - won't pop assert on fail so check return value
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 0, true, testName, false, false);
            if (pass == true)
            {
                Assert.Fail("<JS_PTI_BasicEndToEnd_Test> Echoed string should NOT have been found in the output but it was.");
            }
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "[IC] Connected!", 1, false, testName, true);

            // Verify integrity of Ambrosia logs by replaying 
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true);
        }


        //** Basic End to End that is bidirectional and that is stopped and restarted 
        [TestMethod]
        [TestCategory("JSADO")]
        public void JS_PTI_BasicRestartEndToEnd_BiDi_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 4;
            long totalBytes = 4294967296;  
            long totalEchoBytes = 4294967296;
            int bytesPerRound = 0;
            int maxMessageSize = 0;
            int batchSizeCutoff = 0;
            bool bidi = true;

            string originaltestName = "jsptirestartendtoendbiditest"; 
            string testName = originaltestName + uniqueTestNameID;  // this is the version to make it unqiue so other CI tests don't affect it
            string logOutputFileName_TestApp = testName + "_TestApp.log";
            string logOutputFileNameRestarted_TestApp = testName + "_TestApp_Restarted.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_LBOpt_deleteLogs, "false");   // default is false but ok to specifically state in case default changes

            // Start it once
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp);

            // Give it 10 seconds to do something before killing it
            MyUtils.TestDelay(10000);

            // Kill it 
            MyUtils.StopAllAmbrosiaProcesses();

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_LBOpt_deleteLogs, "false");   // default is false but ok to specifically state in case default changes
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "false");   // get auto changed to false but ok to specifically state in case default changes

            // Restart it and make sure it continues
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileNameRestarted_TestApp);

            // Verify the data in the restarted output file
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "Bytes received: " + totalBytes.ToString(), 15, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "All rounds complete", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "[IC] Connected!", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true);

            // Verify integrity of Ambrosia logs by replaying
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true);
        }

        //** Basic End to End that is NOT bidirectional where Client and Server are in separate Procs
        [TestMethod]
        public void JS_PTI_BasicTwoProc_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            string originaltestName = "jsptitwoproctest"; 
            string testName = originaltestName + uniqueTestNameID;  // this is the version to make it unqiue so other CI tests don't affect it

            int numRounds = 3;
            long totalBytes = 3221225472;
            long totalEchoBytes = 3221225472;
            int bytesPerRound = 0;
            int maxMessageSize = 0;
            int batchSizeCutoff = 0;
            int messagesSent = 114688;
            bool bidi = false;
            string clientInstanceName = testName+"client";
            string serverInstanceName = testName+"server";

            string logOutputClientFileName_TestApp = testName + "Client_TestApp.log";
            string logOutputServerFileName_TestApp = testName + "Server_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName, JSUtils.JSPTI_CombinedInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);

            // Launch the client and the server as separate procs 
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Verify the data in the output file of the server
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 10, false, testName, true); 
            pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "[IC] Connected!", 1, false, testName, true);

            // Verify the data in the output file of the CLIENT - since not bidi, no echoed bytes
            // Verify that echo is NOT part of the output for client
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 5,false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 0, true, testName, false, false);
            if (pass == true)
            {
                Assert.Fail("<JS_PTI_BasicEndToEnd_Test> Echoed string should NOT have been found in the CLIENT output but it was.");
            }
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "[IC] Connected!", 1, false, testName,true,false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true, false);

            // Verify integrity of Ambrosia logs by replaying server side of things (not bidi so only do Server)
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, "", JSUtils.JSPTI_ServerInstanceRole);

        }

        //** Same as Basic End to End that is NOT bidirectional where Client and Server are in separate Procs but tests the Post Method
        [TestMethod]
        [TestCategory("JSADO")]
        public void JS_PTI_BasicTwoProc_BiDi_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            string originaltestName = "jsptitwoproctestbidi"; 
            string testName = originaltestName + uniqueTestNameID;  // this is the version to make it unqiue so other CI tests don't affect it

            int numRounds = 3;
            long totalBytes = 3221225472;
            long totalEchoBytes = 3221225472;
            int bytesPerRound = 0;
            int maxMessageSize = 0;
            int batchSizeCutoff = 0;
            int messagesSent = 114688;
            bool bidi = true;

            string clientInstanceName = testName + "client";
            string serverInstanceName = testName + "server";

            string logOutputClientFileName_TestApp = testName + "Client_TestApp.log";
            string logOutputServerFileName_TestApp = testName + "Server_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName, JSUtils.JSPTI_CombinedInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);

            // Launch the client and the server as separate procs 
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Verify the data in the output file of the server
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 15, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "[IC] Connected!", 1, false, testName, true);

            // Verify the data in the output file of the CLIENT 
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 5, true, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 5, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "[IC] Connected!", 1, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true, false);

            // Verify integrity of Ambrosia logs by replaying server and client side of things (do both since bidi)
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true,"",JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, "", JSUtils.JSPTI_ServerInstanceRole);
        }


        //** Same as Basic End to End (non bidi) but with Including Post Method
        [TestMethod]
        public void JS_PTI_BasicEndToEnd_PostMeth_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 4;
            long totalBytes = 4294967296;
            long totalEchoBytes = 4294967296;
            int bytesPerRound = 0;
            int maxMessageSize = 0;
            int batchSizeCutoff = 0;
            int messagesSent = 245760;
            bool bidi = false;

            string logTriggerSize = "256";  

            string originaltestName = "jsptiendtoendtestpostmeth"; 
            string testName = originaltestName + uniqueTestNameID;  // this is the version to make it unqiue so other CI tests don't affect it
            string logOutputFileName_TestApp = testName + "_TestApp.log";
            string logOutputFileName_TTDVerify = testName + "__VerifyTTD_1.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize);
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp,0,false,"","",true);

            // Verify the data in the output file - too many changing rows in output to do a cmp file so verify some of the key lines
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);

            // Verify that echo is NOT part of the output - won't pop assert on fail so check return value
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true);

            // Verify the Post Method messages
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "outgoing messages and 768 in-flight post methods...", 1, false, testName, true);  // The "Waiting for xxx number changes from run to run
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Outgoing message queue is empty; there are no in-flight post methods", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The result handler for the 'incrementValue' post method was called the expected number of times (245760)", 1, false, testName, true);

            // Verify integrity of Ambrosia logs by replaying
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TTDVerify, "outgoing messages and 768 in-flight post methods...", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TTDVerify, "Outgoing message queue is empty; there are no in-flight post methods", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TTDVerify, "SUCCESS: The result handler for the 'incrementValue' post method was called the expected number of times (245760)", 1, false, testName, true);

        }


        //** Sames as basic End to End that is NOT bidirectional and that is stopped and restarted but with Post Method switch set
        [TestMethod]
        public void JS_PTI_BasicRestartEndToEnd_PostMeth_Test()
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

            string originaltestName = "jsptirestartendtoendpostmeth"; 
            string testName = originaltestName + uniqueTestNameID;  // this is the version to make it unqiue so other CI tests don't affect it

            string logOutputFileName_TestApp = testName + "_TestApp.log";
            string logOutputFileNameRestarted_TestApp = testName + "_TestApp_Restarted.log";
            string logOutputFileName_TTDVerify = testName + "__VerifyTTD_1.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_LBOpt_deleteLogs, "false");   // default is false but ok to specifically state in case default changes

            // Start it once
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp,0,false,"","",true);

            // Give it 20 seconds where it tries to connect but doesn't
            MyUtils.TestDelay(20000);

            // Kill it 
            MyUtils.StopAllAmbrosiaProcesses();

            // Restart it and make sure it continues
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileNameRestarted_TestApp,0,false,"","",true);

            // Verify the data in the restarted output file
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "[IC] Connected!", 1, false, testName, true);

            // Verify the Post Method messages
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "Restarted result timeouts for", 1, false, testName, true);  
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "outgoing messages and 128 in-flight post methods...", 1, false, testName, true);  
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "Outgoing message queue is empty; there are no in-flight post methods", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "SUCCESS: The result handler for the 'incrementValue' post method was called the expected number of times (114688)", 1, false, testName, true);

            // Verify integrity of Ambrosia logs by replaying 
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TTDVerify, "outgoing messages and 128 in-flight post methods...", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TTDVerify, "Outgoing message queue is empty; there are no in-flight post methods", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TTDVerify, "SUCCESS: The result handler for the 'incrementValue' post method was called the expected number of times (114688)", 1, false, testName, true);

        }


        //** Same as basic End to End that is NOT bidirectional where Client and Server are in separate Procs and it is testing Post Method
        [TestMethod]
        public void JS_PTI_BasicTwoProc_PostMeth_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            string originaltestName = "jsptitwoprocpostmeth"; 
            string testName = originaltestName + uniqueTestNameID;  // this is the version to make it unqiue so other CI tests don't affect it

            int numRounds = 4;
            long totalBytes = 4294967296;
            long totalEchoBytes = 4294967296;
            int bytesPerRound = 0;
            int maxMessageSize = 0;
            int batchSizeCutoff = 0;
            int messagesSent = 245760;
            bool bidi = false;
            string clientInstanceName = testName + "client";
            string serverInstanceName = testName + "server";

            string logOutputClientFileName_TestApp = testName + "Client_TestApp.log";
            string logOutputServerFileName_TestApp = testName + "Server_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName, JSUtils.JSPTI_CombinedInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);

            // Launch the client and the server as separate procs 
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole,"",true);
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName,true);

            // Verify the data in the output file of the server
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 10, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "[IC] Connected!", 1, false, testName, true);

            // Verify the data in the output file of the CLIENT - since not bidi, no echoed bytes
            // Verify that echo is NOT part of the output for client
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 5, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "[IC] Connected!", 1, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true, false);

            // Verify the Post Method messages
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "SUCCESS: The result handler for the 'incrementValue' post method was called the expected number of times (245760)", 1, false, testName, true, false);

            // Verify integrity of Ambrosia logs by replaying server side of things (not bidi so only do Server which will not show Post Meth)
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, "", JSUtils.JSPTI_ServerInstanceRole);
        }


        //** Basic End to End that is NOT bidirectional where Client and Server are in separate Procs
        [TestMethod]
        public void JS_PTI_BasicRestartTwoProc_PostMeth_Test()
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

            string originaltestName = "jsptirestartpostmeth"; 
            string testName = originaltestName + uniqueTestNameID;  // this is the version to make it unqiue so other CI tests don't affect it

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
            int serverProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole,"",true);
            int clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName,true);

            // Give it 20 seconds where it tries to connect but doesn't
            MyUtils.TestDelay(20000);

            // Kill client
            MyUtils.KillProcess(clientProcessID);

            // Restart the client and make sure it continues
            clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientRestartedFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Verify the data in the restarted output file
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);

            // Verify that echo is NOT part of the output - won't pop assert on fail so check return value
            pass = MyUtils.WaitForProcessToFinish(logOutputClientRestartedFileName_TestApp, "All rounds complete", 1, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientRestartedFileName_TestApp, "[IC] Connected!", 1, false, testName, true, false);

            // Verify the Post Method messages
            pass = MyUtils.WaitForProcessToFinish(logOutputClientRestartedFileName_TestApp, "Restarted result timeouts for", 1, false, testName, true,false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientRestartedFileName_TestApp, "SUCCESS: The result handler for the 'incrementValue' post method was called the expected number of times (114688)", 1, false, testName, true,false);

            // Verify integrity of Ambrosia logs by replaying 
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, "", JSUtils.JSPTI_ServerInstanceRole);
        }


        //** Runs the built in unit tests 
        [TestMethod]
        public void JS_Node_UnitTests()
        {

            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            string testName = "jsnodeunittest";
            string finishedString = "UNIT TESTS COMPLETE";
            string successString = "SUMMARY: 114 passed (100%), 0 failed (0%)";
            string logOutputFileName_TestApp = testName + "_TestApp.log";

            // Launched all the unit tests for JS Node (npm run unittests)
            int JSTestAppID = JSUtils.StartJSNodeUnitTests(logOutputFileName_TestApp);

            // Wait until summary at the end and if not there, then know not finished
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, finishedString, 2, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, successString, 1, false, testName, true, false);

        }

    }
}

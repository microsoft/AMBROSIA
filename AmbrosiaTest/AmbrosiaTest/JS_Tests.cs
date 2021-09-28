using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Windows.Forms; // need this to handle threading issue on sleeps
using System.Configuration;
using System.IO;


namespace AmbrosiaTest
{
    [TestClass]
    public class JS_Tests
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
            JSUtils.JS_TestCleanup();
        }


        //**
        //** Setting the largest maxMessageSize and batchSizeCutoff since maxMessageSize is the size of payload and batchSizeCutoff is the number of message - this is not Fixed message size so as it is descending
        //** C# LB uses 64 MB for GiantMessageTest
        //**
        [TestMethod]
        public void JS_PTI_GiantMessage_BiDi_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 3;
            long totalBytes = 805306368;
            long totalEchoBytes = 805306368;
            int bytesPerRound = 268435456; // 256 MB
            int maxMessageSize = 67108864;  // 64 MBs
            int batchSizeCutoff = 0;
            int messagesSent = 28;
            bool bidi = true;

            string testName = "jsptigiantmessagebiditest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";

            // Set msgQueueSize to max size so can handle large message
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_LBOpt_msgQueueSize, "350");  // 350 MB is max - 25% of GC heap size of 64 bit OS

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp);

            // Verify the data in the output file - too many changing rows in output to do a cmp file so verify some of the key lines
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 15, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 2, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 5, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "[IC] Connected!", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true);

            // Verify integrity of Ambrosia logs by replaying
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, true);
        }


        //** Same as Giant Message BiDi but just not bidirecitonal
        [TestMethod]
        public void JS_PTI_GiantMessage_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 2;
            long totalBytes = 536870912;
            long totalEchoBytes = 536870912;  // not needed since not bi directional
            int bytesPerRound = 268435456; // 256 MB
            int maxMessageSize = 67108864;  // 64 MBs
            int batchSizeCutoff = 0;
            int messagesSent = 12;
            bool bidi = false;

            string testName = "jsptigiantmessagetest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_LBOpt_msgQueueSize, "350");  // 350 MB is max - 25% of GC heap size of 64 bit OS

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp);

            // Verify the data in the output file - too many changing rows in output to do a cmp file so verify some of the key lines
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 15, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 2, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "[IC] Connected!", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true);

            // Verify integrity of Ambrosia logs by replaying
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, true);
        }

        //** Setting the largest memoryUsed to simulate large checkpoints 
        [TestMethod]
        public void JS_PTI_GiantCheckPoint_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 2;
            long totalBytes = 134217728;
            long totalEchoBytes = 134217728; 
            int bytesPerRound = 67108864;  //64 MB
            int maxMessageSize = 33554432; 
            int batchSizeCutoff = 0;
            int messagesSent = 6;
            bool bidi = false;
            int memoryUsed = 104857600; // padding"(in bytes) used to simulate large checkpoints by being included in app state-- 1GB (1073741824) is what C# PTI uses. Will get OOM issue if do much more than 100 MB so just use 100 MB.  See bug #170 for details.  -  

            int checkPointSize = 209716012;

            string testName = "jsptigiantcheckpointtest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp, memoryUsed);

            // Verify the data in the output file - too many changing rows in output to do a cmp file so verify some of the key lines
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "[IC] Connected!", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "[IC] Reading a checkpoint "+ checkPointSize.ToString() + " bytes", 1, false, testName, true);

            // Verify integrity of Ambrosia logs by replaying
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, true);
        }


        //** Similar to non bidi test but this is small byte settings and bidi
        [TestMethod]
        public void JS_PTI_GiantCheckPoint_BiDi_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 5;
            long totalBytes = 640;
            long totalEchoBytes = 640;
            int bytesPerRound = 128;
            int maxMessageSize = 32;
            int batchSizeCutoff = 32;
            int messagesSent = 36;

            int memoryUsed = 104857600;  // padding"(in bytes) used to simulate large checkpoints by being included in app state-- 1GB (1073741824) is what C# PTI uses. Will get OOM issue if do much more than 100 MB so just use 100 MB.  See bug #170 for details.  -  

            int checkPointSize = 209715990;
            bool bidi = true;

            string testName = "jsptigiantcheckpointbiditest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp, memoryUsed);

            // Verify the data in the output file - too many changing rows in output to do a cmp file so verify some of the key lines
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true);

            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "[IC] Connected!", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "[IC] Reading a checkpoint "+ checkPointSize.ToString() + " bytes", 1, false, testName, true);


            // Verify integrity of Ambrosia logs by replaying
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, true);
        }


        //** Simple bidi test of Fixed message length - using small data sizes
        [TestMethod]
        public void JS_PTI_FixedMsgSize_BiDi_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 7;
            long totalBytes = 896;
            long totalEchoBytes = 896;
            int bytesPerRound = 128;
            int maxMessageSize = 32;
            int batchSizeCutoff = 32;
            int messagesSent = 28;
            bool bidi = true;
            bool fixedMsgSize = true;

            string testName = "jsptibidifmstest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp, 0,fixedMsgSize);

            // Verify the data in the output file - too many changing rows in output to do a cmp file so verify some of the key lines
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "[IC] Connected!", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Starting new round (with 7 rounds left) of 4 messages of 32 bytes each", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Starting new round (with 6 rounds left) of 4 messages of 32 bytes each", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Starting new round (with 5 rounds left) of 4 messages of 32 bytes each", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Starting new round (with 4 rounds left) of 4 messages of 32 bytes each", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Starting new round (with 3 rounds left) of 4 messages of 32 bytes each", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Starting new round (with 2 rounds left) of 4 messages of 32 bytes each", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Starting new round (with 1 round left) of 4 messages of 32 bytes each", 1, false, testName, true);

            // Verify integrity of Ambrosia logs by replaying
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, true);
        }

        //** Simple test (not bidi) of Fixed message length - using larger data sizes
        [TestMethod]
        public void JS_PTI_FixedMsgSize_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 3;
            long totalBytes = 201326592;
            long totalEchoBytes = 201326592;
            int bytesPerRound = 67108864;  
            int maxMessageSize = 4194304;
            int batchSizeCutoff = 0;
            int messagesSent = 48;
            bool bidi = false;
            bool fixedMsgSize = true;

            string testName = "jsptifmstest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp, 0, fixedMsgSize);

            // Verify the data in the output file - too many changing rows in output to do a cmp file so verify some of the key lines
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "[IC] Connected!", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Starting new round (with 3 rounds left) of 16 messages of 4194304 bytes each", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Starting new round (with 2 rounds left) of 16 messages of 4194304 bytes each", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Starting new round (with 1 round left) of 16 messages of 4194304 bytes each", 1, false, testName, true);

            // Verify integrity of Ambrosia logs by replaying
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, true);
        }



        //** Test that if you call AutoRegister with "TrueAndExit" parameter it will just register it and not go any further
        [TestMethod]
        public void JS_PTI_AutoRegisterAndExit_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 5;
            long totalBytes = 640;
            long totalEchoBytes = 640;
            int bytesPerRound = 128;
            int maxMessageSize = 32;
            int batchSizeCutoff = 32;
            bool bidi = false;

            string testName = "jsptiautoregexittest";
            string logOutputFileName_Register = testName + "_Register.log";
            string logOutputFileName_StartTesttApp = testName + "_Start.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "TrueAndExit");   // The actual test
            
            // Launch the app where it just registers it
            string workingDir = ConfigurationManager.AppSettings["AmbrosiaJSTestDirectory"] + "\\PTI\\App";
            string fileNameExe = "node.exe";
            string argString = "out\\main.js -ir=Combined -n="+ numRounds.ToString() + "-eeb="+ totalBytes.ToString() + " -nhc -efb="+ totalBytes.ToString() + " -mms="+ maxMessageSize.ToString() + " -bpr="+ bytesPerRound.ToString() + " -bsc="+ batchSizeCutoff.ToString();

            int processID = MyUtils.LaunchProcess(workingDir, fileNameExe, argString, false, logOutputFileName_Register);
            if (processID <= 0)
            {
                MyUtils.FailureSupport("");
                Assert.Fail("<JS_PTI_AutoRegisterAndExit_Test> JS TestApp was not started.  ProcessID <=0 ");
            }

            // Give it a few seconds to start
            Thread.Sleep(3000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            // Just make sure registered part ran and configured it
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Register, "Ambrosia configuration loaded from 'ambrosiaConfig.json'", 1, false, testName, true, false);

            // Starting it a second time should just start right up fine - if it doesn't then we know it wasn't registered from the first call
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_StartTesttApp);

            // Wait until it finishes
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_StartTesttApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_StartTesttApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);

            // Also make sure the first run only registerd and did NOT actually run through IC calls or anything else
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Register, "SUCCESS: ", 0, false, testName, false, false);
            if (pass == true)
            {
                Assert.Fail("<JS_PTI_AutoRegisterAndExit_Test> Found a SUCCESS string, but should not have.");
            }
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_Register, "[IC]: ", 0, false, testName, false, false);
            if (pass == true)
            {
                Assert.Fail("<JS_PTI_AutoRegisterAndExit_Test> Found a [IC] string, but should not have.");
            }
        }


        //** Simple test to verify DeleteLog = FALSE for files works
        [TestMethod]
        public void JS_PTI_DeleteFileLogFalse_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            string logDirectory = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"];

            int numRounds = 4;
            long totalBytes = 512;
            long totalEchoBytes = 512;
            int bytesPerRound = 128;
            int maxMessageSize = 32;
            int batchSizeCutoff = 32;
            bool bidi = false;

            string testName = "jsptideletefilelogfalsetest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);

            // creates the log files
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp);

            // Wait until finish
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true);

            //Get date of the serverlog1 and serverchkpt1
            DateTime serveroriglogDate = File.GetCreationTime(logDirectory + "\\jsptideletefilelogfalsetest_0\\serverlog1");
            DateTime serverorigcheckptDate = File.GetCreationTime(logDirectory + "\\jsptideletefilelogfalsetest_0\\serverchkpt1");

            // set to NOT delete the log
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_LBOpt_deleteLogs, "false");

            // call it again 
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp);

            // Wait until finish
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true);

            DateTime servernewlogDate = File.GetCreationTime(logDirectory + "\\jsptideletefilelogfalsetest_0\\serverlog1");
            DateTime servernewcheckptDate = File.GetCreationTime(logDirectory + "\\jsptideletefilelogfalsetest_0\\serverchkpt1");

            // Verify that new log files were NOT created
            if (serveroriglogDate != servernewlogDate)
                Assert.Fail("Original Log Date and New Log Date were NOT the same which means they were deleted when they shouldn't have been.");

            if (serverorigcheckptDate != servernewcheckptDate)
                Assert.Fail("Original CheckPoint Date and New CheckPoint Date were NOT the same which means they were deleted when they shouldn't have been.");
        }


        //** Simple test to verify DeleteLog = true for files works
        [TestMethod]
        public void JS_PTI_DeleteFileLogTrue_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            string logDirectory = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"];

            int numRounds = 4;
            long totalBytes = 512;
            long totalEchoBytes = 512;
            int bytesPerRound = 128;
            int maxMessageSize = 32;
            int batchSizeCutoff = 32;
            bool bidi = false;

            string testName = "jsptideletefilelogtruetest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);

            // creates the log files
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp);

            // Wait until finish
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true);

            //Get date of the serverlog1 and serverchkpt1
            DateTime serveroriglogDate = File.GetCreationTime(logDirectory + "\\jsptideletefilelogtruetest_0\\serverlog1");
            DateTime serverorigcheckptDate = File.GetCreationTime(logDirectory + "\\jsptideletefilelogtruetest_0\\serverchkpt1");

            // set to delete the log
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_LBOpt_deleteLogs, "true");

            // call it again 
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp);

            // Wait until finish
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true);

            DateTime servernewlogDate = File.GetCreationTime(logDirectory + "\\jsptideletefilelogtruetest_0\\serverlog1");
            DateTime servernewcheckptDate = File.GetCreationTime(logDirectory + "\\jsptideletefilelogtruetest_0\\serverchkpt1");

            // Verify that new log files were created
            if (serveroriglogDate == servernewlogDate)
                Assert.Fail("Original Log Date and New Log Date were the same which means it was NOT deleted.");

            if (serverorigcheckptDate == servernewcheckptDate)
                Assert.Fail("Original CheckPoint Date and New CheckPoint Date were the same which means it was NOT deleted.");
        }


        //**  Similar to restart after both killed, but this is case where the process is NOT killed and the process is just restarted. 
        //** The second process takes over and the original processes die. It is the way would migrate the client version.
        [TestMethod]
        public void JS_PTI_MigrateClientTwoProc_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 15;
            long totalBytes = 122880;
            long totalEchoBytes = 122880;
            int bytesPerRound = 8192;
            int maxMessageSize = 32;
            int batchSizeCutoff = 8192;
            int messagesSent = 7424;
            bool bidi = false;

            string testName = "jsptimigrateclienttwoproctest";
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
            int serverProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole, "", clientInstanceName);
            int clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Give it 5 seconds where it tries to connect 
            Thread.Sleep(5000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            // DO NOT Kill both Client and Server 
            // This is main part of test - get it to have Client and Server take over and run and orig Client and Server are stopped
            // MyUtils.KillProcess(serverProcessID);
            // MyUtils.KillProcess(clientProcessID);

            // Change the ports in the config files before restarting a new server and client
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "3510", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "3020", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "3021", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "4500", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "4020", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "4021", JSUtils.JSPTI_ServerInstanceRole);

            // Restart the server and client and make sure it continues
            clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientRestartedFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Give it a sec or so to get client started up again
            Thread.Sleep(2000);
            Application.DoEvents();  

            serverProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerRestartedFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole, "", clientInstanceName);

            // Verify the data in the restarted output file
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartedFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartedFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);

            // Verify that echo is NOT part of the output - won't pop assert on fail so check return value
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 0, true, testName, false, false);
            if (pass == true)
            {
                Assert.Fail("<JS_PTI_MigrateClientTwoProc_Test> Echoed string should NOT have been found in the output but it was.");
            }
            pass = MyUtils.WaitForProcessToFinish(logOutputClientRestartedFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientRestartedFileName_TestApp, "[IC] Connected!", 1, false, testName, true, false);

            // Verify integrity of Ambrosia logs by replaying 
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, true, "", JSUtils.JSPTI_ServerInstanceRole, "", clientInstanceName);
        }


        //**  Similar to restart after both killed, but this is case where the process is NOT killed and the process is just restarted. 
        //** The second process takes over and the original processes die. It is the way would migrate the client version.
        //** This is for the BI DIRECTIONAL aspect of two proc
        [TestMethod]
        public void JS_PTI_MigrateClientTwoProc_BiDi_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 5;
            long totalBytes = 40960;
            long totalEchoBytes = 40960;
            int bytesPerRound = 8192;
            int maxMessageSize = 32;
            int batchSizeCutoff = 8192;
            int messagesSent = 2304; // 7424;
            bool bidi = true;

            string testName = "jsptimigrateclienttwoprocbiditest";
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
            int serverProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole, "", clientInstanceName);
            int clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Give it 5 seconds where it tries to connect 
            Thread.Sleep(5000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            // DO NOT Kill both Client and Server 
            // This is main part of test - get it to have Client and Server take over and run and orig Client and Server are stopped
            // MyUtils.KillProcess(serverProcessID);
            // MyUtils.KillProcess(clientProcessID);

            // Change the ports in the config files before restarting a new server and client
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "3510", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "3020", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "3021", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "4500", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "4020", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "4021", JSUtils.JSPTI_ServerInstanceRole);

            // Restart the server and client and make sure it continues
            clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientRestartedFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Give it 5 seconds where it tries to connect 
            Thread.Sleep(5000);
            Application.DoEvents();

            serverProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerRestartedFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole, "", clientInstanceName);

            // Verify the data in the restarted output file
            // Verify the data in the restarted output file
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartedFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartedFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true, false);

            // Verify that echo is part of the output
            pass = MyUtils.WaitForProcessToFinish(logOutputClientRestartedFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 1, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientRestartedFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientRestartedFileName_TestApp, "[IC] Connected!", 1, false, testName, true, false);

            // Verify integrity of Ambrosia logs by replaying 
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, true, "", JSUtils.JSPTI_ServerInstanceRole, "", clientInstanceName);
        }

        //**  Similar to restart after both killed, but this is case where the process is NOT killed and the process is just restarted. 
        //** The second process takes over and the original processes die. It is the way would migrate the client version.
        [TestMethod]
        public void JS_PTI_MigrateClient_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 6;
            long totalBytes = 6442450944;
            long totalEchoBytes = 6442450944;
            int bytesPerRound = 0;
            int maxMessageSize = 0;
            int batchSizeCutoff = 0;
            int messagesSent = 1032192;
            bool bidi = false;

            string testName = "jsptimigrateclienttest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";
            string logOutputFileNameRestarted_TestApp = testName + "_TestApp_Restarted.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_LBOpt_deleteLogs, "false");   // default is false but ok to specifically state in case default changes

            // Start it once
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp);

            // wait until check point saved then restart it 
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Checkpoint saved", 5, false, testName, true); // number of bytes processed

            // DO NOT Kill both app 
            // This is main part of test - get it to have Client and Server take over and run and orig Client and Server are stopped
            // MyUtils.KillProcess("node");

            // Change the ports in the config files before restarting so doesn't conflict port numbers
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "3520");
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "3020");
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "3021");

            // Restart it and make sure it continues
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileNameRestarted_TestApp);

            // Verify the data in the restarted output file
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);

            // Verify that echo is NOT part of the output - won't pop assert on fail so check return value
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 0, true, testName, false, false);
            if (pass == true)
            {
                Assert.Fail("<JS_PTI_MigrateClient_Test> Echoed string should NOT have been found in the output but it was.");
            }
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true);

            // Verify integrity of Ambrosia logs by replaying 
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, true);
        }

        //**  Similar to restart after both killed, but this is case where the process is NOT killed and the process is just restarted. 
        //** The second process takes over and the original processes die. It is the way would migrate the client version.
        //* For the Bidirectional option
        [TestMethod]
        public void JS_PTI_MigrateClient_BiDi_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 6;
            long totalBytes = 6442450944;
            long totalEchoBytes = 6442450944;
            int bytesPerRound = 0;
            int maxMessageSize = 0;
            int batchSizeCutoff = 0;
            int messagesSent = 1032192;
            bool bidi = true;

            string testName = "jsptimigrateclientbiditest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";
            string logOutputFileNameRestarted_TestApp = testName + "_TestApp_Restarted.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_LBOpt_deleteLogs, "false");   // default is false but ok to specifically state in case default changes

            // Start it once
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp);

            // Give it 5 seconds where it tries to connect but doesn't
            Thread.Sleep(5000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            // DO NOT Kill both app 
            // This is main part of test - get it to have Client and Server take over and run and orig Client and Server are stopped
            // MyUtils.KillProcess("node");

            // Change the ports in the config files before restarting so doesn't conflict port numbers
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "3520");
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "3020");
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "3021");

            // Restart it and make sure it continues
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileNameRestarted_TestApp);

            // Verify the data in the restarted output file
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "Bytes received: " + totalBytes.ToString(), 15, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "[IC] Connected!", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true);

            // Verify integrity of Ambrosia logs by replaying
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, true);
        }

        //** Upgrade Server test - tests that the version number of the server can be upgraded
        [TestMethod]
        public void JS_PTI_UpgradeServer_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 20;
            long totalBytes = 327680;
            long totalEchoBytes = 327680;
            int bytesPerRound = 16384;
            int maxMessageSize = 32;
            int batchSizeCutoff = 16384;
            bool bidi = false;

            string testName = "jsptiupgradeservertest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";
            string logOutputFileNameRestarted_TestApp = testName + "_TestApp_Restarted.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_LBOpt_deleteLogs, "false");   // default is false but ok to specifically state in case default changes
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_activeCode, "VCurrent");  // should be the default but just in case not
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_appVersion, "0");

            // Start it once
            int ptiID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp);

            // Once it connects we know it is registered so kill it
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "[IC] Ready ...", 1, false, testName, true,false);

            // Kill Server which will kill the IC too
            MyUtils.KillProcess(ptiID);

            //Set the Upgrade Version
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_upgradeVersion, "11");
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "false");

            // Restart it and make sure it continues
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileNameRestarted_TestApp);

            // Verify the data in the restarted output file
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);

            // Verify that echo is NOT part of the output - won't pop assert on fail so check return value
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 0, true, testName, false, false);
            if (pass == true)
            {
                Assert.Fail("<JS_PTI_UpgradeServer_Test> Echoed string should NOT have been found in the output but it was.");
            }
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "All rounds complete", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "Upgrade complete", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "VNext: Successfully upgraded!", 1, false, testName, true);

        }

        //** Upgrades the version number of server
        //* For the Bidirectional option
        [TestMethod]
        public void JS_PTI_UpgradeServer_BiDi_Test()
        {

            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 6;
            long totalBytes = 6442450944;
            long totalEchoBytes = 6442450944;
            int bytesPerRound = 0;
            int maxMessageSize = 0;
            int batchSizeCutoff = 0;
            bool bidi = true;

            string testName = "jsptiupgradeserverbiditest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";
            string logOutputFileNameRestarted_TestApp = testName + "_TestApp_Restarted.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_LBOpt_deleteLogs, "false");
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_activeCode, "VCurrent");  // should be the default but just in case not
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_appVersion, "0");

            // Start it once
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp);

            // wait for it to connect so know it is registered
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "[IC] Ready ...", 1, false, testName, true, false);

            // Kill Server and any corresponding IC
            MyUtils.StopAllAmbrosiaProcesses();

            //Set the Upgrade Version
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "false");
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_upgradeVersion, "1");

            // Restart it and make sure it continues
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileNameRestarted_TestApp);

            // Verify the data in the restarted output file
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "Bytes received: " + totalBytes.ToString(), 15, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "All rounds complete", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "[IC] Connected!", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "Upgrade complete", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "VNext: Successfully upgraded!", 1, false, testName, true);

        }


        //**  End to End for Two Proc that is NOT bidirectional and where the Server is stopped and restarted 
        [TestMethod]
        public void JS_PTI_UpgradeServerTwoProc_Test()
        {

            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 6;
            long totalBytes = 6442450944;
            long totalEchoBytes = 6442450944;
            int bytesPerRound = 0;
            int maxMessageSize = 0;
            int batchSizeCutoff = 0;
            bool bidi = false;

            string testName = "jsptiupgradeservertwoproctest";
            string clientInstanceName = testName + "client";
            string serverInstanceName = testName + "server";
            string logOutputClientFileName_TestApp = testName + "Client_TestApp.log";
            string logOutputServerFileName_TestApp = testName + "Server_TestApp.log";
            string logOutputServerRestartedFileName_TestApp = testName + "Server_TestApp_Restarted.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName, JSUtils.JSPTI_CombinedInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);

            // Update the client config file
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_LBOpt_deleteLogs, "false", JSUtils.JSPTI_ClientInstanceRole); 
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_activeCode, "VCurrent", JSUtils.JSPTI_ClientInstanceRole);  
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_appVersion, "0", JSUtils.JSPTI_ClientInstanceRole);

            // Update the server config file
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_LBOpt_deleteLogs, "false", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_activeCode, "VCurrent", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_appVersion, "0", JSUtils.JSPTI_ServerInstanceRole);

            // Start it once - Launch the client and the server as separate procs 
            int serverProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole, "", clientInstanceName);
            int clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Give it 10 seconds to get some going 
            Thread.Sleep(10000);
            Application.DoEvents();  

            // Kill server
            MyUtils.KillProcess(serverProcessID);

            //Set the Upgrade Version
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "false", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_upgradeVersion, "120", JSUtils.JSPTI_ServerInstanceRole);

            // Restart the server and make sure it continues
            serverProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerRestartedFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole, "", clientInstanceName);

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
            pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartedFileName_TestApp, "Upgrade complete", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartedFileName_TestApp, "VNext: Successfully upgraded!", 1, false, testName, true);
        }


        //**  End to End for Two Proc that is bidirectional and where the Server is stopped and restarted 
        [TestMethod]
        public void JS_PTI_UpgradeServerTwoProc_BiDi_Test()
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

            string testName = "jsptiupgradeservertwoprocbiditest";
            string clientInstanceName = testName + "client";
            string serverInstanceName = testName + "server";
            string logOutputClientFileName_TestApp = testName + "Client_TestApp.log";
            string logOutputServerFileName_TestApp = testName + "Server_TestApp.log";
            string logOutputServerRestartedFileName_TestApp = testName + "Server_TestApp_Restarted.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName, JSUtils.JSPTI_CombinedInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);

            // Update the client config file
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_LBOpt_deleteLogs, "false", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_activeCode, "VCurrent", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_appVersion, "0", JSUtils.JSPTI_ClientInstanceRole);

            // Update the server config file
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_LBOpt_deleteLogs, "false", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_activeCode, "VCurrent", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_appVersion, "119", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_upgradeVersion, "119", JSUtils.JSPTI_ServerInstanceRole);


            // Start it once - Launch the client and the server as separate procs 
            int serverProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole, "", clientInstanceName);
            int clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Give it 10 seconds to get some going 
            Thread.Sleep(10000);
            Application.DoEvents();

            // Kill server
            MyUtils.KillProcess(serverProcessID);

            //Set the Upgrade Version
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "false", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_upgradeVersion, "120", JSUtils.JSPTI_ServerInstanceRole);

            // Restart the server and make sure it continues
            serverProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerRestartedFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole, "", clientInstanceName);

            // Verify the data in the output file of the server
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartedFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 10, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartedFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartedFileName_TestApp, "[IC] Connected!", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartedFileName_TestApp, "Upgrade complete", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartedFileName_TestApp, "VNext: Successfully upgraded!", 1, false, testName, true);


            // Verify the data in the output file of the CLIENT 
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 5, true, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "All rounds complete", 5, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "[IC] Connected!", 1, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true, false);

        }


        //** Upgrade Server test - tests that the version number of the server can be upgraded and then ran again to make sure it uses the updated server number
        [TestMethod]
        public void JS_PTI_UpgradeServerBackToBack_Test()
        {

            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 15;
            long totalBytes = 245760;
            long totalEchoBytes = 245760;
            int bytesPerRound = 16384;
            int maxMessageSize = 32;
            int batchSizeCutoff = 16384;
            bool bidi = false;

            string testName = "jsptiupgradeserverbacktobacktest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";
            string logOutputFileNameRestarted_TestApp = testName + "_TestApp_Restarted.log";
            string logOutputFileNameRestartedAgain_TestApp = testName + "_TestApp_Restarted_Again.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_LBOpt_deleteLogs, "false");   // default is false but ok to specifically state in case default changes
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_activeCode, "VCurrent");  // should be the default but just in case not
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_appVersion, "50");
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_upgradeVersion, "50");

            // Start it once
            int ptiID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp);

            // Once it connects we know it is registered so kill it
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "[IC] Ready ...", 1, false, testName, true, false);

            // Kill Server which will kill the IC too
            MyUtils.KillProcess(ptiID);

            //Set the Upgrade Version
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_upgradeVersion, "51");
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "false");

            // Restart it once and make sure it continues
            int ptiID2 = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileNameRestarted_TestApp);

            // Make sure all upgraded and ran fine
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "Upgrade complete", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "VNext: Successfully upgraded!", 1, false, testName, true);

            // Kill Server which will kill the IC too
            MyUtils.KillProcess(ptiID2);

            // Restart it once and make sure it continues with the new version
            int ptiID3 = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileNameRestartedAgain_TestApp);

            // Verify the data in the restarted output file
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestartedAgain_TestApp, "VNext: Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestartedAgain_TestApp, "VNext: SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);

            // Verify that echo is NOT part of the output - won't pop assert on fail so check return value
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestartedAgain_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 0, true, testName, false, false);
            if (pass == true)
            {
                Assert.Fail("<JS_PTI_UpgradeServerBackToBack_Test> Echoed string should NOT have been found in the output but it was.");
            }
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestartedAgain_TestApp, "VNext: All rounds complete", 1, false, testName, true);

        }

    }
}

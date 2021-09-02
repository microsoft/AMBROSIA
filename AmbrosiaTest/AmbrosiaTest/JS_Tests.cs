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
            int maxMessageSize = 32;
            int batchSizeCutoff = 32;
            int messagesSent = 36;
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
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "[IC] I'm a checkpointer", 1, false, testName, true);  // since it is done, it looks like it is a check pointer instead of doing connection
            pass = MyUtils.WaitForProcessToFinish(logOutputFileNameRestarted_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true);

            // Verify integrity of Ambrosia logs by replaying
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, true);
        }



        //*#*## TO DO:  Add the "deleteLogs" tests for Blobs *#*#*#*

        //** Runs the built in unit tests 
        [TestMethod]
        public void JS_NodeUnitTests()
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
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, finishedString, 2, false, testName, true,false);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, successString, 1, false, testName, true,false);

        }


    }
}

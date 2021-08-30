using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Windows.Forms; // need this to handle threading issue on sleeps
using System.Configuration;


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



        //** Setting the largest maxMessageSize and batchSizeCutoff since maxMessageSize is the size of payload and batchSizeCutoff is the number of message - this is not Fixed message size so as it is descending
        //** C# LB uses 64 MB for GiantMessageTest
        //**
        //**  NOTE - this test takes kind of a long time to run especially do more than 2 rounds. Maybe longer than it should:  Bug #166 - Large Messages have poor performance
        //**
        [TestMethod]
        public void JS_PTI_GiantMessage_BiDi_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 2;
            long totalBytes = 536870912;
            long totalEchoBytes = 536870912;
            int bytesPerRound = 268435456; // 256 MB
            int maxMessageSize = 67108864;  // 64 MBs
            int batchSizeCutoff = 1;
            int messagesSent = 12;
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
            int batchSizeCutoff = 1;
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
            //pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 5, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "[IC] Connected!", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true);

            // Verify integrity of Ambrosia logs by replaying
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, true);
        }

        //** Setting the largest memoryUsed 
        [TestMethod]
        public void JS_PTI_GiantCheckPoint_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 12;
            long totalBytes = 512;
            long totalEchoBytes = 512;
            int bytesPerRound = 128;
            int maxMessageSize = 64;
            int batchSizeCutoff = 32;
            int messagesSent = 22;

            string testName = "jsptigiantcheckpointtest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, true, logOutputFileName_TestApp);

            // Verify the data in the output file - too many changing rows in output to do a cmp file so verify some of the key lines
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "[IC] Connected!", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true);

            // Verify integrity of Ambrosia logs by replaying
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, true, true, true);
        }



        //** Runs the built in unit tests 
        [TestMethod]
        public void JS_NodeUnitTests()
        {

            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            string testName = "jsnodeunittest";
            string finishedString = "UNIT TESTS COMPLETE";
            string successString = "SUMMARY: 112 passed (100%), 0 failed (0%)";
            string logOutputFileName_TestApp = testName + "_TestApp.log";

            // Launched all the unit tests for JS Node (npm run unittests)
            int JSTestAppID = JSUtils.StartJSNodeUnitTests(logOutputFileName_TestApp);

            // Wait until summary at the end and if not there, then know not finished
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, finishedString, 2, false, testName, true,false);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, successString, 1, false, testName, true,false);

        }


    }
}

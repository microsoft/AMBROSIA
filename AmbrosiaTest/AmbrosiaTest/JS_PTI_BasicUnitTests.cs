using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Windows.Forms; // need this to handle threading issue on sleeps
using System.Configuration;


namespace AmbrosiaTest
{
    [TestClass]
    public class JS_PTI_BasicUnitTests
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

        //** Basic End to End that is bidirectional where ehoing the 'doWork' method call back to the client
        [TestMethod]
        public void JS_PTI_BasicEndToEnd_BiDi_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 2;
            long totalBytes = 256;
            long totalEchoBytes = 256;
            int bytesPerRound = 128;
            int maxMessageSize = 32;
            int batchSizeCutoff = 32;
            int messagesSent = 12;

            string testName = "jsptibidiendtoendtest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, true, logOutputFileName_TestApp);

            // Verify the data in the output file - too many changing rows in output to do a cmp file so verify some of the key lines
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: "+ totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of bytes ("+ totalBytes.ToString() + ") have been received", 1, false, testName, true); 
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of echoed bytes ("+ totalEchoBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "All rounds complete ("+ messagesSent.ToString()+ " messages sent)", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "[IC] Connected!", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "round #"+ numRounds.ToString(), 1, false, testName, true);

            // Verify integrity of Ambrosia logs by replaying
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds,totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize,batchSizeCutoff, true, true, true);
        }


        //** Basic End to End that is NOT bidirectional
        [TestMethod]
        public void JS_PTI_BasicEndToEnd_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 4;
            long totalBytes = 512;
            long totalEchoBytes = 512;
            int bytesPerRound = 128;
            int maxMessageSize = 32;
            int batchSizeCutoff = 32;
            int messagesSent = 28;

            string testName = "jsptiendtoendtest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, false, logOutputFileName_TestApp);

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
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, false, true, true);
        }


    }
}

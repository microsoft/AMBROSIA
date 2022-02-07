using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Windows.Forms; // need this to handle threading issue on sleeps
using System.Configuration;
using System.IO;


namespace AmbrosiaTest
{
    [TestClass]
    public class JS_Blob_Tests
    {
        //************* Init Code *****************
        // NOTE: Make sure all names be "Azure Safe". No capital letters and no underscore.
        [TestInitialize()]
        public void Initialize()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            // generic Ambrosia init 
            MyUtils.TestInitialize(true);

            // Set config file back to the way it was 
            JSUtils.JS_RestoreJSConfigFile();
        }
        //************* Init Code *****************


        [TestCleanup()]
        public void Cleanup()
        {
            // Kill all exes associated with tests
            JS_Utilities JSUtils = new JS_Utilities();
            JSUtils.JS_TestCleanup_Blob();
        }

        //** Test of saving the log to the blob instead of file
        [TestMethod]
        public void JS_PTI_Blob_Test()
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
            bool bidi = false;

            string testName = "jsptisavetoblobtest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";

            // update config values for test 
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icLogStorageType, JSUtils.logTypeBlobs);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icLogFolder, "");

            // Launch but it is using blobs instead of files
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp);

            // Verify the data in the output file - too many changing rows in output to do a cmp file so verify some of the key lines
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 10, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);

            // Verify that echo is NOT part of the output - won't pop assert on fail so check return value
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 0, true, testName, false, false);
            if (pass == true)
            {
                Assert.Fail("<JS_PTI_Blob_Basic_Test> Echoed string should NOT have been found in the output but it was.");
            }
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "[IC] Connected!", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true);

            // Verify that directory does not exist to show it wasn't in a file as not real easy ways to verify blob
            string logDirectory = MyUtils.baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"];
            string expectedLogFile = logDirectory + "\\" + testName + "_0";

            if (Directory.Exists(expectedLogFile))
            {
                Assert.Fail("<JS_PTI_Blob_Basic_Test> - Directory:" + expectedLogFile + " was found when it shouldn't have been.");
            }
        }

        //** Test of saving the log to the blob instead of file for bi directional 
        [TestMethod]
        public void JS_PTI_Blob_BiDi_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 8;
            long totalBytes = 1024;
            long totalEchoBytes = 1024;
            int bytesPerRound = 128;
            int maxMessageSize = 64;
            int batchSizeCutoff = 64;
            int messagesSent = 16;
            bool bidi = true;

            string testName = "jsptisavetoblobbiditest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";

            // update config values for test 
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icLogStorageType, JSUtils.logTypeBlobs);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icLogFolder, "");

            // Launch but it is using blobs instead of files
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp);

            // Verify the data in the output file - too many changing rows in output to do a cmp file so verify some of the key lines
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "[IC] Connected!", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true);

            // Verify that directory does not exist to show it wasn't in a file as not real easy ways to verify blob
            string logDirectory = MyUtils.baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"];
            string expectedLogFile = logDirectory + "\\" + testName + "_0";

            if (Directory.Exists(expectedLogFile))
            {
                Assert.Fail("<JS_PTI_Blob_Basic_BiDi_Test> - Directory:" + expectedLogFile + " was found when it shouldn't have been.");
            }
        }


        //** Test of saving the log to the blob instead of file WITHOUT having a blank icLogFolder
        [TestMethod]
        public void JS_PTI_Blob_NoBlankICLogFolder_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 7;
            long totalBytes = 14336;
            long totalEchoBytes = 14336;
            int bytesPerRound = 2048;
            int maxMessageSize = 128;
            int batchSizeCutoff = 128;
            int messagesSent = 208;
            bool bidi = false;

            string testName = "jsptiblobnoblankictest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";

            // update config values for test 
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icLogStorageType, JSUtils.logTypeBlobs);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icLogFolder, testName);  // This is a big part of test - do NOT make this blank or leave in the path. Have it as 

            // Launch but it is using blobs instead of files
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp);

            // Verify the data in the output file - too many changing rows in output to do a cmp file so verify some of the key lines
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);

            // Verify that echo is NOT part of the output - won't pop assert on fail so check return value
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 0, true, testName, false, false);
            if (pass == true)
            {
                Assert.Fail("<JS_PTI_Blob_NoBlankICLogFolder_Test> Echoed string should NOT have been found in the output but it was.");
            }
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "[IC] Connected!", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true);

            // Verify that directory does not exist to show it wasn't in a file as not real easy ways to verify blob
            string logDirectory = MyUtils.baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"];
            string expectedLogFile = logDirectory + "\\" + testName + "_0";

            if (Directory.Exists(expectedLogFile))
            {
                Assert.Fail("<JS_PTI_Blob_NoBlankICLogFolder_Test> - Directory:" + expectedLogFile + " was found when it shouldn't have been.");
            }
        }


        //** Basic End to End that is NOT bidirectional where Client and Server are in separate Procs and saving to a blob
        [TestMethod]
        public void JS_PTI_Blob_TwoProc_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            string testName = "jsptiblobtwoproctest";

            int numRounds = 4;
            long totalBytes = 16384;
            long totalEchoBytes = 16384;
            int bytesPerRound = 4096;
            int maxMessageSize = 256;
            int batchSizeCutoff = 256;
            int messagesSent = 176;
            bool bidi = false;
            string clientInstanceName = testName + "client";
            string serverInstanceName = testName + "server";

            string logOutputClientFileName_TestApp = testName + "Client_TestApp.log";
            string logOutputServerFileName_TestApp = testName + "Server_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName, JSUtils.JSPTI_CombinedInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icLogStorageType, JSUtils.logTypeBlobs, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icLogFolder, "", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icLogStorageType, JSUtils.logTypeBlobs, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icLogFolder, "", JSUtils.JSPTI_ServerInstanceRole);

            // Launch the client and the server as separate procs 
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Verify the data in the output file of the server
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 10, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "[IC] Connected!", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "logStorageType=Blobs", 1, false, testName, true);

            // Verify the data in the output file of the CLIENT - since not bidi, no echoed bytes
            // Verify that echo is NOT part of the output for client
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 0, true, testName, false, false);
            if (pass == true)
            {
                Assert.Fail("<JS_PTI_Blob_BasicTwoProc_Test> Echoed string should NOT have been found in the CLIENT output but it was.");
            }
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 5, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "[IC] Connected!", 1, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "logStorageType=Blobs", 1, false, testName, true, false);

            // Verify that directory does not exist to show it wasn't in a file as not real easy ways to verify blob
            string logDirectory = MyUtils.baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"];
            string expectedClientLogFile = logDirectory + "\\" + testName + "client_0";
            if (Directory.Exists(expectedClientLogFile))
            {
                Assert.Fail("<JS_PTI_Blob_BasicTwoProc_Test> - Directory:" + expectedClientLogFile + " was found when it shouldn't have been.");
            }
            string expectedServerLogFile = logDirectory + "\\" + testName + "server_0";
            if (Directory.Exists(expectedServerLogFile))
            {
                Assert.Fail("<JS_PTI_Blob_BasicTwoProc_Test> - Directory:" + expectedServerLogFile + " was found when it shouldn't have been.");
            }
        }


        //** Basic End to End that is bidirectional where Client and Server are in separate Procs and saving to a blob
        [TestMethod]
        public void JS_PTI_Blob_TwoProc_BiDi_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            string testName = "jsptiblobtwoprocbiditest";

            int numRounds = 2;
            long totalBytes = 8192;
            long totalEchoBytes = 8192;
            int bytesPerRound = 4096;
            int maxMessageSize = 256;
            int batchSizeCutoff = 256;
            int messagesSent = 48;
            bool bidi = true;
            string clientInstanceName = testName + "client";
            string serverInstanceName = testName + "server";

            string logOutputClientFileName_TestApp = testName + "Client_TestApp.log";
            string logOutputServerFileName_TestApp = testName + "Server_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName, JSUtils.JSPTI_CombinedInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icLogStorageType, JSUtils.logTypeBlobs, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icLogFolder, "", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icLogStorageType, JSUtils.logTypeBlobs, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icLogFolder, "", JSUtils.JSPTI_ServerInstanceRole);

            // Launch the client and the server as separate procs 
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Verify the data in the output file of the server
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 10, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "[IC] Connected!", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "logStorageType=Blobs", 1, false, testName, true);

            // Verify the data in the output file of the CLIENT 
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 5, true, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 5, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "[IC] Connected!", 1, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true, false);

        }



        //** Basic End to End that is NOT bidirectional and Client saves to Blob and Server saves to File
        [TestMethod]
        public void JS_PTI_BlobClientFile_TwoProc_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            string testName = "jsptiblobclientfiletwoproctest";

            int numRounds = 4;
            long totalBytes = 16384;
            long totalEchoBytes = 16384;
            int bytesPerRound = 4096;
            int maxMessageSize = 256;
            int batchSizeCutoff = 256;
            int messagesSent = 176;
            bool bidi = false;
            string clientInstanceName = testName + "client";
            string serverInstanceName = testName + "server";

            string logOutputClientFileName_TestApp = testName + "Client_TestApp.log";
            string logOutputServerFileName_TestApp = testName + "Server_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName, JSUtils.JSPTI_CombinedInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icLogStorageType, JSUtils.logTypeBlobs, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icLogFolder, "", JSUtils.JSPTI_ClientInstanceRole);
            //  JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icLogStorageType, JSUtils.logTypeBlobs, JSUtils.JSPTI_ServerInstanceRole);  // Keep Server as a File
            //  JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icLogFolder, "", JSUtils.JSPTI_ServerInstanceRole);

            // Launch the client and the server as separate procs 
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Verify the data in the output file of the server
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 10, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "[IC] Connected!", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "logStorageType=Files", 1, false, testName, true);

            // Verify the data in the output file of the CLIENT - since not bidi, no echoed bytes
            // Verify that echo is NOT part of the output for client
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 0, true, testName, false, false);
            if (pass == true)
            {
                Assert.Fail("<JS_PTI_BlobClientFile_BasicTwoProc_Test> Echoed string should NOT have been found in the CLIENT output but it was.");
            }
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 5, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "[IC] Connected!", 1, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "logStorageType=Blobs", 1, false, testName, true, false);

            // Verify that directory does not exist to show it wasn't in a file as not real easy ways to verify blob
            string logDirectory = MyUtils.baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"];
            string expectedClientLogFile = logDirectory + "\\" + testName + "client_0";
            if (Directory.Exists(expectedClientLogFile))
            {
                Assert.Fail("<JS_PTI_BlobClientFile_BasicTwoProc_Test> - Directory:" + expectedClientLogFile + " was found when it shouldn't have been.");
            }
            string expectedServerLogFile = logDirectory + "\\" + testName + "server_0";
            if (Directory.Exists(expectedServerLogFile) == false )
            {
                Assert.Fail("<JS_PTI_BlobClientFile_BasicTwoProc_Test> - Directory:" + expectedServerLogFile + " was NOT found.");
            }

            // Verify integrity of Ambrosia logs by replaying
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true,"", JSUtils.JSPTI_ServerInstanceRole);

        }


        //** Basic End to End that is NOT bidirectional and Client saves to file and Server saves to blob
        [TestMethod]
        public void JS_PTI_BlobServerFile_TwoProc_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            string testName = "jsptiblobserverfiletwoproctest";

            int numRounds = 4;
            long totalBytes = 16384;
            long totalEchoBytes = 16384;
            int bytesPerRound = 4096;
            int maxMessageSize = 256;
            int batchSizeCutoff = 256;
            int messagesSent = 176;
            bool bidi = false;
            string clientInstanceName = testName + "client";
            string serverInstanceName = testName + "server";

            string logOutputClientFileName_TestApp = testName + "Client_TestApp.log";
            string logOutputServerFileName_TestApp = testName + "Server_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName, JSUtils.JSPTI_CombinedInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            // JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icLogStorageType, JSUtils.logTypeBlobs, JSUtils.JSPTI_ClientInstanceRole);  // keep client as a file
            // JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icLogFolder, "", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icLogStorageType, JSUtils.logTypeBlobs, JSUtils.JSPTI_ServerInstanceRole);  
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icLogFolder, "", JSUtils.JSPTI_ServerInstanceRole);

            // Launch the client and the server as separate procs 
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Verify the data in the output file of the server
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 10, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "[IC] Connected!", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "logStorageType=Blobs", 1, false, testName, true);

            // Verify the data in the output file of the CLIENT - since not bidi, no echoed bytes
            // Verify that echo is NOT part of the output for client
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 0, true, testName, false, false);
            if (pass == true)
            {
                Assert.Fail("<JS_PTI_BlobServerFile_BasicTwoProc_Test> Echoed string should NOT have been found in the CLIENT output but it was.");
            }
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 5, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "[IC] Connected!", 1, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "logStorageType=Files", 1, false, testName, true, false);

            // Verify that directory does not exist to show it wasn't in a file as not real easy ways to verify blob
            string logDirectory = MyUtils.baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"];
            string expectedClientLogFile = logDirectory + "\\" + testName + "client_0";
            if (Directory.Exists(expectedClientLogFile) ==  false)
            {
                Assert.Fail("<JS_PTI_BlobServerFile_BasicTwoProc_Test> - Directory:" + expectedClientLogFile + " was NOT found.");
            }
            string expectedServerLogFile = logDirectory + "\\" + testName + "server_0";
            if (Directory.Exists(expectedServerLogFile))
            {
                Assert.Fail("<JS_PTI_BlobServerFile_BasicTwoProc_Test> - Directory:" + expectedServerLogFile + " was found when it shouldn't have been.");
            }
        }


        //** Test of deleting the blob log 
        [TestMethod]
        public void JS_PTI_Blob_DeleteLog_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 4;
            long totalBytes = 512;
            long totalEchoBytes = 512;
            int bytesPerRound = 128;
            int maxMessageSize = 64;
            int batchSizeCutoff = 32;
            bool bidi = false;

            string testName = "jsptiblobdeletelogtest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";
            string logOutputFileName_2_TestApp = testName + "_2_TestApp.log";

            // update config values for test 
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icLogStorageType, JSUtils.logTypeBlobs);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icLogFolder, "");

            // Launch it once
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp);

            // Verify the data in the output file - too many changing rows in output to do a cmp file so verify some of the key lines
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed

            // set to delete the log
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_LBOpt_deleteLogs, "true");

            // Launch it again 
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_2_TestApp);

            // Verify the data in the output file - too many changing rows in output to do a cmp file so verify some of the key lines
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_2_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed

            //*** If the logs were NOT deleted then it would be "loading" a check point. If it is deleted then the checkpoint is just saved
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_2_TestApp, "Checkpoint saved:", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_2_TestApp, "Checkpoint loaded", 0, true, testName, false, false);
            if (pass == true)
            {
                Assert.Fail("<JS_PTI_Blob_DeleteLog_Test> Checkpoint loaded was found so must read log files that already existed.");
            }
        }

    }
}

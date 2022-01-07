using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Windows.Forms; // need this to handle threading issue on sleeps
using System.Configuration;
using System.IO;


namespace AmbrosiaTest
{
    [TestClass]
    public class JS_HostingMode_Tests
    {
        //************* Init Code *****************
        // NOTE: Make sure all names be "Azure Safe". No capital letters and no underscore.
        [TestInitialize()]
        public void Initialize()
        {
            Utilities MyUtils = new Utilities();

            // generic Ambrosia init 
            MyUtils.TestInitialize();

            // Set config file back to the way it was 
            RestoreSeparatedJSConfigFile();
        }
        //************* Init Code *****************


        [TestCleanup()]
        public void Cleanup()
        {
            // Kill all exes associated with tests
            JS_Utilities JSUtils = new JS_Utilities();
            JSUtils.JS_TestCleanup_HostingMode();
        }

        // Derived from JSUtils.JS_RestoreJSConfigFile but modified for the config file for separated 
        public void RestoreSeparatedJSConfigFile()
        {
            try
            {
                Utilities MyUtils = new Utilities();
                JS_Utilities JSUtils = new JS_Utilities();

                // ** Restore Config file from golden one
                string basePath = ConfigurationManager.AppSettings["AmbrosiaJSTestDirectory"];
                string basePTIPath = ConfigurationManager.AppSettings["AmbrosiaJSPTIDirectory"];
                string ambrosiaSeparatedGoldConfigfileName = "ambrosiaConfig.separatedGOLD.json";
                string ambrosiaConfigfileName = "ambrosiaConfig.json";

                //** Set defaults that are test run specific
                string CurrentFramework = MyUtils.NetFramework;
                if (MyUtils.NetFrameworkTestRun == false)
                {
                    CurrentFramework = MyUtils.NetCoreFramework;
                }

                //*** Copy from The Gold Config to App Config ***
                File.Copy(basePath + "\\" + ambrosiaSeparatedGoldConfigfileName, basePTIPath + JSUtils.JSPTI_AppPath+"\\" + ambrosiaConfigfileName, true);

                //*** Copy from The Gold Config to Client Config ***
                File.Copy(basePath + "\\" + ambrosiaSeparatedGoldConfigfileName, basePTIPath + JSUtils.JSPTI_ClientPath+"\\" + ambrosiaConfigfileName, true);

                // Set the defaults based on current system
                JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "2510", JSUtils.JSPTI_ClientInstanceRole);
                JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "2010", JSUtils.JSPTI_ClientInstanceRole);
                JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "2011", JSUtils.JSPTI_ClientInstanceRole);

                //*** Copy from The Gold Config to Server Config ***
                File.Copy(basePath + "\\" + ambrosiaSeparatedGoldConfigfileName, basePTIPath + JSUtils.JSPTI_ServerPath+"\\" + ambrosiaConfigfileName, true);

                // Set the defaults based on current system
                JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "2500", JSUtils.JSPTI_ServerInstanceRole);
                JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "2000", JSUtils.JSPTI_ServerInstanceRole);
                JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "2001", JSUtils.JSPTI_ServerInstanceRole);
            }
            catch (Exception e)
            {
                Assert.Fail("<RestoreSeparatedJSConfigFile> Failure! " + e.Message);
            }
        }

        //**  Testing the "Separate" option for the feature in ambrosiaConfig.json ( "icHostingMode": "Separated",) but with two procs
        [TestMethod]
        public void JS_PTI_HostingModeSeparate_TwoProc_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 2;
            long totalBytes = 8192;
            long totalEchoBytes = 8192;
            int bytesPerRound = 4096;
            int maxMessageSize = 256;
            int batchSizeCutoff = 256;
            int messagesSent = 48;
            bool bidi = false;

            string testName = "jsptihostmodeseparatetesttwoproc";
            string clientInstanceName = testName + "client";
            string serverInstanceName = testName + "server";
            string logOutputClientFileName_TestApp = testName + "Client_TestApp.log";
            string logOutputServerFileName_TestApp = testName + "Server_TestApp.log";

            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string logOutputFileName_ImmCoord1 = testName + "_ImmCoord1.log";

            // Set name and ports to match IC call for client and server
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "1500", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "1000", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "1001", JSUtils.JSPTI_ClientInstanceRole);

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "2500", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "2000", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "2001", JSUtils.JSPTI_ServerInstanceRole);


            // Manually register the instance
            string logOutputFileName_AMB1 = testName + "_AMB1.log";
            AMB_Settings AMB1 = new AMB_Settings
            {
                AMB_ServiceName = clientInstanceName,
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
                AMB_ServiceName = serverInstanceName,
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

            // manually start the IC
            int ImmCoordProcessID1 = MyUtils.StartImmCoord(clientInstanceName, 1500, logOutputFileName_ImmCoord1);

            //ImmCoord2
            string logOutputFileName_ImmCoord2 = testName + "_ImmCoord2.log";
            int ImmCoordProcessID2 = MyUtils.StartImmCoord(serverInstanceName, 2500, logOutputFileName_ImmCoord2);

            // Start JS client and server
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, false, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Verify the data in the restarted output file
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); 
            pass = MyUtils.WaitForProcessToFinish(logOutputServerFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);

            // Verify that echo is NOT part of the output - won't pop assert on fail so check return value
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 0, true, testName, false, false);
            if (pass == true)
            {
                Assert.Fail("<JS_PTI_HostingModeSeparate_TwoProc_Test> Echoed string should NOT have been found in the output but it was.");
            }
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName,true,false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "[IC]", 0, true, testName, false, false);  // shouldn't be any IC comments
            if (pass == true)
            {
                Assert.Fail("<JS_PTI_HostingModeSeparate_TwoProc_Test> There shouldn't be any Imm Coord messages in output since separate process");
            }

            // Do not verify logs as the setting 'debugStartCheckpoint' is not allowed for Separate host mode
            //JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true);
        }



        //**  Testing the "Separate" option for the feature in ambrosiaConfig.json ( "icHostingMode": "Separated",)
        [TestMethod]
        public void JS_PTI_HostingModeSeparate_Test()
        {

            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 2;
            long totalBytes = 8192;
            long totalEchoBytes = 8192;
            int bytesPerRound = 4096;
            int maxMessageSize = 256;
            int batchSizeCutoff = 256;
            int messagesSent = 48;
            bool bidi = false;

            string testName = "jsptihostmodeseparatetest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string logOutputFileName_ImmCoord1 = testName + "_ImmCoord1.log";

            // Set name and ports to match IC call
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "1500");
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "1000");
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "1001");

            // Manually register the instance
            string logOutputFileName_AMB1 = testName + "_AMB1.log";
            AMB_Settings AMB1 = new AMB_Settings
            {
                AMB_ServiceName = testName,
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

            // manually start the IC
            int ImmCoordProcessID1 = MyUtils.StartImmCoord(testName, 1500, logOutputFileName_ImmCoord1);

            // Start JS app 
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp);

            // Verify the data in the restarted output file
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); 
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);

            // Verify that echo is NOT part of the output - won't pop assert on fail so check return value
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 0, true, testName, false, false);
            if (pass == true)
            {
                Assert.Fail("<JS_PTI_HostingModeSeparate_Test> Echoed string should NOT have been found in the output but it was.");
            }
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "[IC]", 0, true, testName, false, false);  // shouldn't be any IC comments
            if (pass == true)
            {
                Assert.Fail("<JS_PTI_HostingModeSeparate_Test> There shouldn't be any Imm Coord messages in output since separate process");
            }

        }

        //**  Testing the "Separate" option for the feature in ambrosiaConfig.json ( "icHostingMode": "Separated",) but with BiDi turned on 
        [TestMethod]
        public void JS_PTI_HostingModeSeparate_BiDi_Test()
        {

            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 2;
            long totalBytes = 8192;
            long totalEchoBytes = 8192;
            int bytesPerRound = 4096;
            int maxMessageSize = 256;
            int batchSizeCutoff = 256;
            int messagesSent = 48;
            bool bidi = true;

            string testName = "jsptihostmodesepbiditest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string logOutputFileName_ImmCoord1 = testName + "_ImmCoord1.log";

            // Set name and ports to match IC call
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "1500");
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "1000");
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "1001");

            // Manually register the instance
            string logOutputFileName_AMB1 = testName + "_AMB1.log";
            AMB_Settings AMB1 = new AMB_Settings
            {
                AMB_ServiceName = testName,
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

            // manually start the IC
            int ImmCoordProcessID1 = MyUtils.StartImmCoord(testName, 1500, logOutputFileName_ImmCoord1);

            // Start JS app 
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp);

            // Verify the data in the output file - too many changing rows in output to do a cmp file so verify some of the key lines
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true);

            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "[IC]", 0, true, testName, false, false);  // shouldn't be any IC comments
            if (pass == true)
            {
                Assert.Fail("<JS_PTI_HostingModeSeparate_BiDi_Test> There shouldn't be any Imm Coord messages in output since separate process");
            }
        }


        //**  Testing the "Separate" option for the feature in ambrosiaConfig.json ( "icHostingMode": "Separated")
        //** This starts PTI before IC is started
        [TestMethod]
        public void JS_PTI_HostingModeSeparateStartPTIFirst_BiDi_Test()
        {

            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 2;
            long totalBytes = 8192;
            long totalEchoBytes = 8192;
            int bytesPerRound = 4096;
            int maxMessageSize = 256;
            int batchSizeCutoff = 256;
            int messagesSent = 48;
            bool bidi = true;

            string testName = "jsptihostmodesepptifirsttest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
            string logOutputFileName_ImmCoord1 = testName + "_ImmCoord1.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "1500");
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "1000");
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "1001");

            // Manually register the instance
            string logOutputFileName_AMB1 = testName + "_AMB1.log";
            AMB_Settings AMB1 = new AMB_Settings
            {
                AMB_ServiceName = testName,
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

            // Start JS app 
            JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputFileName_TestApp);

            // manually start the IC after the PTI was started
            int ImmCoordProcessID1 = MyUtils.StartImmCoord(testName, 1500, logOutputFileName_ImmCoord1);

            // Verify the data in the output file - too many changing rows in output to do a cmp file so verify some of the key lines
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalEchoBytes.ToString() + ") have been received", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "All rounds complete (" + messagesSent.ToString() + " messages sent)", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "round #" + numRounds.ToString(), 1, false, testName, true);

            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "[IC]", 0, true, testName, false, false);  // shouldn't be any IC comments
            if (pass == true)
            {
                Assert.Fail("<JS_PTI_HostingModeSeparateStartPTIFirst_Test> There shouldn't be any Imm Coord messages in output since separate process");
            }
        }

    }
}

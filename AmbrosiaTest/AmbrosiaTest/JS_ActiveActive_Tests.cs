﻿using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AmbrosiaTest
{
    [TestClass]
    public class JS_ActiveActive_Tests
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
            JSUtils.JS_TestCleanup_ActiveActive();
        }

        //****************************
        // The basic test of Active Active where kill primary server
        // 1 client 
        // 3 servers - primary, checkpointing secondary and active secondary (becomes primary)
        //
        // killing first server (primary) will then have active secondary become primary
        // also restarting first server will make it the new active secondary
        //  
        //****************************
        [TestMethod]
        public void JS_PTI_ActiveActive_KillPrimary_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 4;
            long totalBytes = 4294967296;
            long totalEchoBytes = 4294967296;
            int bytesPerRound = 0;
            int maxMessageSize = 0; 
            int batchSizeCutoff = 0;
            bool fixedMsgSize = false;
            bool bidi = false;

            // Various strings used to verify servers are who they should be
            string becomingPrimary = "Becoming primary";
            string nowPrimary = "NOW I'm Primary";
            string iMChkPointer = " I'm a checkpointer";
            string iMSecondary = "I'm a secondary";

            string logTriggerSize = "256";

            string testName = "jsptiactiveactivekillprimary";
            string clientInstanceName = testName + "client";
            string serverInstanceName = testName + "server";

            string logOutputClientFileName_TestApp = testName + "Client_TestApp.log";
            string logOutputServerPrimaryFileName_TestApp = testName + "ServerPrimary_TestApp.log";
            string logOutputServerRestartPrimaryFileName_TestApp = testName + "ServerRestartPrimary_TestApp.log";
            string logOutputServerChkPtFileName_TestApp = testName + "ServerChkPt_TestApp.log";
            string logOutputServerSecondaryFileName_TestApp = testName + "ServerSecondary_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName, JSUtils.JSPTI_CombinedInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);

            // Start Client
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "4110", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "4000", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "4001", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ClientInstanceRole);
            int clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, fixedMsgSize, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Start Primary
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "0", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "1510", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "1500", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "1501", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            int serverPrimaryProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerPrimaryFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Give it an extra second to start
            MyUtils.TestDelay(1000);

            // Start Check Pointer
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "1", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "2110", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "2000", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "2001", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            int serverChkPtProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerChkPtFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Start Secondary
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "2", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "3110", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "3000", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "3001", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            int serverSecondaryProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerSecondaryFileName_TestApp, 0, fixedMsgSize, JSUtils.JSPTI_ServerInstanceRole);

            // Verify Servers are who they should be by checking output strings
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerChkPtFileName_TestApp, iMChkPointer, 5, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerPrimaryFileName_TestApp, becomingPrimary, 5, false, testName, true, false); // when shows it is primary, then check others
            pass = MyUtils.WaitForProcessToFinish(logOutputServerSecondaryFileName_TestApp, iMSecondary, 1, false, testName, true, false);

            // Kill primary
            MyUtils.KillProcess(serverPrimaryProcessID);

            // Verify Secondary becomese primary
            pass = MyUtils.WaitForProcessToFinish(logOutputServerSecondaryFileName_TestApp, nowPrimary, 3, false, testName, true, false);

            // Restart the primary server just to verify a new started one will become active secondary now - do not auto register as already registered
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "false", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "0", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "1610", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "1600", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "1601", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            int serverRestartPrimaryProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerRestartPrimaryFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Verify restarted becomes secondary
            pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartPrimaryFileName_TestApp, iMSecondary, 1, false, testName, true, false);

            // Verify the data in the restarted output file
            pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartPrimaryFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 5, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerSecondaryFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 5, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerChkPtFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 5, false, testName, true);

            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "All rounds complete", 5, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "[IC] Connected!", 5, false, testName, true, false);

            // Verify integrity of Ambrosia logs by replaying 
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_CombinedInstanceRole);  // Set combined role to ActiveActive so can verify log
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, "", JSUtils.JSPTI_ServerInstanceRole);
        }

        //****************************
        // The basic test of Active Active where kill primary server
        // 1 client 
        // 3 servers - primary, checkpointing secondary and active secondary (becomes primary)
        //
        // killing first server (primary) will then have active secondary become primary
        // also restarting first server will make it the new active secondary
        //  
        //****************************
        [TestMethod]
        public void JS_PTI_ActiveActive_KillPrimary_BiDi_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 4;
            long totalBytes = 4294967296;
            long totalEchoBytes = 4294967296;
            int bytesPerRound = 0;
            int maxMessageSize = 0; 
            int batchSizeCutoff = 0;
            bool fixedMsgSize = false; 
            bool bidi = true;

            // Various strings used to verify servers are who they should be
            string becomingPrimary = "Becoming primary";
            string nowPrimary = "NOW I'm Primary";
            string iMChkPointer = " I'm a checkpointer";
            string iMSecondary = "I'm a secondary";

            string testName = "jsptiactiveactivekillprimarybidi";
            string clientInstanceName = testName + "client";
            string serverInstanceName = testName + "server";

            string logTriggerSize = "256";

            string logOutputClientFileName_TestApp = testName + "Client_TestApp.log";
            string logOutputServerPrimaryFileName_TestApp = testName + "ServerPrimary_TestApp.log";
            string logOutputServerRestartPrimaryFileName_TestApp = testName + "ServerRestartPrimary_TestApp.log";
            string logOutputServerChkPtFileName_TestApp = testName + "ServerChkPt_TestApp.log";
            string logOutputServerSecondaryFileName_TestApp = testName + "ServerSecondary_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName, JSUtils.JSPTI_CombinedInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);

            // Start Client
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "4110", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "4000", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "4001", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ClientInstanceRole);
            int clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, fixedMsgSize, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Start Primary
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "0", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "1510", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "1500", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "1501", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            int serverPrimaryProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerPrimaryFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Give it an extra second to start
            MyUtils.TestDelay(1000);

            // Start Check Pointer
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "1", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "2110", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "2000", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "2001", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            int serverChkPtProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerChkPtFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Give it an extra second to start
            MyUtils.TestDelay(1000);

            // Start Secondary
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "2", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "3110", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "3000", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "3001", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            int serverSecondaryProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerSecondaryFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Verify Servers are who they should be by checking output strings
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerChkPtFileName_TestApp, iMChkPointer, 5, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerPrimaryFileName_TestApp, becomingPrimary, 5, false, testName, true, false); // when shows it is primary, then check others
            pass = MyUtils.WaitForProcessToFinish(logOutputServerSecondaryFileName_TestApp, iMSecondary, 1, false, testName, true, false);

            // Kill primary
            MyUtils.KillProcess(serverPrimaryProcessID);

            // Verify Secondary becomese primary
            pass = MyUtils.WaitForProcessToFinish(logOutputServerSecondaryFileName_TestApp, nowPrimary, 3, false, testName, true, false);

            // Restart the primary server just to verify a new started one will become active secondary now - do not auto register as already registered
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "false", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "0", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "1610", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "1600", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "1601", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            int serverRestartPrimaryProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerRestartPrimaryFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Verify restarted becomes secondary
            pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartPrimaryFileName_TestApp, iMSecondary, 1, false, testName, true, false);

            // Verify the data in the restarted output file
            pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartPrimaryFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 5, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerSecondaryFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 5, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerChkPtFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 5, false, testName, true);

            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalBytes.ToString() + ") have been received", 5, false, testName, true,false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "All rounds complete", 5, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "[IC] Connected!", 1, false, testName, true, false);

            // Verify integrity of Ambrosia logs by replaying 
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_CombinedInstanceRole);  // Set combined role to ActiveActive so can verify log
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, "", JSUtils.JSPTI_ServerInstanceRole);
        }


        //****************************
        // The basic test of Active Active but killing active secondary
        // 1 client 
        // 3 servers - primary, checkpointing secondary and active secondary (can become primary)
        //
        // killing secondary leaves without secondary, so others should work ok ... 
        // when a new instance is started, that new one will be the secondary
        //  
        //****************************
        [TestMethod]
        public void JS_PTI_ActiveActive_KillSecondary_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 4;
            long totalBytes = 4294967296;
            long totalEchoBytes = 4294967296;
            int bytesPerRound = 0;
            int maxMessageSize = 0;
            int batchSizeCutoff = 0;
            bool fixedMsgSize = false;
            bool bidi = false;

            // Various strings used to verify servers are who they should be
            string becomingPrimary = "Becoming primary";
            string iMChkPointer = " I'm a checkpointer";
            string iMSecondary = "I'm a secondary";

            string logTriggerSize = "256";

            string testName = "jsptiactiveactivekillsecondary";
            string clientInstanceName = testName + "client";
            string serverInstanceName = testName + "server";

            string logOutputClientFileName_TestApp = testName + "Client_TestApp.log";
            string logOutputServerPrimaryFileName_TestApp = testName + "ServerPrimary_TestApp.log";
            string logOutputServerRestartSecondaryFileName_TestApp = testName + "ServerRestartSecondary_TestApp.log";
            string logOutputServerChkPtFileName_TestApp = testName + "ServerChkPt_TestApp.log";
            string logOutputServerSecondaryFileName_TestApp = testName + "ServerSecondary_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName, JSUtils.JSPTI_CombinedInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);

            // Start Client
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "4110", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "4000", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "4001", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ClientInstanceRole);
            int clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, fixedMsgSize, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Start Primary
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "0", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "1510", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "1500", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "1501", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            int serverPrimaryProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerPrimaryFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Give it an extra second to start
            MyUtils.TestDelay(1000);

            // Start Check Pointer
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "1", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "2110", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "2000", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "2001", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            int serverChkPtProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerChkPtFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Give it an extra second to start
            MyUtils.TestDelay(1000);

            // Start Secondary
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "2", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "3110", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "3000", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "3001", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            int serverSecondaryProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerSecondaryFileName_TestApp, 0, fixedMsgSize, JSUtils.JSPTI_ServerInstanceRole);

            MyUtils.TestDelay(1000);

            // Verify Servers are who they should be by checking output strings
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerChkPtFileName_TestApp, iMChkPointer, 5, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerPrimaryFileName_TestApp, becomingPrimary, 5, false, testName, true, false); // when shows it is primary, then check others

            // Kill secondary
            MyUtils.KillProcess(serverSecondaryProcessID);

            // Restart the secondary server just to verify a new started one will become active secondary now - do not auto register as already registered
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "false", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "2", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "5210", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "5200", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "5201", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            int serverRestartPrimaryProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerRestartSecondaryFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Verify restarted becomes secondary
            pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartSecondaryFileName_TestApp, iMSecondary, 1, false, testName, true, false);

            // Verify the data in the restarted output file
            pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartSecondaryFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 5, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerPrimaryFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 5, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerChkPtFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 5, false, testName, true);

            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "All rounds complete", 5, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "[IC] Connected!", 5, false, testName, true, false);

            // Verify integrity of Ambrosia logs by replaying 
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_CombinedInstanceRole);  // Set combined role to ActiveActive so can verify log
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, "", JSUtils.JSPTI_ServerInstanceRole);
        }


        //****************************
        // The basic test of Active Active but killing active secondary (BiDirectional test)
        // 1 client 
        // 3 servers - primary, checkpointing secondary and active secondary (can become primary)
        //
        // killing secondary leaves without secondary, so others should work ok ... 
        // when a new instance is started, that new one will be the secondary
        //  
        //****************************
        [TestMethod]
        public void JS_PTI_ActiveActive_KillSecondary_BiDi_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 4;
            long totalBytes = 4294967296;
            long totalEchoBytes = 4294967296;
            int bytesPerRound = 0;
            int maxMessageSize = 0;
            int batchSizeCutoff = 0;
            bool fixedMsgSize = false;
            bool bidi = true;

            // Various strings used to verify servers are who they should be
            string becomingPrimary = "Becoming primary";
            string iMChkPointer = " I'm a checkpointer";
            string iMSecondary = "I'm a secondary";

            string logTriggerSize = "256";

            string testName = "jsptiactiveactivekillsecondarybidi";
            string clientInstanceName = testName + "client";
            string serverInstanceName = testName + "server";

            string logOutputClientFileName_TestApp = testName + "Client_TestApp.log";
            string logOutputServerPrimaryFileName_TestApp = testName + "ServerPrimary_TestApp.log";
            string logOutputServerRestartSecondaryFileName_TestApp = testName + "ServerRestartSecondary_TestApp.log";
            string logOutputServerChkPtFileName_TestApp = testName + "ServerChkPt_TestApp.log";
            string logOutputServerSecondaryFileName_TestApp = testName + "ServerSecondary_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName, JSUtils.JSPTI_CombinedInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);

            // Start Client
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "4110", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "4000", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "4001", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ClientInstanceRole);
            int clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, fixedMsgSize, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Start Primary
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "0", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "1510", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "1500", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "1501", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            int serverPrimaryProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerPrimaryFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Give it an extra second to start
            MyUtils.TestDelay(1000);

            // Start Check Pointer
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "1", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "2110", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "2000", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "2001", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            int serverChkPtProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerChkPtFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Give it an extra second to start
            MyUtils.TestDelay(1000);

            // Start Secondary
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "2", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "3110", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "3000", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "3001", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            int serverSecondaryProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerSecondaryFileName_TestApp, 0, fixedMsgSize, JSUtils.JSPTI_ServerInstanceRole);

            // Verify Servers are who they should be by checking output strings
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerChkPtFileName_TestApp, iMChkPointer, 5, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerPrimaryFileName_TestApp, becomingPrimary, 5, false, testName, true, false); // when shows it is primary, then check others
            //pass = MyUtils.WaitForProcessToFinish(logOutputServerSecondaryFileName_TestApp, iMSecondary, 1, false, testName, true, false);

            // Kill secondary
            MyUtils.KillProcess(serverSecondaryProcessID);

            // Restart the secondary server just to verify a new started one will become active secondary now - do not auto register as already registered
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "false", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "2", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "5315", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "5305", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "5306", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            int serverRestartPrimaryProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerRestartSecondaryFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Verify restarted becomes secondary
            pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartSecondaryFileName_TestApp, iMSecondary, 1, false, testName, true, false);

            // Verify the data in the restarted output file
            pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartSecondaryFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 5, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerPrimaryFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 5, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerChkPtFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 5, false, testName, true);

            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalBytes.ToString() + ") have been received", 5, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "All rounds complete", 5, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "[IC] Connected!", 1, false, testName, true, false);

            // Verify integrity of Ambrosia logs by replaying 
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_CombinedInstanceRole);  // Set combined role to ActiveActive so can verify log
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, "", JSUtils.JSPTI_ServerInstanceRole);
        }


        //****************************
        // More complex test of Active Active for client and server
        // 3 clients - primary, checkpointing secondary and active secondary 
        // 3 servers - primary, checkpointing secondary and active secondary
        //
        // Kill primary of client and primary of server and then restart both
        //  
        //****************************
        [TestMethod]
        public void JS_PTI_ActiveActive_KillClientAndServer_Test()
        {

            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 4;
            long totalBytes = 4294967296;
            long totalEchoBytes = 4294967296;
            int bytesPerRound = 0;
            int maxMessageSize = 0;
            int batchSizeCutoff = 0;
            bool fixedMsgSize = false;
            bool bidi = false;
            string logTriggerSize = "256";

            // Various strings used to verify servers are who they should be
            string nowPrimary = "NOW I'm Primary";
            string becomingPrimary = "Becoming primary";
            string iMChkPointer = " I'm a checkpointer";
            string iMSecondary = "I'm a secondary";
            string testName = "jsptiactiveactivekillclientserver";
            string clientInstanceName = testName + "client";
            string serverInstanceName = testName + "server";

            string logOutputClientPrimaryFileName_TestApp = testName + "ClientPrimary_TestApp.log";
            string logOutputClientChkPtFileName_TestApp = testName + "ClientChkPt_TestApp.log";
            string logOutputClientRestartPrimaryFileName_TestApp = testName + "ClientRestartPrimary_TestApp.log";
            string logOutputClientSecondaryFileName_TestApp = testName + "ClientSecondary_TestApp.log";
            string logOutputServerPrimaryFileName_TestApp = testName + "ServerPrimary_TestApp.log";
            string logOutputServerRestartPrimaryFileName_TestApp = testName + "ServerRestartPrimary_TestApp.log";
            string logOutputServerChkPtFileName_TestApp = testName + "ServerChkPt_TestApp.log";
            string logOutputServerSecondaryFileName_TestApp = testName + "ServerSecondary_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName, JSUtils.JSPTI_CombinedInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);

            // Start Client Primary 
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "0", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "4110", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "4000", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "4001", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ClientInstanceRole);
            int clientPrimaryProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientPrimaryFileName_TestApp, 0, fixedMsgSize, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Give it an extra second to start
            MyUtils.TestDelay(1000);

            // Start Client Check Pointer 
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "1", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "5110", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "5000", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "5001", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ClientInstanceRole);
            int clientChkPtrProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientChkPtFileName_TestApp, 0, fixedMsgSize, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Give it an extra second to start
            MyUtils.TestDelay(1000);

            // Start Client Check Secondary 
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "2", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "6110", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "6000", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "6001", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ClientInstanceRole);
            int clientSecondaryProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientSecondaryFileName_TestApp, 0, fixedMsgSize, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Start Server Primary
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "0", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "1510", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "1500", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "1501", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            int serverPrimaryProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerPrimaryFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Give it an extra second to start
            MyUtils.TestDelay(1000);

            // Start Server Check Pointer
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "1", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "2110", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "2000", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "2001", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            int serverChkPtProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerChkPtFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Give it an extra second to start
            MyUtils.TestDelay(1000);

            // Start Server Secondary
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "2", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "3110", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "3000", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "3001", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            int serverSecondaryProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerSecondaryFileName_TestApp, 0, fixedMsgSize, JSUtils.JSPTI_ServerInstanceRole);

            // Verify Servers are who they should be by checking output strings
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerChkPtFileName_TestApp, iMChkPointer, 10, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerPrimaryFileName_TestApp, becomingPrimary, 5, false, testName, true, false); 
            pass = MyUtils.WaitForProcessToFinish(logOutputServerSecondaryFileName_TestApp, iMSecondary, 1, false, testName, true, false);

            // Verify Clients are properly setup
            pass = MyUtils.WaitForProcessToFinish(logOutputClientChkPtFileName_TestApp, iMChkPointer, 5, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientPrimaryFileName_TestApp, becomingPrimary, 5, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientSecondaryFileName_TestApp, iMSecondary, 1, false, testName, true, false);

            // Kill Server Primary 
            MyUtils.KillProcess(serverPrimaryProcessID);

            // Kill Client Primary 
            MyUtils.KillProcess(clientPrimaryProcessID);

            // Verify Secondary becomese primary
            pass = MyUtils.WaitForProcessToFinish(logOutputServerSecondaryFileName_TestApp, nowPrimary, 3, false, testName, true, false);

            // Restart the primary server just to verify a new started one will become active secondary now - do not auto register as already registered
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "false", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "0", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "1610", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "1600", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "1601", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            int serverRestartPrimaryProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerRestartPrimaryFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Restart Primary Client
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "false", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "0", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "4210", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "4200", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "4201", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ClientInstanceRole);
            int clientRestartPrimaryProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientRestartPrimaryFileName_TestApp, 0, fixedMsgSize, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Verify Server output files
            pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartPrimaryFileName_TestApp, iMSecondary, 5, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartPrimaryFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 5, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerSecondaryFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 2, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerChkPtFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 2, false, testName, true);

            // Verify Client output files
            pass = MyUtils.WaitForProcessToFinish(logOutputClientRestartPrimaryFileName_TestApp, iMSecondary, 5, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientRestartPrimaryFileName_TestApp, "All rounds complete", 5, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientChkPtFileName_TestApp, "All rounds complete", 2, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientSecondaryFileName_TestApp, "All rounds complete", 2, false, testName, true, false);

            // Verify integrity of Ambrosia logs by replaying 
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_CombinedInstanceRole);  // Set combined role to ActiveActive so can verify log
            JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, "", JSUtils.JSPTI_ServerInstanceRole);
        }


        //****************************
        // Upgrading a server that is part of an Active Active Set up
        //
        // client - primary, checkpointing secondary and active secondary 
        // 3 servers - primary, checkpointing secondary and active secondary
        //
        // Kill rimary of server and then restart
        //  
        //****************************
        [TestMethod]
        public void JS_PTI_UpgradeActiveActivePrimaryOnly_Test()
        {

            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 4;
            long totalBytes = 4294967296;
            long totalEchoBytes = 4294967296;
            int bytesPerRound = 0;
            int maxMessageSize = 0;
            int batchSizeCutoff = 0;
            bool fixedMsgSize = false;
            bool bidi = false;
            string logTriggerSize = "256";

            // Various strings used to verify servers are who they should be
            string becomingPrimary = "Becoming primary";
            string iMChkPointer = " I'm a checkpointer";
            string iMSecondary = "I'm a secondary";

            string testName = "jsptiupgradeactiveactiveprimary";
            string clientInstanceName = testName + "client";
            string serverInstanceName = testName + "server";

            string logOutputClientFileName_TestApp = testName + "Client_TestApp.log";
            string logOutputServerPrimaryFileName_TestApp = testName + "ServerPrimary_TestApp.log";
            string logOutputServerUpgradedPrimaryFileName_TestApp = testName + "ServerUpgradedPrimary_TestApp.log";
            string logOutputServerChkPtFileName_TestApp = testName + "ServerChkPt_TestApp.log";
            string logOutputServerSecondaryFileName_TestApp = testName + "ServerSecondary_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName, JSUtils.JSPTI_CombinedInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);

            // Start Client
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "4110", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "4000", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "4001", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_LBOpt_deleteLogs, "false", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_activeCode, "VCurrent", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_appVersion, "0", JSUtils.JSPTI_ClientInstanceRole);
            int clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, fixedMsgSize, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Start Primary
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "0", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "1510", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "1500", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "1501", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_LBOpt_deleteLogs, "false", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_activeCode, "VCurrent", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_appVersion, "0", JSUtils.JSPTI_ServerInstanceRole);
            int serverPrimaryProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerPrimaryFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Give it an extra second to start
            MyUtils.TestDelay(1000);

            // Start Check Pointer
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "1", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "2110", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "2000", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "2001", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_LBOpt_deleteLogs, "false", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_activeCode, "VCurrent", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_appVersion, "0", JSUtils.JSPTI_ServerInstanceRole);
            int serverChkPtProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerChkPtFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Start Secondary
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "2", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "6110", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "6000", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "6001", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_LBOpt_deleteLogs, "false", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_activeCode, "VCurrent", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_appVersion, "0", JSUtils.JSPTI_ServerInstanceRole);
            int serverSecondaryProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerSecondaryFileName_TestApp, 0, fixedMsgSize, JSUtils.JSPTI_ServerInstanceRole);

            // Verify Servers are who they should be by checking output strings
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerChkPtFileName_TestApp, iMChkPointer, 5, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerPrimaryFileName_TestApp, becomingPrimary, 5, false, testName, true, false); // when shows it is primary, then check others
            pass = MyUtils.WaitForProcessToFinish(logOutputServerSecondaryFileName_TestApp, iMSecondary, 1, false, testName, true, false);

            // Kill server
            MyUtils.KillProcess(serverPrimaryProcessID);

            //Set the Upgrade Version for primary and start it 
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "false", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "0", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "1610", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "1000", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "1001", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_upgradeVersion, "120", JSUtils.JSPTI_ServerInstanceRole);

            // Restart the server and make sure it continues
            int serverRestartedProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerUpgradedPrimaryFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Because checkpointer and secondary were not upgraded so they were stopped which means nothing to take the checkpoint or be secondary

            // Verify the data in the restarted output file
            pass = MyUtils.WaitForProcessToFinish(logOutputServerUpgradedPrimaryFileName_TestApp, "Bytes received: " + totalBytes.ToString(), 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputServerUpgradedPrimaryFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 1, false, testName, true);

            // Verify that echo is NOT part of the output - won't pop assert on fail so check return value
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "All rounds complete", 1, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "[IC] Connected!", 1, false, testName, true, false);

            // This proves that the upgrade worked
            pass = MyUtils.WaitForProcessToFinish(logOutputServerUpgradedPrimaryFileName_TestApp, "Upgrade of state and code complete", 1, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerUpgradedPrimaryFileName_TestApp, "VNext:", 1, false, testName, true);
        }






        //*#*#*#*#*#*#
        /*
        //****************************
        // Debugging fail in checkpointer where get this error in check pointer
        // [91m2021/11/02 13:59:11.101: Uncaught Exception: Error: The supplied varInt32 value(170 170 170 170 170 138) encodes a value larger than 32-bits
        //
        //  *##* NOTE - Remove these from the Clean up
        //  
        //****************************
        [TestMethod]
        public void JS_PTI_ActiveActive_DEBUG_KillPrimary_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();


            int numRounds = 4;
            long totalBytes = 4294967296;
            long totalEchoBytes = 4294967296;
            int bytesPerRound = 0;
            int maxMessageSize = 0;  //*#*#*#* Default to 0 --- try -mms=32768 (crash) and try again (still with -fms). If that doesn’t repro, try -mms=16384, then -mms=8192 
            int batchSizeCutoff = 0;
            bool fixedMsgSize = true;  
            bool bidi = false;
            string logTriggerSize = "256";

            // Various strings used to verify servers are who they should be
            string becomingPrimary = "Becoming primary";
            string nowPrimary = "NOW I'm Primary";
            string iMChkPointer = " I'm a checkpointer";
            string iMSecondary = "I'm a secondary";

            string testName = "jsptidebug";
            string clientInstanceName = testName + "client";
            string serverInstanceName = testName + "server";

            string portExtension = "292";  

            string clientCRAPort = "25"+ portExtension;   // 51
            string clientReceivePort = "26"+ portExtension;
            string clientSendPort = "27"+ portExtension;

            string primaryCRAPort = "15"+ portExtension; //54
            string primaryReceivePort = "16"+ portExtension;
            string primarySendPort = "17"+ portExtension;

            string checkptrCRAPort = "21"+ portExtension; //57
            string checkptrReceivePort = "22"+ portExtension;
            string checkptrSendPort = "23"+ portExtension;

            string logOutputClientFileName_TestApp = testName + "Client_TestApp.log";
            string logOutputServerPrimaryFileName_TestApp = testName + "ServerPrimary_TestApp.log";
            //*#*#*#  string logOutputServerRestartPrimaryFileName_TestApp = testName + "ServerRestartPrimary_TestApp.log";
            string logOutputServerChkPtFileName_TestApp = testName + "ServerChkPt_TestApp.log";
            string logOutputServerSecondaryFileName_TestApp = testName + "ServerSecondary_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName, JSUtils.JSPTI_CombinedInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);

            // Start Client
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, clientCRAPort, JSUtils.JSPTI_ClientInstanceRole);  //2510
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, clientReceivePort, JSUtils.JSPTI_ClientInstanceRole); //2500
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, clientSendPort, JSUtils.JSPTI_ClientInstanceRole); //2501
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ClientInstanceRole);
            int clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, fixedMsgSize, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Start Primary
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "0", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, primaryCRAPort, JSUtils.JSPTI_ServerInstanceRole); //1510
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, primaryReceivePort, JSUtils.JSPTI_ServerInstanceRole); //1500
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, primarySendPort, JSUtils.JSPTI_ServerInstanceRole); //1501
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            int serverPrimaryProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerPrimaryFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Start Check Pointer
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "1", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, checkptrCRAPort, JSUtils.JSPTI_ServerInstanceRole); //2110
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, checkptrReceivePort, JSUtils.JSPTI_ServerInstanceRole); //2000
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, checkptrSendPort, JSUtils.JSPTI_ServerInstanceRole); //2001
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            int serverChkPtProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerChkPtFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Verify Servers are who they should be by checking output strings
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerChkPtFileName_TestApp, iMChkPointer, 5, false, testName, true, false);
            //#*#*# pass = MyUtils.WaitForProcessToFinish(logOutputServerPrimaryFileName_TestApp, becomingPrimary, 5, false, testName, true, false); // when shows it is primary, then check others
            //#*#*#  pass = MyUtils.WaitForProcessToFinish(logOutputServerSecondaryFileName_TestApp, iMSecondary, 1, false, testName, true, false);

            // Verify the data in the restarted output file
            //*#*#*# pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartPrimaryFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 5, false, testName, true);
            //*#*#  pass = MyUtils.WaitForProcessToFinish(logOutputServerSecondaryFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 5, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerPrimaryFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 3, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerChkPtFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 3, false, testName, true);



            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "All rounds complete", 3, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "[IC] Connected!", 3, false, testName, true, false);

            // Verify integrity of Ambrosia logs by replaying 
            //*#*#*# JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_CombinedInstanceRole);  // Set combined role to ActiveActive so can verify log
            //*#*#*# JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, "", JSUtils.JSPTI_ServerInstanceRole);
        }



        //****************************
        // 
        // Debugging fail in checkpointer where get this error in check pointer
        // [91m2021/11/02 13:59:11.101: Uncaught Exception: Error: The supplied varInt32 value(170 170 170 170 170 138) encodes a value larger than 32-bits
        //  
        //****************************
        [TestMethod]
        public void JS_PTI_ActiveActive_DEBUG_KillPrimary_BiDi_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            int numRounds = 4;
            long totalBytes = 4294967296;
            long totalEchoBytes = 4294967296;
            int bytesPerRound = 0;
            int maxMessageSize = 0;  //*#*#*#* Default to 0 --- try -mms=32768 and try again (still with -fms). If that doesn’t repro, try -mms=16384, then -mms=8192 ;
            int batchSizeCutoff = 0;
            bool fixedMsgSize = true;
            bool bidi = false;
            string logTriggerSize = "256";

            // Various strings used to verify servers are who they should be
            string becomingPrimary = "Becoming primary";
            string nowPrimary = "NOW I'm Primary";
            string iMChkPointer = " I'm a checkpointer";
            string iMSecondary = "I'm a secondary";

            string testName = "jsptidebugbidi";
            string clientInstanceName = testName + "client";
            string serverInstanceName = testName + "server";

            string logOutputClientFileName_TestApp = testName + "Client_TestApp.log";
            string logOutputServerPrimaryFileName_TestApp = testName + "ServerPrimary_TestApp.log";
            string logOutputServerRestartPrimaryFileName_TestApp = testName + "ServerRestartPrimary_TestApp.log";
            string logOutputServerChkPtFileName_TestApp = testName + "ServerChkPt_TestApp.log";
            string logOutputServerSecondaryFileName_TestApp = testName + "ServerSecondary_TestApp.log";

            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName, JSUtils.JSPTI_CombinedInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);



            //*#*#* Allows for TTD on all the individual log files
            //*#*#* NOTE -- make sure comment out Init so doesn't delete files
            //*#*#* Put JS Utilities back where it just always takes the first 
            testName = "jsptidebug";
            bidi = false;
            for (int i = 1; i <= 16; i++)
            {
                JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_CombinedInstanceRole);  // Set combined role to ActiveActive so can verify log
                JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, "", JSUtils.JSPTI_ServerInstanceRole, "", i);
                MyUtils.KillProcessByName("node");
            }

                Assert.Fail("STOP");
            //*#*#*# 


            // Start Client
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, clientInstanceName, JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "2560", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "2560", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "2561", JSUtils.JSPTI_ClientInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ClientInstanceRole);
            int clientProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputClientFileName_TestApp, 0, fixedMsgSize, JSUtils.JSPTI_ClientInstanceRole, serverInstanceName);

            // Start Primary
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "0", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "1560", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "1560", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "1561", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            int serverPrimaryProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerPrimaryFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Start Check Pointer
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "1", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "2160", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "2060", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "2061", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            int serverChkPtProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerChkPtFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Start Secondary
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "2", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "3160", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "3060", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "3061", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_logTriggerSizeinMB, logTriggerSize, JSUtils.JSPTI_ServerInstanceRole);
            //*#*# int serverSecondaryProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerSecondaryFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Verify Servers are who they should be by checking output strings
            bool pass = MyUtils.WaitForProcessToFinish(logOutputServerChkPtFileName_TestApp, iMChkPointer, 5, false, testName, true, false);
            //*#*##pass = MyUtils.WaitForProcessToFinish(logOutputServerPrimaryFileName_TestApp, becomingPrimary, 5, false, testName, true, false); // when shows it is primary, then check others

            //#*#*#             pass = MyUtils.WaitForProcessToFinish(logOutputServerSecondaryFileName_TestApp, iMSecondary, 1, false, testName, true, false);

            // Kill primary
            //*#*#  MyUtils.KillProcess(serverPrimaryProcessID);

            // Verify Secondary becomese primary
            //#*#*#            pass = MyUtils.WaitForProcessToFinish(logOutputServerSecondaryFileName_TestApp, nowPrimary, 3, false, testName, true, false);

            // Restart the primary server just to verify a new started one will become active secondary now - do not auto register as already registered
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, serverInstanceName, JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_autoRegister, "false", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_replicaNumber, "0", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icCraPort, "1110", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icReceivePort, "1000", JSUtils.JSPTI_ServerInstanceRole);
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_icSendPort, "1001", JSUtils.JSPTI_ServerInstanceRole);
            //*#*#*#  int serverRestartPrimaryProcessID = JSUtils.StartJSPTI(numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, logOutputServerRestartPrimaryFileName_TestApp, 0, false, JSUtils.JSPTI_ServerInstanceRole);

            // Verify restarted becomes secondary
            //*#*#*# pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartPrimaryFileName_TestApp, iMSecondary, 1, false, testName, true, false);

            // Verify the data in the restarted output file
            //*#*#*# pass = MyUtils.WaitForProcessToFinish(logOutputServerRestartPrimaryFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 5, false, testName, true);
            //*#*#  pass = MyUtils.WaitForProcessToFinish(logOutputServerSecondaryFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 5, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerPrimaryFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 5, false, testName, true);
            pass = MyUtils.WaitForProcessToFinish(logOutputServerChkPtFileName_TestApp, "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received", 5, false, testName, true);


            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "SUCCESS: The expected number of echoed bytes (" + totalBytes.ToString() + ") have been received", 5, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "All rounds complete", 5, false, testName, true, false);
            pass = MyUtils.WaitForProcessToFinish(logOutputClientFileName_TestApp, "[IC] Connected!", 1, false, testName, true, false);

            // Verify integrity of Ambrosia logs by replaying 
            //*#*#*# JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_isActiveActive, "true", JSUtils.JSPTI_CombinedInstanceRole);  // Set combined role to ActiveActive so can verify log
            //*#*#*# JSUtils.JS_VerifyTimeTravelDebugging(testName, numRounds, totalBytes, totalEchoBytes, bytesPerRound, maxMessageSize, batchSizeCutoff, bidi, true, "", JSUtils.JSPTI_ServerInstanceRole);
        }

        */
        //*#*#*#*#*#*#
    }
}

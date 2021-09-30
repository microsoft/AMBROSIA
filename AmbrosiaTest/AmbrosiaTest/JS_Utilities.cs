using System;
using System.Diagnostics;
using System.Configuration;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Windows.Forms; // need this to handle threading issue on sleeps
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AmbrosiaTest
{

    public class JS_Utilities
    {
        // Message at the bottom of the output file to show everything passed
        public string CodeGenSuccessMessage = "Code file generation SUCCEEDED: 2 of 2 files generated; 0 TypeScript errors, 0 merge conflicts";
        public string CodeGenFailMessage = "Code file generation FAILED: 0 of 2 files generated";
        public string CodeGenNoTypeScriptErrorsMessage = "Success: No TypeScript errors found in generated file ";

        public string JSPTI_CombinedInstanceRole = "Combined";
        public string JSPTI_ClientInstanceRole = "Client";
        public string JSPTI_ServerInstanceRole = "Server";

        //** Config Settings in ambrosiaConfig.json
        public string JSConfig_autoRegister = "autoRegister";
        public string JSConfig_instanceName = "instanceName";
        public string JSConfig_icCraPort = "icCraPort";
        public string JSConfig_icReceivePort = "icReceivePort";
        public string JSConfig_icSendPort = "icSendPort";
        public string JSConfig_icLogFolder = "icLogFolder";
        public string JSConfig_icBinFolder = "icBinFolder";
        public string JSConfig_useNetCore = "useNetCore";
        public string JSConfig_logTriggerSizeinMB = "logTriggerSizeInMB";
        public string JSConfig_debugStartCheckpoint = "debugStartCheckpoint";
        public string JSConfig_debugTestUpgrade = "debugTestUpgrade";
        public string JSConfig_appVersion = "appVersion";
        public string JSConfig_activeCode = "activeCode";
        public string JSConfig_upgradeVersion = "upgradeVersion";
        public string JSConfig_icLogStorageType = "icLogStorageType";
        public string JSConfig_isActiveActive = "isActiveActive";
        public string JSConfig_replicaNumber = "replicaNumber";
        public string JSConfig_hostingMode = "icHostingMode";


        // NOTE: all lbOptions settings need "lbOptions" at beginning so know it is nested there
        public string JSConfig_LBOpt_msgQueueSize = "lbOptionsmaxMessageQueueSizeInMB";  
        public string JSConfig_LBOpt_deleteLogs = "lbOptionsdeleteLogs";

        //*********
        // LogType
        // This is type \ location of the logs.. "Files" or "Blobs" for JS
        //*********
        public string logTypeFiles = "Files";
        public string logTypeBlobs = "Blobs";


        // Runs a TS file through the JS LB and verifies code gen works correctly
        // Handles  valid tests one way, Negative tests from a different directory and Source Files as negative tests
        public void Test_CodeGen_TSFile(string TestFile, bool NegTest = false, string PrimaryErrorMessage = "", string SecondaryErrorMessage = "", bool UsingSrcTestFile = false)
        {
            try
            {

                Utilities MyUtils = new Utilities();

                // Test Name is just the file without the extension
                string TestName = TestFile.Substring(0, TestFile.Length - 3);

                // Launch the client job process with these values
                string testfileDir = @"../../AmbrosiaTest/JSTest/JS_CodeGen_TestFiles/";
                if (NegTest)
                {
                    testfileDir = @"../../AmbrosiaTest/JSTest/JS_CodeGen_TestFiles/NegativeTests/";
                }
                if (UsingSrcTestFile)
                {
                    testfileDir = @"../../AmbrosiaTest/JSTest/node_modules/ambrosia-node/src/";
                    TestName = "SRC_" + TestName;
                }

                string ConSuccessString = CodeGenNoTypeScriptErrorsMessage + TestName + "_GeneratedConsumerInterface.g.ts";
                string PubSuccessString = CodeGenNoTypeScriptErrorsMessage + TestName + "_GeneratedPublisherFramework.g.ts";
                bool pass = true;  // not actually used in this test but it is a generic utility fctn return

                string testappdir = ConfigurationManager.AppSettings["AmbrosiaJSTestDirectory"];
                string sourcefile = testfileDir + TestFile;
                string generatedfile = TestName + "_Generated";
                string fileNameExe = "node.exe";
                string argString = "out\\TestCodeGen.js sourceFile=" + sourcefile + " mergeType=None generatedFileName=" + generatedfile;
                string testOutputLogFile = TestName + "_CodeGen_Out.log";


                int processID = MyUtils.LaunchProcess(testappdir, fileNameExe, argString, false, testOutputLogFile);
                if (processID <= 0)
                {
                    MyUtils.FailureSupport("");
                    Assert.Fail("<Test_CodeGen_TSFile> JS TestApp was not started.  ProcessID <=0 ");
                }


                // Verify things differently if it is a negative test
                if (NegTest)
                {
                    pass = MyUtils.WaitForProcessToFinish(testOutputLogFile, CodeGenFailMessage, 1, false, TestFile, true,false);

                    // Verify the log file only has the one error (one that is related to not being annotated)
                    if (UsingSrcTestFile)
                    {
                        // just give a breath for file to close 
                        Thread.Sleep(500);

                        string TestLogDir = ConfigurationManager.AppSettings["TestLogOutputDirectory"];
                        string outputFile = TestLogDir + "\\" + testOutputLogFile;
                        var total = 0;

                        using (StreamReader sr = new StreamReader(outputFile))
                        {

                            while (!sr.EndOfStream)
                            {
                                var counts = sr
                                    .ReadLine()
                                    .Split(' ')
                                    .GroupBy(s => s)
                                    .Select(g => new { Word = g.Key, Count = g.Count() });
                                var wc = counts.SingleOrDefault(c => c.Word == "Error:");
                                total += (wc == null) ? 0 : wc.Count;
                            }
                        }

                        // Look for "Error:" in the log file
                        if (total > 1)
                        {
                            Assert.Fail("<Test_CodeGen_TSFile> Failure! Found more than 1 error in output file:"+ testOutputLogFile);
                        }
                    }
                }
                else
                {
                    // Wait to see if success comes shows up in log file for total and for consumer and publisher
                    pass = MyUtils.WaitForProcessToFinish(testOutputLogFile, CodeGenSuccessMessage, 1, false, TestFile, true,false);
                    pass = MyUtils.WaitForProcessToFinish(testOutputLogFile, ConSuccessString, 1, false, TestFile, true,false);
                    pass = MyUtils.WaitForProcessToFinish(testOutputLogFile, PubSuccessString, 1, false, TestFile, true,false);

                    // Verify the generated files with cmp files 
                    string GenConsumerFile = TestName + "_GeneratedConsumerInterface.g.ts";
                    string GenPublisherFile = TestName + "_GeneratedPublisherFramework.g.ts";
                    MyUtils.VerifyTestOutputFileToCmpFile(GenConsumerFile, true);
                    MyUtils.VerifyTestOutputFileToCmpFile(GenPublisherFile, true);
                }

                // Can use these to verify extra messages in the log file
                if (PrimaryErrorMessage != "")
                {
                    pass = MyUtils.WaitForProcessToFinish(testOutputLogFile, PrimaryErrorMessage, 1, false, TestFile, true,false);
                }
                if (SecondaryErrorMessage != "")
                {
                    pass = MyUtils.WaitForProcessToFinish(testOutputLogFile, SecondaryErrorMessage, 1, false, TestFile, true,false);
                }
            }
            catch (Exception e)
            {
                Assert.Fail("<Test_CodeGen_TSFile> Failure! Exception:" + e.Message);
            }
        }


        // Run JS Node Unit Tests
        public int StartJSNodeUnitTests(string testOutputLogFile)
        {
            Utilities MyUtils = new Utilities();

            // Launch the client job process with these values
            string workingDir = ConfigurationManager.AppSettings["AmbrosiaJSDirectory"] + "\\Ambrosia-Node";
            string fileNameExe = "pwsh.exe";
            string argString = "-c npm run unittests";

            int processID = MyUtils.LaunchProcess(workingDir, fileNameExe, argString, false, testOutputLogFile);
            if (processID <= 0)
            {
                MyUtils.FailureSupport("");
                Assert.Fail("<StartJSNodeUnitTests> npm unittests were not started.  ProcessID <=0 ");
            }

            // Give it a few seconds to start
            Thread.Sleep(2000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            return processID;
        }


        // Start Javascript Test App
        public int StartJSPTI(int numRounds, long totalBytes, long totalEchoBytes, int bytesPerRound, int maxMessageSize, int batchSizeCutoff, bool bidi, string testOutputLogFile, int memoryUsed = 0, bool fms = false, string instanceRole= "", string serverInstanceName = "")
        {

/*   *** For reference - PTI parameters
 
        -h | --help                    : [Common] Displays this help message
        -ir | --instanceRole =          : [Common] The role of this instance in the test('Server', 'Client', or 'Combined'); defaults to 'Combined'
        - m | --memoryUsed =             : [Common] Optional "padding"(in bytes) used to simulate large checkpoints by being included in app state; defaults to 0
        - c | --autoContinue =           : [Common] Whether to continue automatically at startup(if true), or wait for the 'Enter' key(if false) ; defaults to true
        - sin | --serverInstanceName =   : [Client] The name of the instance that's acting in the 'Server' role for the test; only required when --role is 'Client'
        - bpr | --bytesPerRound =        : [Client] The total number of message payload bytes that will be sent in a single round; defaults to 1 GB
        - bsc | --batchSizeCutoff =      : [Client] Once the total number of message payload bytes queued reaches(or exceeds) this limit, then the batch will be sent; defaults to 10 MB
        - mms | --maxMessageSize =       : [Client] The maximum size(in bytes) of the message payload; must be a power of 2(eg. 65536), and be at least 16; defaults to 64KB
        - n | --numOfRounds =            : [Client] The number of rounds(of size bytesPerRound) to work through; each round will use a[potentially] different message size; defaults to 1
        - nds | --noDescendingSize      : [Client] Disables descending(halving) the message size after each round; instead, a random size[power of 2] between 16 and--maxMessageSize will be used
        -fms | --fixedMessageSize      : [Client] All messages(in all rounds) will be of size --maxMessageSize; --noDescendingSize(if also supplied) will be ignored
        - eeb | --expectedEchoedBytes =  : [Client] The total number of "echoed" bytes expected to be received from the server when--bidirectional is specified; the client will report a "success" message when this number of bytes have been received
        -cin | --clientInstanceName =   : [Server] The name of the instance that's acting in the 'Client' role for the test; only required when --role is 'Server' and --bidirectional is specified
        - nhc | --noHealthCheck         : [Server] Disables the periodic server health check(requested via an Impulse message)
        -bd | --bidirectional          : [Server] Enables echoing the 'doWork' method call back to the client
        -efb | --expectedFinalBytes =   : [Server] The total number of bytes expected to be received from all clients; the server will report a "success" message when this number of bytes have been received
*/

            Utilities MyUtils = new Utilities();

            // Launch the client job process with these values
            string workingDir = ConfigurationManager.AppSettings["AmbrosiaJSTestDirectory"]+"\\PTI\\App";
            string fileNameExe = "node.exe";
            string argString = "out\\main.js";

            // Instance Role defaults to Combined
            if (instanceRole=="")
            {
                argString = argString + " -ir="+ JSPTI_CombinedInstanceRole;
            }
            else
            {
                argString = argString + " -ir=" + instanceRole;
            }

            // set the -n and -eeb parameter only for non server
            if (instanceRole != JSPTI_ServerInstanceRole)
            {
                argString = argString + " -n=" + numRounds.ToString()+" -eeb="+ totalEchoBytes.ToString();
            }

            // set the -nhc and -efb for anything that is not client (server or combined)
            if (instanceRole != JSPTI_ClientInstanceRole) 
            {
                argString = argString + " -nhc -efb=" + totalBytes.ToString();
            }

            // set the serverInstance if set (only used for client)
            if (serverInstanceName != "")
            {
                argString = argString + " -sin=" + serverInstanceName;
            }

            // Enables echoing the 'doWork' method call back to the client
            if ((bidi) && (instanceRole != JSPTI_ClientInstanceRole))
            {
                argString = argString + " -bd";
            }

            // memory used setting for checkpoint testing
            if (memoryUsed > 0 )
            {
                argString = argString + " -m="+ memoryUsed.ToString();
            }

            // Max Message Size
            if ((maxMessageSize > 0) && (instanceRole != JSPTI_ServerInstanceRole))
            {
                argString = argString + " -mms=" + maxMessageSize.ToString();
            }

            // bytes per round
            if ((bytesPerRound > 0) && (instanceRole != JSPTI_ServerInstanceRole))
            {
                argString = argString + " -bpr=" + bytesPerRound.ToString();
            }

            // batch size cutoff ... if 0 then use default
            if ((batchSizeCutoff > 0) && (instanceRole != JSPTI_ServerInstanceRole))
            {
                argString = argString + " -bsc=" + batchSizeCutoff.ToString();
            }

            // fixed message size
            if (fms)
            {
                argString = argString + " -fms";
            }

            // use client config file for client and server for server
            if (instanceRole == JSPTI_ClientInstanceRole)
            {
                argString = argString + " ambrosiaConfigFile=..\\Client\\ambrosiaConfig.json";
            }
            if (instanceRole == JSPTI_ServerInstanceRole)
            {
                argString = argString + " ambrosiaConfigFile=..\\Server\\ambrosiaConfig.json";
            }

            int processID = MyUtils.LaunchProcess(workingDir, fileNameExe, argString, false, testOutputLogFile);
            if (processID <= 0)
            {
                MyUtils.FailureSupport("");
                Assert.Fail("<StartJSTestApp> JS TestApp was not started.  ProcessID <=0 ");
            }

            // Give it a few seconds to start
            Thread.Sleep(3000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            return processID;
        }


        //** Restores the JS Config file for the test app from the golden config file
        public void JS_RestoreJSConfigFile(bool SetAutoRegister = true, string altGOLDConfigFile = "")
        {
            try
            {
                Utilities MyUtils = new Utilities();

                // ** Restore Config file from golden one
                string basePath = ConfigurationManager.AppSettings["AmbrosiaJSTestDirectory"];
                string ambrosiaGoldConfigfileName = "ambrosiaConfigGOLD.json";
                string ambrosiaConfigfileName = "ambrosiaConfig.json";

                if (altGOLDConfigFile != "")
                {
                    ambrosiaGoldConfigfileName = altGOLDConfigFile;
                 }

                //** Set defaults that are test run specific
                string CurrentFramework = MyUtils.NetFramework;
                if (MyUtils.NetFrameworkTestRun == false)
                {
                    CurrentFramework = MyUtils.NetCoreFramework;
                }

                string icBinDirectory = Directory.GetCurrentDirectory() + "\\" + CurrentFramework;
                string logDirectory = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"];

                //*** Copy from The Gold Config to App Config ***
                File.Copy(basePath + "\\" + ambrosiaGoldConfigfileName, basePath + "\\PTI\\App\\" + ambrosiaConfigfileName, true);

                // Set the defaults based on current system
                Directory.CreateDirectory(logDirectory);  // can't load JSon if the log path doesn't exist
                JS_UpdateJSConfigFile(JSConfig_autoRegister, SetAutoRegister.ToString());
                JS_UpdateJSConfigFile(JSConfig_icLogFolder, logDirectory);
                JS_UpdateJSConfigFile(JSConfig_icBinFolder, icBinDirectory);

                //*** Copy from The Gold Config to Client Config ***
                File.Copy(basePath + "\\" + ambrosiaGoldConfigfileName, basePath + "\\PTI\\Client\\" + ambrosiaConfigfileName, true);

                // Set the defaults based on current system
                JS_UpdateJSConfigFile(JSConfig_autoRegister, SetAutoRegister.ToString(), JSPTI_ClientInstanceRole);  
                JS_UpdateJSConfigFile(JSConfig_icLogFolder, logDirectory, JSPTI_ClientInstanceRole);
                JS_UpdateJSConfigFile(JSConfig_icBinFolder, icBinDirectory, JSPTI_ClientInstanceRole);
                JS_UpdateJSConfigFile(JSConfig_icCraPort, "2510", JSPTI_ClientInstanceRole);
                JS_UpdateJSConfigFile(JSConfig_icReceivePort, "2010", JSPTI_ClientInstanceRole);
                JS_UpdateJSConfigFile(JSConfig_icSendPort, "2011", JSPTI_ClientInstanceRole);

                //*** Copy from The Gold Config to Server Config ***
                File.Copy(basePath + "\\" + ambrosiaGoldConfigfileName, basePath + "\\PTI\\Server\\" + ambrosiaConfigfileName, true);

                // Set the defaults based on current system
                JS_UpdateJSConfigFile(JSConfig_autoRegister, SetAutoRegister.ToString(), JSPTI_ServerInstanceRole);  
                JS_UpdateJSConfigFile(JSConfig_icLogFolder, logDirectory, JSPTI_ServerInstanceRole);
                JS_UpdateJSConfigFile(JSConfig_icBinFolder, icBinDirectory, JSPTI_ServerInstanceRole);
                JS_UpdateJSConfigFile(JSConfig_icCraPort, "2500", JSPTI_ServerInstanceRole);
                JS_UpdateJSConfigFile(JSConfig_icReceivePort, "2000", JSPTI_ServerInstanceRole);
                JS_UpdateJSConfigFile(JSConfig_icSendPort, "2001", JSPTI_ServerInstanceRole);
            }
            catch (Exception e)
            {
                Assert.Fail("<JS_RestoreJSConfigFile> Failure! " + e.Message);
            }

        }

        //** Updates a property setting in a the JS Config File (ambrosiaConfig.json)
        //*
        //*  NOTE - if property in lbOptions section, make sure property name has lbOptions prepended to it
        //* 
        public void JS_UpdateJSConfigFile(string property, string newValue, string instanceRole= "")
        {

            try
            {
                string lbOptionsHeader = "lbOptions";
                string data = string.Empty;

                string basePath = ConfigurationManager.AppSettings["AmbrosiaJSTestDirectory"] + "\\PTI\\App";

                // change it if client or server
                if (instanceRole==JSPTI_ClientInstanceRole)
                {
                    basePath = ConfigurationManager.AppSettings["AmbrosiaJSTestDirectory"] + "\\PTI\\Client";
                }
                if (instanceRole == JSPTI_ServerInstanceRole)
                {
                    basePath = ConfigurationManager.AppSettings["AmbrosiaJSTestDirectory"] + "\\PTI\\Server";
                }

                string ambrosiaConfigfileName = "ambrosiaConfig.json";
                string ConfigFile = basePath+"\\"+ambrosiaConfigfileName;

                //** Read JSON config file
                data = File.ReadAllText(ConfigFile);
                var jo1 = JObject.Parse(data);
                var tz = jo1[property];

                // Checks if property is in nested area of lbOptions - if it is then handle it differently
                if (property.Contains(lbOptionsHeader))
                {
                    property = property.Replace(lbOptionsHeader, "");
                    tz = jo1[lbOptionsHeader][property];
                }

                var currentValue = ((Newtonsoft.Json.Linq.JValue)tz).Value;
                var typeOfCurrentValue = currentValue.GetType();
                
                // Special case where auto register is a string that is normally a boolean
                // Easier to just be creative and put this check in vs create a whole new function to handle this one off
                if (newValue == "TrueAndExit")
                {
                    typeOfCurrentValue = newValue.GetType();
                }
                ((Newtonsoft.Json.Linq.JValue)tz).Value = Convert.ChangeType(newValue, typeOfCurrentValue); 

                //** Write the key \ value 
                string dataObj = JsonConvert.SerializeObject(jo1, Formatting.Indented);
                Directory.CreateDirectory(basePath);
                File.WriteAllText(Path.Combine(basePath, ambrosiaConfigfileName), dataObj);
            }
            catch (Exception e)
            {
                Assert.Fail("<JS_UpdateJSConfigFile> Failure! " + e.Message);
            }
        }


        //*********************************************************************
        // Modeled after the C# version of "VerifyAmbrosiaLogFile & JS_VerifyTimeTravelDebugging" but this need too different to just expand those.
        //
        // Verifies the integrity of the Ambrosia for JS generated log file by doing Time Travel Debugging of the log file. 
        // Instead of using Ambrosia.exe to verify log, this uses node.exe to verify it (which calls Ambrosia.exe under the covers).
        //
        // NOTE: For JS created log files, can NOT use ambrosia.exe (with debugInstance flag) with C# PTI client / server because JS log files (like VerifyAmbrosiaLogFile)
        //  because there is different messaging in JS log files than C# generated log files. Therefore, Verify TTD is the only verification of JS log files.
        //
        // NOTE: data is too volatile for cmp file method so verify specific strings
        //*********************************************************************
        public void JS_VerifyTimeTravelDebugging(string testName, int numRounds, long totalBytes, long totalEchoBytes, int bytesPerRound, int maxMessageSize, int batchSizeCutoff, bool bidi, bool startWithFirstFile, bool checkForDoneString = true, string specialVerifyString = "", string instanceRole = "", string serverInstanceName = "")
        {

            Utilities MyUtils = new Utilities();

            string currentDir = Directory.GetCurrentDirectory();
            string bytesReceivedString = "Bytes received: " + totalBytes.ToString();
            string successString = "SUCCESS: The expected number of bytes (" + totalBytes.ToString() + ") have been received";
            string successEchoString = "SUCCESS: The expected number of echoed bytes (" + totalBytes.ToString() + ") have been received";
            string allRoundsComplete = "All rounds complete";
            string argForTTD = "Args: DebugInstance instanceName=" + testName+instanceRole.ToLower();
            string startingCheckPoint = "checkpoint="; // append the number below after calculated
            string workingDir = ConfigurationManager.AppSettings["AmbrosiaJSTestDirectory"] + "\\PTI\\App";  // defaults to Combined (app)
            string logOutputFileName_TestApp = testName + "_" + instanceRole + "_VerifyTTD.log";
            string fileNameExe = "node.exe";
            string argString = "out\\main.js";
            string strLogFileInstanceRole = "";  // app instanceRole is blank

            // Make sure all node etc are stopped 
            MyUtils.StopAllAmbrosiaProcesses();

            // Instance Role defaults to Combined
            if (instanceRole == "")
            {
                argString = argString + " -ir=" + JSPTI_CombinedInstanceRole;
            }
            else
            {
                argString = argString + " -ir=" + instanceRole;
                strLogFileInstanceRole = instanceRole;  // used in the file name of the log
            }
            string ambrosiaBaseLogDir = currentDir + "\\" + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"];  // don't put + "\\" on end as mess up location .. need append in Ambrosia call though
            string ambrosiaLogDirFromPTI = ConfigurationManager.AppSettings["TTDAmbrosiaLogDirectory"] + "\\";
            string ambServiceLogPath = ambrosiaBaseLogDir + "\\";

            // Enables echoing the 'doWork' method call back to the client
            if ((bidi) && (instanceRole != JSPTI_ClientInstanceRole))
            {
                argString = argString + " -bd";
            }

            // set the -n and -eeb parameter only for non server
            if (instanceRole != JSPTI_ServerInstanceRole)
            {
                argString = argString + " -n=" + numRounds.ToString() + " -eeb=" + totalEchoBytes.ToString();
            }

            // set the -nhc and -efb for anything that is not client (server or combined)
            if (instanceRole != JSPTI_ClientInstanceRole)
            {
                argString = argString + " -nhc -efb=" + totalBytes.ToString();
            }

            // If passing zero then just use the default value.
            // Max Message Size
            if ((maxMessageSize > 0) && (instanceRole != JSPTI_ServerInstanceRole))
            {
                argString = argString + " -mms=" + maxMessageSize.ToString();
            }

            // bytes per round
            if ((bytesPerRound > 0) && (instanceRole != JSPTI_ServerInstanceRole))
            {
                argString = argString + " -bpr=" + bytesPerRound.ToString();
            }

            // batch size cutoff ... if 0 then use default
            if ((batchSizeCutoff > 0) && (instanceRole != JSPTI_ServerInstanceRole))
            {
                argString = argString + " -bsc=" + batchSizeCutoff.ToString();
            }

            // set the serverInstance if set (only used for client)
            if (serverInstanceName != "")
            {
                argString = argString + " -sin=" + serverInstanceName;
            }

            // if not in standard log place, then must be in InProc log location which is relative to PTI - safe assumption
            if (Directory.Exists(ambrosiaBaseLogDir) == false)
            {
                ambrosiaBaseLogDir = ConfigurationManager.AppSettings["PerfTestJobExeWorkingDirectory"] + ConfigurationManager.AppSettings["PTIAmbrosiaLogDirectory"];
                ambrosiaLogDirFromPTI = "..\\..\\" + ambrosiaBaseLogDir + "\\";   // feels like there has to be better way of determining this - used for TTD
                ambServiceLogPath = "..\\..\\" + ambrosiaBaseLogDir + "\\";
            }

            // used to get log file
            string ambrosiaFullLogDir = ambrosiaBaseLogDir + "\\" + testName + strLogFileInstanceRole + "_0";
            string startingChkPtVersionNumber = "1";
            string logFirstFile = "";

            // Get most recent version of log file and check point
            string actualLogFile = "";
            if (Directory.Exists(ambrosiaFullLogDir))
            {
                DirectoryInfo d = new DirectoryInfo(ambrosiaFullLogDir);
                FileInfo[] files = d.GetFiles().OrderBy(p => p.CreationTime).ToArray();

                foreach (FileInfo file in files)
                {
                    // Sets the first (oldest) file
                    if (logFirstFile == "")
                    {
                        logFirstFile = file.Name;
                    }

                    // This will be most recent file
                    actualLogFile = file.Name;
                }
            }
            else
            {
                Assert.Fail("<JS_VerifyTimeTravelDebugging> Unable to find Log directory: " + ambrosiaFullLogDir);
            }

            // can get first file or most recent
            if (startWithFirstFile)
            {
                actualLogFile = logFirstFile;
            }

            // determine if log or chkpt file
            if (actualLogFile.Contains("chkpt"))
            {
                int chkPtPos = actualLogFile.IndexOf("chkpt");
                startingChkPtVersionNumber = actualLogFile.Substring(chkPtPos + 5);
            }
            else
            {
                int LogPos = actualLogFile.IndexOf("log");
                startingChkPtVersionNumber = actualLogFile.Substring(LogPos + 3);
            }

            startingCheckPoint = startingCheckPoint + startingChkPtVersionNumber;  // used in verification of output log
            JS_UpdateJSConfigFile(JSConfig_debugStartCheckpoint, startingChkPtVersionNumber);
            JS_UpdateJSConfigFile(JSConfig_instanceName, testName + strLogFileInstanceRole.ToLower());  // need to update this so know where the log files are at

            int processID = MyUtils.LaunchProcess(workingDir, fileNameExe, argString, false, logOutputFileName_TestApp);
            if (processID <= 0)
            {
                MyUtils.FailureSupport("");
                Assert.Fail("<JS_VerifyTimeTravelDebugging> JS TestApp was not started.  ProcessID <=0 ");
            }

            // Give it a few seconds to start
            Thread.Sleep(3000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            bool pass = true;

            if ((instanceRole == JSPTI_CombinedInstanceRole) || (instanceRole == ""))
            {
                // Combined Instance role puts client and server in one log file
                pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, totalBytes.ToString(), 15, false, testName, true, checkForDoneString);
                pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, successString, 2, false, testName, true, false);
                pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, allRoundsComplete, 1, false, testName, true, false);

                // Since client and server in same log file, have to check if bidi is here
                if (bidi == false)
                {
                    pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, successEchoString, 0, true, testName, false, false);
                    if (pass == true)
                        Assert.Fail("<JS_VerifyTimeTravelDebugging> Echoed string should NOT have been found in the output but it was.");
                }
                else // do echo string check if bidi
                    pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, successEchoString, 1, false, testName, true, false);

            }

            if (instanceRole == JSPTI_ServerInstanceRole)
            {
                // Verify the TTD outptut  of the server
                pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, bytesReceivedString, 10, false, testName, true);
                pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, successString, 1, false, testName, true);
            }

            if (instanceRole == JSPTI_ClientInstanceRole)
            {
                // Verify the data in the output file of the CLIENT - if NOT bidi the TTD won't work for client only any ways so can assume if doing it, then it is bidi
                pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, successEchoString, 10, false, testName, true, false);
                pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, allRoundsComplete, 1, false, testName, true, false);
            }

            if (specialVerifyString != "")  // used for special strings that are not generic enough to hard code and is more test specific
                pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, specialVerifyString, 1, false, testName, true, false);

            // double check to make sure actually TTD and not just normal run
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, argForTTD, 1, false, testName, true, false);
        }


        //** Clean up all the left overs from JS tests that are related to Blobs. 
        public void JS_TestCleanup_Blob()
        {
            Utilities MyUtils = new Utilities();

            // If failures in queue then do not want to do anything (init, run test, clean up) 
            if (MyUtils.CheckStopQueueFlag())
            {
                return;
            }

            // Stop all running processes that hung or were left behind
            MyUtils.StopAllAmbrosiaProcesses();

            Thread.Sleep(2000);

            // Clean up Azure - this is called after each test so put all test names in for azure tables
            MyUtils.CleanupAzureTables("jsptisavetoblobtest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptisavetoblobbiditest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptiblobnoblankictest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptiblobtwoproctest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptiblobtwoprocbiditest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptiblobclientfiletwoproctest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptiblobserverfiletwoproctest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptiblobdeletelogtest");
            Thread.Sleep(2000);
        }

        //** Clean up all the left overs from JS tests that are related to JS Restarts
        public void JS_TestCleanup_Restart()
        {
            Utilities MyUtils = new Utilities();

            // If failures in queue then do not want to do anything (init, run test, clean up) 
            if (MyUtils.CheckStopQueueFlag())
            {
                return;
            }

            // Stop all running processes that hung or were left behind
            MyUtils.StopAllAmbrosiaProcesses();

            Thread.Sleep(2000);

            // Clean up Azure - this is called after each test so put all test names in for azure tables
            MyUtils.CleanupAzureTables("jsptirestartafterfinishesbiditest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptirestartkillclient");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptirestartkillclientbidi");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptirestartkillserver");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptirestartkillserverbidi");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptirestartkillboth");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptirestartkillbothbidi");
            Thread.Sleep(2000);
        }

        //** Clean up all the left overs from JS tests that are related to JS Basic tests (JS_PTI_BasicUnitTests.cs)
        public void JS_TestCleanup_Basic()
        {
            Utilities MyUtils = new Utilities();

            // If failures in queue then do not want to do anything (init, run test, clean up) 
            if (MyUtils.CheckStopQueueFlag())
            {
                return;
            }

            // Stop all running processes that hung or were left behind
            MyUtils.StopAllAmbrosiaProcesses();

            Thread.Sleep(2000);

            // Clean up Azure - this is called after each test so put all test names in for azure tables
            MyUtils.CleanupAzureTables("jsptibidiendtoendtest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptiendtoendtest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptirestartendtoendbiditest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptirestartendtoendtest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptitwoproctest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptitwoproctestbidi");
            Thread.Sleep(2000);
        }

        //** Clean up all the left overs from JS tests that are related to JS Hosting Mode tests (JS_HostingMode.cs)
        public void JS_TestCleanup_HostingMode()
        {
            Utilities MyUtils = new Utilities();

            // If failures in queue then do not want to do anything (init, run test, clean up) 
            if (MyUtils.CheckStopQueueFlag())
            {
                return;
            }

            // Stop all running processes that hung or were left behind
            MyUtils.StopAllAmbrosiaProcesses();

            Thread.Sleep(2000);

            // Clean up Azure - this is called after each test so put all test names in for azure tables
            MyUtils.CleanupAzureTables("jsptihostmodeseparatetesttwoproc");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptihostmodeseparatetest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptihostmodesepbiditest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptihostmodesepptifirsttest");
            Thread.Sleep(2000);
        }


        //** Clean up all the left overs from JS tests. 
        public void JS_TestCleanup()
        {
            Utilities MyUtils = new Utilities();

            // If failures in queue then do not want to do anything (init, run test, clean up) 
            if (MyUtils.CheckStopQueueFlag())
            {
                return;
            }

            // Stop all running processes that hung or were left behind
            MyUtils.StopAllAmbrosiaProcesses();
            Thread.Sleep(2000);

            // Clean up Azure - this is called after each test so put all test names in for azure tables
            MyUtils.CleanupAzureTables("jsptigiantmessagebiditest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptigiantmessagetest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptigiantcheckpointtest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptigiantcheckpointbiditest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptibidifmstest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptifmstest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptideletefilelogtruetest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptideletefilelogfalsetest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptiautoregexittest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptimigrateclienttwoproctest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptimigrateclienttwoprocbiditest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptimigrateclienttest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptimigrateclientbiditest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptiupgradeservertest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptiupgradeserverbiditest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptiupgradeservertwoproctest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptiupgradeservertwoprocbiditest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptiupgradeserverbacktobacktest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptimultipleclienttest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptimultipleclientbiditest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("jsptinoupgradeversiontest");
            Thread.Sleep(2000);
        }

    }
}

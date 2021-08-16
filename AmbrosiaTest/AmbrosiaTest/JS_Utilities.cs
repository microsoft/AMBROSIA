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
        public string JSConfig_icLogFolder = "icLogFolder";
        public string JSConfig_icBinFolder = "icBinFolder";
        public string JSConfig_useNetCore = "useNetCore";
        public string JSConfig_logTriggerSizeinMB = "logTriggerSizeInMB";
        public string JSConfig_debugStartCheckpoint = "debugStartCheckpoint";
        public string JSConfig_debugTestUpgrade = "debugTestUpgrade";
        public string JSConfig_appVersion = "appVersion";
        public string JSConfig_upgradeVersion = "upgradeVersion";


        // Runs a TS file through the JS LB and verifies code gen works correctly
        // Handles  valid tests one way, Negative tests from a different directory and Source Files as negative tests
        public void Test_CodeGen_TSFile(string TestFile, bool NegTest = false, string PrimaryErrorMessage = "", string SecondaryErrorMessage = "", bool UsingSrcTestFile = false)
        {
            try
            {

                Utilities MyUtils = new Utilities();

                // Test Name is just the file without the extension
                string TestName = TestFile.Substring(0, TestFile.Length - 3);

                //*#*#* TO DO: Use the directories in the App.config file and not hard coded
                //string scriptDir = ConfigurationManager.AppSettings["AmbrosiaJSTestDirectory"];
                //*#*#* TO DO: Use the directories in the App.config file and not hard coded

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
                    Assert.Fail("<StartJSTestApp> JS TestApp was not started.  ProcessID <=0 ");
                }

                // Verify things differently if it is a negative test
                if (NegTest)
                {
                    pass = MyUtils.WaitForProcessToFinish(testOutputLogFile, CodeGenFailMessage, 1, false, TestFile, true,false);

                    // Verify the log file only has the one error (one that is related to not being annotated)
                    if (UsingSrcTestFile)
                    {

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

        // Build JS Test App - easiest to call external powershell script.
        // ** TO DO - maybe make this a generic "build .TS file" or something like that
        // ** For now - this is only .ts that is required to be built
        public void BuildJSTestApp()
        {
            try
            {

                //*#*#* TO DO - Build this ... 

                /*
                Utilities MyUtils = new Utilities();

                // For some reason, the powershell script does NOT work if called from bin/x64/debug directory. Setting working directory to origin fixes it
                string scriptWorkingDir = @"..\..\..\..\..\AmbrosiaTest";
                string scriptDir = ConfigurationManager.AppSettings["AmbrosiaJSTestDirectory"];
                string fileName = "pwsh.exe";
                string parameters = "-file BuildJSTestApp.ps1 " + scriptDir;
                bool waitForExit = true;
                string testOutputLogFile = "BuildJSTestApp.log";

                int powerShell_PID = MyUtils.LaunchProcess(scriptWorkingDir, fileName, parameters, waitForExit, testOutputLogFile);

                // Give it a few seconds to be sure
                Thread.Sleep(2000);
                Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

                // Verify .js file exists
                string expectedjsfile = scriptDir + "\\out\\TestApp.js";
                if (File.Exists(expectedjsfile) == false)
                {
                    MyUtils.FailureSupport("");
                    Assert.Fail("<BuildJSTestApp> " + expectedjsfile + " was not built");
                }
                */
            }
            catch (Exception e)
            {
                Assert.Fail("<BuildJSTestApp> Failure! " + e.Message);
            }
        }


        // Start Javascript Test App
        public int StartJSTestApp(string instanceRole, string testOutputLogFile)
        {

            //node.\out\main.js - ir = Combined - n = 2 - bpr = 128 - mms = 32 - bsc = 32 - bd - nhc
            // instanceRole role  == Client, Server or Combined

            Utilities MyUtils = new Utilities();

            // Launch the client job process with these values
            string workingDir = ConfigurationManager.AppSettings["AmbrosiaJSTestDirectory"]+"\\PTI\\App";
            string fileNameExe = "node.exe";
            string argString = "out\\main.js -ir="+instanceRole+" -n=2 -bpr=128 -mms=32 -bsc=32 -bd -nhc -efb=256 -eeb=256";

            int processID = MyUtils.LaunchProcess(workingDir, fileNameExe, argString, false, testOutputLogFile);
            if (processID <= 0)
            {
                MyUtils.FailureSupport("");
                Assert.Fail("<StartJSTestApp> JS TestApp was not started.  ProcessID <=0 ");
            }

            // Give it a few seconds to start
            Thread.Sleep(5000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            return processID;
        }


        //** Restores the JS Config file for the test app from the golden config file
        //** Probably called in init
        public void JS_RestoreJSConfigFile()
        {
            Utilities MyUtils = new Utilities();

            //*#*#* TO DO
            //*#*#* Finish this 
            //*#*#* Update Init of JS to add this
            //*#*# Update Init to set Auto Register

            // Copy from JSTest\ambrosiaConfigGOLD.json to PTI\App\ambrosiaConfig.json

        }


        //** Sets a JS Config File (ambrosiaConfig.json) setting
        public void JS_UpdateJSConfigFile(string key, string newValue)
        {
            try
            {
                string data = string.Empty;
                string basePath =  ConfigurationManager.AppSettings["AmbrosiaJSTestDirectory"] + "\\PTI\\App";
                string ambrosiaConfigfileName = "ambrosiaConfig.json";
                string ConfigFile = basePath+"\\"+ambrosiaConfigfileName;

                //** Read JSON config file
                data = File.ReadAllText(ConfigFile);
                var jo1 = JObject.Parse(data);
                var tz = jo1[key];
                var currentValue = ((Newtonsoft.Json.Linq.JValue)tz).Value;
                var typeOfCurrentValue = currentValue.GetType();
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
            MyUtils.CleanupAzureTables("jsptibidiendtoendtest");
            Thread.Sleep(2000);
        }


    }
}

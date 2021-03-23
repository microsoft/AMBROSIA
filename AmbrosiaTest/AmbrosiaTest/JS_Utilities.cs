using System;
using System.Diagnostics;
using System.Configuration;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Windows.Forms; // need this to handle threading issue on sleeps
using System.Collections.Generic;
using System.Linq;

namespace AmbrosiaTest
{

    public class JS_Utilities
    {
        // Message at the bottom of the output file to show everything passed
        public string CodeGenSuccessMessage = "Code file generation SUCCEEDED: 2 of 2 files generated; 0 TypeScript errors, 0 merge conflicts";
        public string CodeGenFailMessage = "Code file generation FAILED: 0 of 2 files generated";
        public string CodeGenNoTypeScriptErrorsMessage = "Success: No TypeScript errors found in generated file ";

        // Runs a TS file through the JS LB and verifies code gen works correctly
        public void Test_CodeGen_TSFile(string TestFile, bool NegTest = false, string ExtraConErrorMessage = "", string ExtraPubErrorMessage = "")
        {
            try
            {
                // Test Name is just the file without the extension
                string TestName = TestFile.Substring(0, TestFile.Length - 3);

                Utilities MyUtils = new Utilities();
                string ConSuccessString = CodeGenNoTypeScriptErrorsMessage + TestName+ "_GeneratedConsumerInterface.g.ts";
                string PubSuccessString = CodeGenNoTypeScriptErrorsMessage + TestName+ "_GeneratedPublisherFramework.g.ts";
                bool pass = true;  // not actually used in this test but it is a generic utility fctn return

                // Launch the client job process with these values
                string testfileDir = @"../../AmbrosiaTest/JSCodeGen/JS_CodeGen_TestFiles/";
                if (NegTest)
                {
                    testfileDir = @"../../AmbrosiaTest/JSCodeGen/JS_CodeGen_TestFiles/NegativeTests/";
                }
                string testappdir = ConfigurationManager.AppSettings["AmbrosiaJSCodeGenDirectory"];
                string sourcefile = testfileDir+TestFile;
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
                    pass = MyUtils.WaitForProcessToFinish(testOutputLogFile, CodeGenFailMessage, 1, false, TestFile, true);
                }
                else
                {
                    // Wait to see if success comes shows up in log file for total and for consumer and publisher
                    pass = MyUtils.WaitForProcessToFinish(testOutputLogFile, CodeGenSuccessMessage, 1, false, TestFile, true);
                    pass = MyUtils.WaitForProcessToFinish(testOutputLogFile, ConSuccessString, 1, false, TestFile, true);
                    pass = MyUtils.WaitForProcessToFinish(testOutputLogFile, PubSuccessString, 1, false, TestFile, true);

                    // Verify the generated files with cmp files 
                    string GenConsumerFile = TestName + "_GeneratedConsumerInterface.g.ts";
                    string GenPublisherFile = TestName + "_GeneratedPublisherFramework.g.ts";
                    MyUtils.VerifyTestOutputFileToCmpFile(GenConsumerFile, true);
                    MyUtils.VerifyTestOutputFileToCmpFile(GenPublisherFile, true);
                }

                // Can use these to verify extra messages in the log file
                if (ExtraConErrorMessage != "")
                {
                    pass = MyUtils.WaitForProcessToFinish(testOutputLogFile, ExtraConErrorMessage, 1, false, TestFile, true);
                }
                if (ExtraPubErrorMessage != "")
                {
                    pass = MyUtils.WaitForProcessToFinish(testOutputLogFile, ExtraPubErrorMessage, 1, false, TestFile, true);

                }
            }
            catch (Exception e)
            {
                Assert.Fail("<BuildTSApp> Failure! Exception:" + e.Message);
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


        // *### These will be for the JS PTI calls 
        // Build JS Test App - easiest to call external powershell script.
        // ** TO DO - maybe make this a generic "build .TS file" or something like that
        // ** For now - this is only .ts that is required to be built
        public void BuildJSTestApp()
        {
            try
            {

                  Utilities MyUtils = new Utilities();

                // For some reason, the powershell script does NOT work if called from bin/x64/debug directory. Setting working directory to origin fixes it
                string scriptWorkingDir = @"..\..\..\..\..\AmbrosiaTest";
                string scriptDir = ConfigurationManager.AppSettings["AmbrosiaJSCodeGenDirectory"];
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
            }
            catch (Exception e)
            {
                Assert.Fail("<BuildJSTestApp> Failure! " + e.Message);
            }
        }


        // Start Javascript Test App
        public int StartJSTestApp(string testOutputLogFile)
        {

            Utilities MyUtils = new Utilities();

            // Launch the client job process with these values
            string workingDir = ConfigurationManager.AppSettings["AmbrosiaJSCodeGenDirectory"];
            string fileNameExe = "node.exe";
            string argString = "out\\TestApp.js";

            int processID = MyUtils.LaunchProcess(workingDir, fileNameExe, argString, false, testOutputLogFile);
            if (processID <= 0)
            {
                MyUtils.FailureSupport("");
                Assert.Fail("<StartJSTestApp> JS TestApp was not started.  ProcessID <=0 ");
            }

            // Give it a few seconds to start
            Thread.Sleep(6000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            return processID;
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
        }


    }
}

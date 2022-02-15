using System.Configuration;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AmbrosiaTest
{
    [TestClass]
    public class BasicEXECalls_Test
    {

        //************* Init Code *****************
        // NOTE: Need this bit of code at the top of every "[TestClass]" (per .cs test file) to get context \ details of the current test running
        [TestInitialize()]
        public void Initialize()
        {
            Utilities MyUtils = new Utilities();
            MyUtils.TestInitialize();
        }
        //************* Init Code *****************

        //**** Show Ambrosia Help 
        [TestMethod]
        public void Help_ShowHelp_Ambrosia_Test()
        {
            Utilities MyUtils = new Utilities();

            string current_framework;
            if (MyUtils.NetFrameworkTestRun)
                current_framework = MyUtils.NetFramework;
            else
                current_framework = MyUtils.NetCoreFramework;

            string testName = "showhelpambrosia";
            string fileName = "Ambrosia";
            string workingDir = current_framework;

            GenericVerifyHelp(testName, fileName, workingDir);
        }

        //**** Show Immortal Coord Help 
        [TestMethod]
        public void Help_ShowHelp_ImmCoord_Test()
        {

            Utilities MyUtils = new Utilities();

            string current_framework;
            if (MyUtils.NetFrameworkTestRun)
                current_framework = MyUtils.NetFramework;
            else
                current_framework = MyUtils.NetCoreFramework;

            string testName = "showhelpimmcoord";
            string fileName = "ImmortalCoordinator";
            string workingDir = current_framework;

            GenericVerifyHelp(testName, fileName, workingDir);
        }

        //**** Show PTI Job Help 
        [TestMethod]
        public void Help_ShowHelp_PTIJob_Test()
        {

            Utilities MyUtils = new Utilities();

            // add proper framework 
            string current_framework;
            if (MyUtils.NetFrameworkTestRun)
                current_framework = MyUtils.NetFramework;
            else
                current_framework = MyUtils.NetCoreFramework;

            string testName = "showhelpptijob";
            string fileName = "job";
            string workingDir = MyUtils.baseAmbrosiaPath + ConfigurationManager.AppSettings["PerfTestJobExeWorkingDirectory"] + current_framework;
            GenericVerifyHelp(testName, fileName, workingDir);
        }

        //**** Show PTI Server Help 
        [TestMethod]
        public void Help_ShowHelp_PTIServer_Test()
        {

            Utilities MyUtils = new Utilities();

            // add proper framework 
            string current_framework;
            if (MyUtils.NetFrameworkTestRun)
                current_framework = MyUtils.NetFramework;
            else
                current_framework = MyUtils.NetCoreFramework;

            string testName = "showhelpptiserver";
            string fileName = "server";
            string workingDir = MyUtils.baseAmbrosiaPath + ConfigurationManager.AppSettings["PerfTestServerExeWorkingDirectory"] + current_framework;
            GenericVerifyHelp(testName, fileName, workingDir);
        }

        //**** Show JS PTI Help 
        [TestMethod]
        public void JS_PTI_ShowHelp_Test()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            string testName = "jsptishowhelptest";

            string TestLogDir = MyUtils.baseAmbrosiaPath + ConfigurationManager.AppSettings["TestLogOutputDirectory"];
            string logOutputFileName = testName + ".log";
            string LogOutputDirFileName = TestLogDir + "\\" + logOutputFileName;

            if (MyUtils.NetFrameworkTestRun == false)
            {
                LogOutputDirFileName = TestLogDir + "\\" + testName + "_Core.log";
            }

            string workingDir = MyUtils.baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaJSPTIDirectory"] + JSUtils.JSPTI_AppPath;
            string fileName = "node";
            string argString = "/C node.exe out\\main.js -h > " + LogOutputDirFileName + " 2>&1";

            GenericVerifyHelp(testName, fileName, workingDir, argString);
        }


        //************* Helper Method *****************
        // basic helper method to call and exe with no params so shows help - verify getting proper help screen
        //*********************************************
        public void GenericVerifyHelp(string testName, string fileName, string workingDir, string argString="")
        {
            Utilities MyUtils = new Utilities();
            string TestLogDir = MyUtils.baseAmbrosiaPath + ConfigurationManager.AppSettings["TestLogOutputDirectory"];
            string logOutputFileName = testName + ".log";

            // Get and log the proper help based on if netframework netcore
            string fileNameExe = fileName + ".exe";
            if (MyUtils.NetFrameworkTestRun == false)
            {
                fileNameExe = "dotnet " + fileName + ".dll";
                logOutputFileName = testName + "_Core.log"; // help message different than netframework so have separate cmp file
            }
            string LogOutputDirFileName = TestLogDir + "\\" + logOutputFileName;

            // this makes it more generic where can supply full argstring if want
            if (argString=="")
            {
                argString = "/C " + fileNameExe + " > " + LogOutputDirFileName + " 2>&1";
            }

            // Use ProcessStartInfo class
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                WindowStyle = ProcessWindowStyle.Normal,
                CreateNoWindow = false,
                WorkingDirectory = workingDir,
                FileName = "cmd.exe",
                Arguments = argString
                };

            // Log the info to debug
            string logInfo = "<LaunchProcess> " + workingDir + "\\" + fileNameExe+ " "+ argString; 
            MyUtils.LogDebugInfo(logInfo);

            // Start cmd.exe process that launches proper exe
            Process process = Process.Start(startInfo);

            // Give it a second to completely start \ finish
            MyUtils.TestDelay(4000);

            // Kill the process id for the cmd that launched the window so it isn't lingering
            MyUtils.KillProcess(process.Id);

            // Verify Help message
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName);

        }
    }
}

using System;
using System.Configuration;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

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
            // Don't need to check for framework as proper file is in AmbrosiaTest ... bin directory
            string testName = "showhelpambrosia";
            string fileName = "Ambrosia";
            GenericVerifyHelp(testName, fileName, "");
        }

        //**** Show Immortal Coord Help 
        [TestMethod]
        public void Help_ShowHelp_ImmCoord_Test()
        {
            // Don't need to check for framework as proper file is in AmbrosiaTest ... bin directory
            string testName = "showhelpimmcoord";
            string fileName = "ImmortalCoordinator";
            GenericVerifyHelp(testName, fileName, "");
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
            string workingDir = ConfigurationManager.AppSettings["PerfTestJobExeWorkingDirectory"] + current_framework;
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
            string workingDir = ConfigurationManager.AppSettings["PerfTestServerExeWorkingDirectory"] + current_framework;
            GenericVerifyHelp(testName, fileName, workingDir);
        }

        //**** Show PT Job Help 
        [TestMethod]
        public void Help_ShowHelp_PTJob_Test()
        {
            Utilities MyUtils = new Utilities();

            // add proper framework 
            string current_framework;
            if (MyUtils.NetFrameworkTestRun)
                current_framework = MyUtils.NetFramework;
            else
                current_framework = MyUtils.NetCoreFramework;

            string testName = "showhelpptjob";
            string fileName = "job";
            string workingDir = ConfigurationManager.AppSettings["AsyncPerfTestJobExeWorkingDirectory"] + current_framework;
            GenericVerifyHelp(testName, fileName, workingDir);
        }

        //**** Show PT Server Help 
        [TestMethod]
        public void Help_ShowHelp_PTServer_Test()
        {
            Utilities MyUtils = new Utilities();

            // add proper framework 
            string current_framework;
            if (MyUtils.NetFrameworkTestRun)
                current_framework = MyUtils.NetFramework;
            else
                current_framework = MyUtils.NetCoreFramework;

            string testName = "showhelpptserver";
            string fileName = "server";
            string workingDir = ConfigurationManager.AppSettings["AsyncPerfTestServerExeWorkingDirectory"] + current_framework;
            GenericVerifyHelp(testName, fileName, workingDir);
        }



        //************* Helper Method *****************
        // basic helper method to call and exe with no params so shows help - verify getting proper help screen
        //*********************************************
        public void GenericVerifyHelp(string testName, string fileName, string workingDir)
        {
            Utilities MyUtils = new Utilities();
            string TestLogDir = ConfigurationManager.AppSettings["TestLogOutputDirectory"];
            string logOutputFileName = testName + ".log";

            // Get and log the proper help based on if netframework netcore
            string fileNameExe = fileName + ".exe";
            if (MyUtils.NetFrameworkTestRun == false)
            {
                fileNameExe = "dotnet " + fileName + ".dll";
                logOutputFileName = testName + "_Core.log"; // help message different than netframework so have separate cmp file
            }
            string LogOutputDirFileName = TestLogDir + "\\" + logOutputFileName;

            // Use ProcessStartInfo class
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                WindowStyle = ProcessWindowStyle.Normal,
                CreateNoWindow = false,
                WorkingDirectory = workingDir,
                FileName = "cmd.exe",
                Arguments = "/C " + fileNameExe + " > " + LogOutputDirFileName + " 2>&1"
            };

            // Log the info to debug
            string logInfo = "<LaunchProcess> " + workingDir + "\\" + fileNameExe;
            MyUtils.LogDebugInfo(logInfo);

            // Start cmd.exe process that launches proper exe
            Process process = Process.Start(startInfo);

            // Give it a second to completely start \ finish
            Thread.Sleep(1000);

            // Kill the process id for the cmd that launched the window so it isn't lingering
            MyUtils.KillProcess(process.Id);

            // Verify Help message
            MyUtils.VerifyTestOutputFileToCmpFile(logOutputFileName);

        }
    }
}

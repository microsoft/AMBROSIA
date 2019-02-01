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
            Utilities MyUtils = new Utilities();
            string testName = "showhelpambrosia";
            string fileName = "Ambrosia";
            GenericVerifyHelp(testName, fileName, "",false);
        }

        //**** Show Immortal Coord Help 
        [TestMethod]
        public void Help_ShowHelp_ImmCoord_Test()
        {
            Utilities MyUtils = new Utilities();
            string testName = "showhelpimmcoord";
            string fileName = "ImmortalCoordinator";
            GenericVerifyHelp(testName, fileName, "", false);
        }

        //**** Show PTI Job Help 
        [TestMethod]
        public void Help_ShowHelp_PTIJob_Test()
        {
            Utilities MyUtils = new Utilities();
            string testName = "showhelpptijob";
            string fileName = "job";
            string workingDir = ConfigurationManager.AppSettings["PerfTestJobExeWorkingDirectory"];
            GenericVerifyHelp(testName, fileName, workingDir,true);
        }

        //**** Show PTI Server Help 
        [TestMethod]
        public void Help_ShowHelp_PTIServer_Test()
        {
            Utilities MyUtils = new Utilities();
            string testName = "showhelpptiserver";
            string fileName = "server";
            string workingDir = ConfigurationManager.AppSettings["PerfTestServerExeWorkingDirectory"];
            GenericVerifyHelp(testName, fileName, workingDir,true);
        }

        //**** Show PT Job Help 
        [TestMethod]
        public void Help_ShowHelp_PTJob_Test()
        {
            Utilities MyUtils = new Utilities();
            string testName = "showhelpptjob";
            string fileName = "job";
            string workingDir = ConfigurationManager.AppSettings["AsyncPerfTestJobExeWorkingDirectory"];
            GenericVerifyHelp(testName, fileName, workingDir,true);
        }

        //**** Show PT Server Help 
        [TestMethod]
        public void Help_ShowHelp_PTServer_Test()
        {
            Utilities MyUtils = new Utilities();
            string testName = "showhelpptserver";
            string fileName = "server";
            string workingDir = ConfigurationManager.AppSettings["AsyncPerfTestServerExeWorkingDirectory"];
            GenericVerifyHelp(testName, fileName, workingDir,true);
        }



        //************* Helper Method *****************
        // basic helper method to call and exe with no params so shows help - verify getting proper help screen
        //*********************************************
        public void GenericVerifyHelp(string testName, string fileName, string workingDir, bool ignoreFrameworkType)
        {
            Utilities MyUtils = new Utilities();
            string TestLogDir = ConfigurationManager.AppSettings["TestLogOutputDirectory"];
            string logOutputFileName = testName + ".log";

            // Get and log the proper help based on if netframework netcore
            string fileNameExe = fileName + ".exe";
            if (ignoreFrameworkType == false)
            {
                if (MyUtils.NetFrameworkTestRun == false)
                {
                    fileNameExe = "dotnet " + fileName + ".dll";
                    logOutputFileName = testName + "_Core.log"; // help message different than netframework so have separate cmp file
                }
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

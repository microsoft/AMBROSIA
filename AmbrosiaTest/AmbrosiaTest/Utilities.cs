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

    public class AMB_Settings
    {
        public string AMB_ServiceName { get; set; }
        public string AMB_ImmCoordName { get; set; }   // This will go away
        public string AMB_PortAppReceives { get; set; }
        public string AMB_PortAMBSends { get; set; }
        public string AMB_TestingUpgrade { get; set; }
        public string AMB_ServiceLogPath { get; set; }
        public string AMB_CreateService { get; set; }
        public string AMB_PauseAtStart { get; set; }
        public string AMB_PersistLogs { get; set; }
        public string AMB_ActiveActive { get; set; }
        public string AMB_NewLogTriggerSize { get; set; }
        public string AMB_StartingCheckPointNum { get; set; }
        public string AMB_Version { get; set; }
        public string AMB_UpgradeToVersion { get; set; }
        public string AMB_ReplicaNumber { get; set; }

    }

    // These are the different modes of what the AMB is called 
    public enum AMB_ModeConsts { RegisterInstance, AddReplica, DebugInstance };

    public class Utilities
    {

        //*********
        // used in SetStopQueueFlag. Have this var here so can easily set it or not
        // without searching through code for place to turn on and off this feature
        // when = true, the queue will stop on a test failure and leave it in the state it is in (no clean up etc)
        // when = false, the queue will run on test failure and continue to next test
        //*********
        static bool StopQueueOnFail = false;

        //*********
        // NetFrameworkTestRun
        // when = true, the test will run under the assumption that .Net Framework files in AmbrosiaTest\bin\x64\debug (or release) directory (from net46 directory)
        // when = false, the test will run under the assumption that .Net Core files in AmbrosiaTest\bin\x64\debug (or release) directory (from netcoreapp3.1 directory)
        // .NET CORE only has DLLs, so no AMB exe so run by using "dotnet"
        // The two strings (NetFramework and NetCoreFramework) are part of the path when calling PTI and PT - called in helper functions
        //*********
        public bool NetFrameworkTestRun = true;
        public string NetFramework = "net461";
        public string NetCoreFramework = "netcoreapp3.1";

        // Returns the Process ID of the process so you then can something with it
        // Currently output to file using ">", but using cmd.exe to do that.
        // If want to run actual file name (instead of via cmd.exe), then need to use stream reader to get output and send to a file 
        public int LaunchProcess(string workingDirectory, string fileName, string parameterString, bool waitForExit, string testOutputLogFile)
        {
            string fileToExecute = fileName;

            // Check to see if a DLL or not ... if it is, then assume it is .NET CORE so need dotnet call
            if (fileName.Contains(".dll"))
            {
                fileName = "dotnet " + fileName;
                fileToExecute = "dotnet.exe";
            }

            string TestLogDir = ConfigurationManager.AppSettings["TestLogOutputDirectory"];
            string LogOutputDirFileName = TestLogDir + "\\" + testOutputLogFile;

            // Use ProcessStartInfo class
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                WindowStyle = ProcessWindowStyle.Normal,
                CreateNoWindow = false,
                WorkingDirectory = workingDirectory,
                FileName = "cmd.exe",
                Arguments = "/C " + fileName + " " + parameterString + " > " + LogOutputDirFileName + " 2>&1"
            };

            // Log the info to debug
            string logInfo = "<LaunchProcess> " + fileName + " " + parameterString;
            LogDebugInfo(logInfo);

            try
            {

                // Start cmd.exe process that launches proper exe
                Process process = Process.Start(startInfo);
                if (waitForExit)
                    process.WaitForExit();

                // Give it a second to completely start
                Thread.Sleep(1000);

                //Figure out the process ID for the program ... process id from process.start is the process ID for cmd.exe
                Process[] processesforapp = Process.GetProcessesByName(fileToExecute.Remove(fileToExecute.Length - 4));
                if (processesforapp.Length == 0)
                {
                    FailureSupport(fileToExecute);
                    Assert.Fail("<LaunchProcess> Failure! Process " + fileToExecute + " failed to start.");
                    return 0;
                }

                int processID = processesforapp[0].Id;
                var processStart = processesforapp[0].StartTime;

                // make sure to get most recent one as that is safe to know that is one we just created
                for (int i = 1; i <= processesforapp.Length - 1; i++)
                {
                    if (processStart < processesforapp[i].StartTime)
                    {
                        processStart = processesforapp[i].StartTime;
                        processID = processesforapp[i].Id;
                    }
                }

                // Kill the process id for the cmd that launched the window so it isn't lingering
                KillProcess(process.Id);

                return processID;

            }
            catch (Exception e)
            {
                FailureSupport("EmptyProcess");
                Assert.Fail("<LaunchProcess> Failure! Exception:" + e.Message);
                return 0;
            }
        }

        // timing mechanism to see when a process finishes. It uses a trigger string ("FINISHED") and will delay until that string
        // is hit or until maxDelay (mins) is hit
        public bool WaitForProcessToFinish(string logFile, string doneString, int maxDelay, bool truncateAmbrosiaLogs, string testName, bool assertOnFalseReturn)
        {
            int timeCheckInterval = 10000;  // 10 seconds
            int maxTimeLoops = (maxDelay * 60000) / timeCheckInterval;

            logFile = ConfigurationManager.AppSettings["TestLogOutputDirectory"] + "\\" + logFile;

            for (int i = 0; i < maxTimeLoops; i++)
            {

                // This file is being written to when this is called so need to do it a bit fancier
                FileStream logFileStream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                StreamReader logFileReader = new StreamReader(logFileStream);

                while (!logFileReader.EndOfStream)
                {
                    string line = logFileReader.ReadLine();
                    if (line.Contains(doneString))
                    {
                        logFileReader.Close();
                        logFileStream.Close();
                        return true; // kick out because had success
                    }
                }

                // Clean up
                logFileReader.Close();
                logFileStream.Close();

                Thread.Sleep(timeCheckInterval);
                Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

                // Clean up Ambrosia logs if asked to - used in long MTF where logs persist - would run out of disk space quickly
                if (truncateAmbrosiaLogs)
                {
                    TruncateAmbrosiaLogDir(testName);
                }
            }

            // made it here so we know it timed out and didn't find the string it was looking for
            // only pop assert if asked to do that
            if (assertOnFalseReturn == true)
            {
                FailureSupport(testName);

                // If times out without string hit - then pop exception
                Assert.Fail("<WaitForProcessToFinish> Failure! Looking for string:" + doneString + " in log file:" + logFile + " but did not find it after waiting:" + maxDelay.ToString() + " minutes.");
            }

            return false;  // made it this far, we know it is a false

        }


        // Used in clean up code where you want to kill all processes by name - for example, you can kill all "ImmortalCoordinator" processes.
        public void KillProcessByName(string processName)
        {
            try
            {
                Process[] processList = Process.GetProcessesByName(processName);
                for (int i = 0; i < processList.Length; i++)
                {
                    KillProcess(processList[i].Id);
                }
            }
            catch (Exception e)
            {
                FailureSupport("");

                Assert.Fail("<KillProcessByName> Failure! Exception:" + e.Message);
            }
        }


        // cleans up all the Azure tables based on name of Object. 
        public void CleanupAzureTables(string nameOfObjects)
        {
            try
            {
                // If failures in queue then do not want to do anything (init, run test, clean up) 
                if (CheckStopQueueFlag())
                {
                    return;
                }

                // For some reason, the powershell script does NOT work if called from bin/x64/debug directory. Setting working directory to origin fixes it
                string scriptWorkingDir = @"..\..\..\..\..\AmbrosiaTest\AmbrosiaTest";
                string fileName = "powershell.exe";
                string parameters = "-file CleanUpAzure.ps1 " + nameOfObjects + "*";
                bool waitForExit = false;
                string testOutputLogFile = nameOfObjects + "_CleanAzureTables.log";

                int powerShell_PID = LaunchProcess(scriptWorkingDir, fileName, parameters, waitForExit, testOutputLogFile);
            }
            catch (Exception e)
            {
                FailureSupport(nameOfObjects);

                Assert.Fail("<CleanUpAzureTables> Failure! Exception:" + e.Message);
            }
        }

        // Outputs running processes and Azure table contents for specific object
        public void LogAmbrosiaCurrentStatus(string nameOfObjects)
        {
            try
            {
                string scriptWorkingDir = @"..\..\..\..\..\AmbrosiaTest\AmbrosiaTest";
                string fileName = "powershell.exe";
                string parameters = "-file CheckAmbrosiaStatus.ps1 " + nameOfObjects + "*";
                bool waitForExit = false;
                string testOutputLogFile = "AmbrosiaStatus_" + nameOfObjects + ".log";

                int powerShell_PID = LaunchProcess(scriptWorkingDir, fileName, parameters, waitForExit, testOutputLogFile);
            }
            catch (Exception e)
            {
                Assert.Fail("<LogAmbrosiaStatus> Failure! Exception:" + e.Message);
            }
        }


        // Deletes all the log files created by Ambrosia
        public void CleanupAmbrosiaLogFiles()
        {

            try
            {
                // If failures in queue then do not want to do anything (init, run test, clean up) 
                if (CheckStopQueueFlag())
                {
                    return;
                }

                string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";

                if (Directory.Exists(ambrosiaLogDir))
                {
                    Directory.Delete(ambrosiaLogDir, true);
                }

                // Give it a second to make sure - had timing issues where wasn't fully deleted by time got here
                Thread.Sleep(1000);

                // Double check to make sure it is deleted and not locked by something else
                if (Directory.Exists(ambrosiaLogDir))
                {
                    FailureSupport("");
                    Assert.Fail("<CleanupAmbrosiaLogFiles> Unable to delete Log Dir:" + ambrosiaLogDir);
                }

            }
            catch (Exception e)
            {
                FailureSupport("");
                Assert.Fail("<CleanupAmbrosiaLogFiles> Unable to clean up log files. Error:" + e.Message);
            }
        }


        // Kills a single process based on Process ID. Used to kill a ImmCoord, Server etc as those are created with a Process ID return.
        // If the processID isn't there, then will each exception and log a line in AmbrosiaTest_Debug.log
        public void KillProcess(int processID)
        {
            try
            {
                // makes it easier to just do it like this
                Process p = Process.GetProcessById(processID);
                p.Kill();

                //** Give it a second to fully get rid of it
                Thread.Sleep(1000);
            }
            catch (Exception e)
            {
                // Don't want to pop exception because if not there, then that is ok most of time this is just clean up any way
                //Assert.Fail("<KillProcess> Failure! Exception:" + e.Message);
                string logInfo = "<KillProcess> Exception:" + e.Message;
                LogDebugInfo(logInfo);
            }
        }

        //*********************************************************************
        // Makes sure all dependent files exist as well as connection strings etc
        //
        //*********************************************************************
        public void VerifyTestEnvironment()
        {

            // used in PT and PTI - set here by default and change below if need to
            string current_framework = NetFramework;

            // Verify logging directory ... if doesn't exist, create it
            string testLogDir = ConfigurationManager.AppSettings["TestLogOutputDirectory"];
            if (Directory.Exists(testLogDir) == false)
            {
                System.IO.Directory.CreateDirectory(testLogDir);
            }

            string cmpLogDir = ConfigurationManager.AppSettings["TestCMPDirectory"];
            if (Directory.Exists(cmpLogDir) == false)
                Assert.Fail("<VerifyTestEnvironment> Cmp directory does not exist. Expecting:" + cmpLogDir);

            if (NetFrameworkTestRun)
            {
                // File is in same directory as test because part of AMB build
                string ImmCoordExe = "ImmortalCoordinator.exe";
                if (File.Exists(ImmCoordExe) == false)
                    Assert.Fail("<VerifyTestEnvironment> Missing ImmortalCoordinator.exe. Expecting:" + ImmCoordExe);

                // File is in same directory as test 
                string AMBExe = "Ambrosia.exe";
                if (File.Exists(AMBExe) == false)
                    Assert.Fail("<VerifyTestEnvironment> Missing AMB exe. Expecting:" + AMBExe);

            }
            else  // .net core only has dll ...
            {
                // File is in same directory as test because part of AMB build
                string ImmCoordExe = "ImmortalCoordinator.dll";
                if (File.Exists(ImmCoordExe) == false)
                    Assert.Fail("<VerifyTestEnvironment> Missing ImmortalCoordinator.dll. Expecting:" + ImmCoordExe);

                // File is in same directory as test 
                string AMBExe = "Ambrosia.dll";
                if (File.Exists(AMBExe) == false)
                    Assert.Fail("<VerifyTestEnvironment> Missing AMB dll. Expecting:" + AMBExe);

                // used in PTI and PT calls 
                current_framework = NetCoreFramework;

            }

            // Don't need AmbrosiaLibCS.exe as part of tests
            // string AmbrosiaLibCSExe = "AmbrosiaLibCS.dll";  
            // if (File.Exists(AmbrosiaLibCSExe) == false)
            //     Assert.Fail("<VerifyTestEnvironment> Missing AmbrosiaLibcs dll. Expecting:" + AmbrosiaLibCSExe);

            string perfTestJobFile = ConfigurationManager.AppSettings["PerfTestJobExeWorkingDirectory"] + current_framework + "\\job.exe";
            if (File.Exists(perfTestJobFile) == false)
                Assert.Fail("<VerifyTestEnvironment> Missing PTI job.exe. Expecting:" + perfTestJobFile);

            string perfTestServerFile = ConfigurationManager.AppSettings["PerfTestServerExeWorkingDirectory"] + current_framework + "\\server.exe";
            if (File.Exists(perfTestServerFile) == false)
                Assert.Fail("<VerifyTestEnvironment> Missing PTI server.exe. Expecting:" + perfTestServerFile);

            string perfAsyncTestJobFile = ConfigurationManager.AppSettings["AsyncPerfTestJobExeWorkingDirectory"] + current_framework + "\\job.exe";
            if (File.Exists(perfAsyncTestJobFile) == false)
                Assert.Fail("<VerifyTestEnvironment> Missing PerformanceTest job.exe. Expecting:" + perfAsyncTestJobFile);

            string perfAsyncTestServerFile = ConfigurationManager.AppSettings["AsyncPerfTestServerExeWorkingDirectory"] + current_framework + "\\server.exe";
            if (File.Exists(perfAsyncTestJobFile) == false)
                Assert.Fail("<VerifyTestEnvironment> Missing PerformanceTest server.exe. Expecting:" + perfAsyncTestJobFile);


            string connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONN_STRING");
            if (connectionString == null)
                Assert.Fail("<VerifyTestEnvironment> Missing Connection String environment variable 'AZURE_STORAGE_CONN_STRING'");
        }


        //*********************************************************************
        // This takes the log file and compares it to the associated .CMP file
        // NOTE: Has a feature if a line in cmp file has *X* then that line will not be used in comparison - useful for dates or debug messages
        //
        // Assumption:  Test Output logs are .log and the cmp is the same file name but with .cmp extension
        //*********************************************************************
        public void VerifyTestOutputFileToCmpFile(string testOutputLogFile)
        {

            // Give it a second to get all ready to be verified - helps timing issues
            Thread.Sleep(1000);

            string testLogDir = ConfigurationManager.AppSettings["TestLogOutputDirectory"];
            string logOutputDirFileName = testLogDir + "\\" + testOutputLogFile;
            string cmpLogDir = ConfigurationManager.AppSettings["TestCMPDirectory"];
            string cmpDirFile = cmpLogDir + "\\" + testOutputLogFile.Replace(".log", ".cmp");

            // Put files into memory so can filter out ignore lines etc
            List<string> logFileList = new List<string>();
            List<string> cmpFileList = new List<string>();

            FileStream logFileStream = new FileStream(logOutputDirFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            StreamReader logFileReader = new StreamReader(logFileStream);
            while (!logFileReader.EndOfStream)
            {
                string logline = logFileReader.ReadLine();
                if (!logline.Contains("*X*"))
                {
                    logFileList.Add(logline);
                }

            }
            logFileReader.Close();
            logFileStream.Close();

            FileStream cmpFileStream = new FileStream(cmpDirFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            StreamReader cmpFileReader = new StreamReader(cmpFileStream);
            while (!cmpFileReader.EndOfStream)
            {
                string cmpline = cmpFileReader.ReadLine();
                if (!cmpline.Contains("*X*"))
                {
                    cmpFileList.Add(cmpline);
                }
            }
            cmpFileReader.Close();
            cmpFileStream.Close();

            // Go through filtered list of strings and verify
            string errorMessage = "Log file vs Cmp file failed! Log file is " + testOutputLogFile + ". Elements are in the filtered list where *X* is ignored.";

            // put around a try catch because want to stop the queue as well
            try
            {
                CollectionAssert.AreEqual(cmpFileList, logFileList, errorMessage);
            }
            catch
            {
                FailureSupport("");
                Assert.Fail("Fail:" + errorMessage);
            }
        }

        //*********************************************************************
        // Verifies the integrity of the Ambrosia generated log file by replaying the log file. This replay is how it would recover
        // using the log file.
        // 
        // checkCMPFile: is flag set because MTF change from run to run which would make invalid cmp files so don't check cmp files there
        // startWithFirstFile: this is what determines verify ... if log files haven't been truncated then use first log file. In some cases (long MTF) use most recent
        //                     an extra way of testing things out. 
        //
        // Assumption:  Test Output logs are .log and the cmp is the same file name but with .cmp extension
        //*********************************************************************
        public void VerifyAmbrosiaLogFile(string testName, long numBytes, bool checkCmpFile, bool startWithFirstFile, string CurrentVersion, string optionalNumberOfClient = "", bool asyncTest = false)
        {

            // Basically doing this for multi client stuff
            string optionalMultiClientStartingPoint = "";
            if (optionalNumberOfClient == "")
            {
                optionalNumberOfClient = "1";
            }
            else
            {
                optionalMultiClientStartingPoint = "0";
            }

            string clientJobName = testName + "clientjob" + optionalMultiClientStartingPoint;
            string serverName = testName + "server";
            string ambrosiaLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";

            // used to get log file
            string ambrosiaClientLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\" + testName + "clientjob" + optionalMultiClientStartingPoint + "_" + CurrentVersion;
            string ambrosiaServerLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\" + testName + "server_" + CurrentVersion;
            string startingClientChkPtVersionNumber = "1";
            string clientFirstFile = "";

            // Get most recent version of CLIENT log file and check point
            string clientLogFile = "";
            if (Directory.Exists(ambrosiaClientLogDir))
            {
                DirectoryInfo d = new DirectoryInfo(ambrosiaClientLogDir);
                FileInfo[] files = d.GetFiles().OrderBy(p => p.CreationTime).ToArray();

                foreach (FileInfo file in files)
                {
                    // Sets the first (oldest) file
                    if (clientFirstFile == "")
                    {
                        clientFirstFile = file.Name;
                    }

                    // This will be most recent file
                    clientLogFile = file.Name;
                }
            }
            else
            {
                Assert.Fail("<VerifyAmbrosiaLogFile> Unable to find directory: " + ambrosiaClientLogDir);
            }

            // can get first file or most recent
            if (startWithFirstFile)
            {
                clientLogFile = clientFirstFile;
            }

            // determine if log or chkpt file
            if (clientLogFile.Contains("chkpt"))
            {
                int chkPtPos = clientLogFile.IndexOf("chkpt");
                startingClientChkPtVersionNumber = clientLogFile.Substring(chkPtPos + 5);
            }
            else
            {
                int LogPos = clientLogFile.IndexOf("log");
                startingClientChkPtVersionNumber = clientLogFile.Substring(LogPos + 3);
            }

            // Get most recent version of SERVER log file and check point
            string startingServerChkPtVersionNumber = "1";
            string serverFirstFile = "";
            string serverLogFile = "";
            if (Directory.Exists(ambrosiaServerLogDir))
            {
                DirectoryInfo d = new DirectoryInfo(ambrosiaServerLogDir);
                FileInfo[] files = d.GetFiles().OrderBy(p => p.CreationTime).ToArray();

                foreach (FileInfo file in files)
                {
                    // Sets the first (oldest) file
                    if (serverFirstFile == "")
                    {
                        serverFirstFile = file.Name;
                    }

                    // This will be most recent file
                    serverLogFile = file.Name;
                }
            }

            // can get first file or most recent
            if (startWithFirstFile)
            {
                serverLogFile = serverFirstFile;
            }

            // determine if log or chkpt file
            if (serverLogFile.Contains("chkpt"))
            {
                int chkPtPos = serverLogFile.IndexOf("chkpt");
                startingServerChkPtVersionNumber = serverLogFile.Substring(chkPtPos + 5);
            }
            else
            {
                int LogPos = serverLogFile.IndexOf("log");
                startingServerChkPtVersionNumber = serverLogFile.Substring(LogPos + 3);
            }

            // AMB Call for Job
            string logOutputFileName_AMB1 = testName + "_AMB1_Verify.log";
            AMB_Settings AMB1 = new AMB_Settings
            {
                AMB_ServiceName = clientJobName,
                AMB_ServiceLogPath = ambrosiaLogDir,
                AMB_StartingCheckPointNum = startingClientChkPtVersionNumber,
                AMB_Version = CurrentVersion.ToString(),
                AMB_TestingUpgrade = "N",
                AMB_PortAppReceives = "1000",
                AMB_PortAMBSends = "1001"
            };
            CallAMB(AMB1, logOutputFileName_AMB1, AMB_ModeConsts.DebugInstance);

            // AMB for Server
            string logOutputFileName_AMB2 = testName + "_AMB2_Verify.log";
            AMB_Settings AMB2 = new AMB_Settings
            {
                AMB_ServiceName = serverName,
                AMB_ServiceLogPath = ambrosiaLogDir,
                AMB_StartingCheckPointNum = startingServerChkPtVersionNumber,
                AMB_Version = CurrentVersion.ToString(),
                AMB_TestingUpgrade = "N",
                AMB_PortAppReceives = "2000",
                AMB_PortAMBSends = "2001"
            };
            CallAMB(AMB2, logOutputFileName_AMB2, AMB_ModeConsts.DebugInstance);

            string logOutputFileName_ClientJob_Verify;
            string logOutputFileName_Server_Verify;

            // if async, use the async job and server
            if (asyncTest)
            {
                // Job call
                logOutputFileName_ClientJob_Verify = testName + "_ClientJob_Verify.log";
                int clientJobProcessID = StartAsyncPerfClientJob("1001", "1000", clientJobName, serverName, "1", logOutputFileName_ClientJob_Verify);

                //Server Call
                logOutputFileName_Server_Verify = testName + "_Server_Verify.log";
                int serverProcessID = StartAsyncPerfServer("2001", "2000", serverName, logOutputFileName_Server_Verify);
            }
            else
            {
                // Job call
                logOutputFileName_ClientJob_Verify = testName + "_ClientJob_Verify.log";
                int clientJobProcessID = StartPerfClientJob("1001", "1000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob_Verify);

                //Server Call
                logOutputFileName_Server_Verify = testName + "_Server_Verify.log";
                int serverProcessID = StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server_Verify, Convert.ToInt32(optionalNumberOfClient), false);

            }

            // wait until done running
            bool pass = WaitForProcessToFinish(logOutputFileName_Server_Verify, numBytes.ToString(), 15, false, testName, true);
            pass = WaitForProcessToFinish(logOutputFileName_ClientJob_Verify, numBytes.ToString(), 15, false, testName, true);

            // MTFs don't check cmp files because they change from run to run
            if (checkCmpFile)
            {
                // verify new log files to cmp files
                VerifyTestOutputFileToCmpFile(logOutputFileName_Server_Verify);
                VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob_Verify);
            }

        }

        public int StartImmCoord(string ImmCoordName, int portImmCoordListensAMB, string testOutputLogFile, bool ActiveActive = false, int replicaNum = 9999)
        {

            // Launch the AMB process with these values
            string workingDir = "";
            string fileNameExe = "ImmortalCoordinator.exe";
            if (NetFrameworkTestRun == false)
            {
                fileNameExe = "ImmortalCoordinator.dll";
            }

            string argString = "-i=" + ImmCoordName + " -p=" + portImmCoordListensAMB.ToString();


            // if Active Active then required to get replicanu
            if (ActiveActive)
            {
                //make sure has all info
                if (replicaNum == 9999)
                {
                    FailureSupport(ImmCoordName);
                    Assert.Fail("<StartImmCoord> Replica Number is required when doing active active ");
                }
                argString = argString + " -aa -r=" + replicaNum.ToString();
            }


            int processID = LaunchProcess(workingDir, fileNameExe, argString, false, testOutputLogFile);
            if (processID <= 0)
            {
                FailureSupport(ImmCoordName);
                Assert.Fail("<StartImmCoord> ImmCoord was not started. ProcessID <=0 ");
            }

            // Give it some time to start
            Thread.Sleep(6000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            return processID;
        }


        //*** Don't return a ProcessID because the process only lasts quick second. Then no longer there so killprocess would cause error
        public void CallAMB(AMB_Settings AMBSettings, string testOutputLogFile, AMB_ModeConsts AMBMode)
        {
            // Launch the AMB process with these values
            string workingDir = "";
            string fileNameExe = "Ambrosia.exe";
            if (NetFrameworkTestRun == false)
            {
                fileNameExe = "Ambrosia.dll";
            }

            string argString = "none";

            // Set up args for the proper mode
            switch (AMBMode)
            {
                case AMB_ModeConsts.RegisterInstance:

                    argString = "RegisterInstance " + "-i=" + AMBSettings.AMB_ServiceName
                        + " -rp=" + AMBSettings.AMB_PortAppReceives + " -sp=" + AMBSettings.AMB_PortAMBSends;

                    // add pause at start
                    if (AMBSettings.AMB_PauseAtStart != null && AMBSettings.AMB_PauseAtStart != "N")
                        argString = argString + " -ps";

                    // add Create Service
                    if (AMBSettings.AMB_CreateService != null)
                        argString = argString + " -cs=" + AMBSettings.AMB_CreateService;

                    // add Service log path
                    if (AMBSettings.AMB_ServiceLogPath != null)
                        argString = argString + " -l=" + AMBSettings.AMB_ServiceLogPath;

                    // add no persist logs at start
                    if (AMBSettings.AMB_PersistLogs != null && AMBSettings.AMB_PersistLogs != "Y")
                        argString = argString + " -npl";

                    // add active active
                    if (AMBSettings.AMB_ActiveActive != null && AMBSettings.AMB_ActiveActive != "N")
                        argString = argString + " -aa";

                    // add upgrade version if it exists
                    if (AMBSettings.AMB_UpgradeToVersion != null)
                        argString = argString + " -uv=" + AMBSettings.AMB_UpgradeToVersion;

                    // add current version if it exists
                    if (AMBSettings.AMB_Version != null)
                        argString = argString + " -cv=" + AMBSettings.AMB_Version;

                    // add new log trigger size if it exists
                    if (AMBSettings.AMB_NewLogTriggerSize != null)
                        argString = argString + " -lts=" + AMBSettings.AMB_NewLogTriggerSize;

                    break;

                case AMB_ModeConsts.AddReplica:
                    argString = "AddReplica " + "-r=" + AMBSettings.AMB_ReplicaNumber + " -i=" + AMBSettings.AMB_ServiceName
                        + " -rp=" + AMBSettings.AMB_PortAppReceives + " -sp=" + AMBSettings.AMB_PortAMBSends;

                    // add Service log path
                    if (AMBSettings.AMB_ServiceLogPath != null)
                        argString = argString + " -l=" + AMBSettings.AMB_ServiceLogPath;

                    // add Create Service
                    if (AMBSettings.AMB_CreateService != null)
                        argString = argString + " -cs=" + AMBSettings.AMB_CreateService;

                    // add pause at start
                    if (AMBSettings.AMB_PauseAtStart != null && AMBSettings.AMB_PauseAtStart != "N")
                        argString = argString + " -ps";

                    // add no persist logs at start
                    if (AMBSettings.AMB_PersistLogs != null && AMBSettings.AMB_PersistLogs != "Y")
                        argString = argString + " -npl";

                    // add new log trigger size if it exists
                    if (AMBSettings.AMB_NewLogTriggerSize != null)
                        argString = argString + " -lts=" + AMBSettings.AMB_NewLogTriggerSize;

                    // add active active
                    if (AMBSettings.AMB_ActiveActive != null && AMBSettings.AMB_ActiveActive != "N")
                        argString = argString + " -aa";

                    // add current version if it exists
                    if (AMBSettings.AMB_Version != null)
                        argString = argString + " -cv=" + AMBSettings.AMB_Version;

                    // add upgrade version if it exists
                    if (AMBSettings.AMB_UpgradeToVersion != null)
                        argString = argString + " -uv=" + AMBSettings.AMB_UpgradeToVersion;

                    break;

                case AMB_ModeConsts.DebugInstance:
                    argString = "DebugInstance " + "-i=" + AMBSettings.AMB_ServiceName + " -rp=" + AMBSettings.AMB_PortAppReceives
                        + " -sp=" + AMBSettings.AMB_PortAMBSends;

                    // add Service log path
                    if (AMBSettings.AMB_ServiceLogPath != null)
                        argString = argString + " -l=" + AMBSettings.AMB_ServiceLogPath;

                    // add Check point
                    if (AMBSettings.AMB_StartingCheckPointNum != null)
                        argString = argString + " -c=" + AMBSettings.AMB_StartingCheckPointNum;

                    // add version
                    if (AMBSettings.AMB_Version != null)
                        argString = argString + " -cv=" + AMBSettings.AMB_Version;

                    // testing upgrade
                    if (AMBSettings.AMB_TestingUpgrade != null && AMBSettings.AMB_TestingUpgrade != "N")
                        argString = argString + " -tu";

                    break;
            }

            int processID = LaunchProcess(workingDir, fileNameExe, argString, false, testOutputLogFile);
            if (processID <= 0)
            {
                FailureSupport("");
                Assert.Fail("<CallAMB> AMB was not started.  ProcessID <=0 ");
            }

            // Give it a bit to start
            Thread.Sleep(5000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

        }

        // Starts the server.exe from PerformanceTestUninterruptible.  
        public int StartPerfServer(string receivePort, string sendPort, string perfJobName, string perfServerName, string testOutputLogFile, int NumClients, bool upgrade, long optionalMemoryAllocat = 0)
        {

            // Configure upgrade properly
            string upgradeString = "N";
            if (upgrade)
            {
                upgradeString = "Y";
            }

            // Set path by using proper framework
            string current_framework = NetCoreFramework;
            if (NetFrameworkTestRun)
                current_framework = NetFramework;

            // Launch the server process with these values
            string workingDir = ConfigurationManager.AppSettings["PerfTestServerExeWorkingDirectory"] + current_framework;
            string fileNameExe = "Server.exe";
            string argString = "-j=" + perfJobName + " -s=" + perfServerName + " -rp=" + receivePort + " -sp=" + sendPort
                + " -n=" + NumClients.ToString() + " -m=" + optionalMemoryAllocat.ToString() + " -c";

            // add upgrade switch if upgradeing
            if (upgradeString != null && upgradeString != "N")
                argString = argString + " -u";

            int processID = LaunchProcess(workingDir, fileNameExe, argString, false, testOutputLogFile);
            if (processID <= 0)
            {
                FailureSupport("");
                Assert.Fail("<StartPerfServer> Perf Server was not started.  ProcessID <=0 ");
            }

            // Give it a few seconds to start
            Thread.Sleep(2000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            return processID;
        }

        // Starts the server.exe from PerformanceTest - handles Async  
        public int StartAsyncPerfServer(string receivePort, string sendPort, string perfServerName, string testOutputLogFile)
        {

            // Set path by using proper framework
            string current_framework = NetCoreFramework;
            if (NetFrameworkTestRun)
                current_framework = NetFramework;

            // Launch the server process with these values
            string workingDir = ConfigurationManager.AppSettings["AsyncPerfTestServerExeWorkingDirectory"] + current_framework;
            string fileNameExe = "Server.exe";
            string argString = "-rp=" + receivePort + " -sp=" + sendPort + " -s=" + perfServerName + " -c ";

            int processID = LaunchProcess(workingDir, fileNameExe, argString, false, testOutputLogFile);
            if (processID <= 0)
            {
                FailureSupport("");
                Assert.Fail("<StartAsyncPerfServer> Async Perf Server was not started.  ProcessID <=0 ");
            }

            // Give it a few seconds to start
            Thread.Sleep(6000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            return processID;
        }


        // Perf Client from PerformanceTestInterruptible --- runs in Async
        public int StartPerfClientJob(string receivePort, string sendPort, string perfJobName, string perfServerName, string perfMessageSize, string perfNumberRounds, string testOutputLogFile)
        {

            // Set path by using proper framework
            string current_framework = NetCoreFramework;
            if (NetFrameworkTestRun)
                current_framework = NetFramework;

            // Launch the client job process with these values
            string workingDir = ConfigurationManager.AppSettings["PerfTestJobExeWorkingDirectory"] + current_framework;
            string fileNameExe = "Job.exe";
            string argString = "-j=" + perfJobName + " -s=" + perfServerName + " -rp=" + receivePort + " -sp=" + sendPort
                + " -mms=" + perfMessageSize + " -n=" + perfNumberRounds + " -c";

            // Start process
            int processID = LaunchProcess(workingDir, fileNameExe, argString, false, testOutputLogFile);
            if (processID <= 0)
            {
                FailureSupport("");
                Assert.Fail("<StartPerfClientJob> Perf Client was not started.  ProcessID <=0 ");
            }

            // Give it a few seconds to start
            Thread.Sleep(2000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            return processID;
        }

        // Perf Client from PerformanceTest --- runs in Async
        public int StartAsyncPerfClientJob(string receivePort, string sendPort, string perfJobName, string perfServerName, string perfNumberRounds, string testOutputLogFile)
        {

            // Set path by using proper framework
            string current_framework = NetCoreFramework;
            if (NetFrameworkTestRun)
                current_framework = NetFramework;

            // Launch the client job process with these values
            string workingDir = ConfigurationManager.AppSettings["AsyncPerfTestJobExeWorkingDirectory"] + current_framework;
            string fileNameExe = "Job.exe";
            string argString = "-rp=" + receivePort + " -sp=" + sendPort + " -j=" + perfJobName + " -s=" + perfServerName + " -n=" + perfNumberRounds + " -c ";

            int processID = LaunchProcess(workingDir, fileNameExe, argString, false, testOutputLogFile);
            if (processID <= 0)
            {
                FailureSupport("");
                Assert.Fail("<StartAsyncPerfClientJob> Async Perf Client was not started.  ProcessID <=0 ");
            }

            // Give it a few seconds to start
            Thread.Sleep(6000);
            Application.DoEvents();  // if don't do this ... system sees thread as blocked thread and throws message.

            return processID;
        }


        public void LogDebugInfo(string logEntry)
        {
            string timeStamp = DateTime.Now.ToString();
            logEntry = "[" + timeStamp + "]  " + logEntry + "\r\n";
            string logDir = ConfigurationManager.AppSettings["TestLogOutputDirectory"];

            try
            {
                // Silently fail if log dir doesn't exist as don't want to stop app for debug info - will warn about it anyways when AMB is ran
                if (Directory.Exists(logDir))
                {
                    File.AppendAllText(logDir + @"\AmbrosiaTest_Debug.log", logEntry);
                }
            }
            catch
            {
                // If debug logging fails ... no biggie, don't want it to stop test
            }
        }

        // ****************************************************************
        // Deletes the log files and check points generated by Ambrosia
        // EXCEPT it keeps the most recent files. This is used in long mean time
        // to failure (MTF) tests. If didn't truncate log directory
        // would run out of hard drive space
        // ****************************************************************
        public void TruncateAmbrosiaLogDir(string testName)
        {
            // Assuming _0 for directory files ... this might be bad assumption
            string ambrosiaClientLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\" + testName + "clientjob_0";
            string ambrosiaServerLogDir = ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\" + testName + "server_0";
            int numberOfFilesToKeep = 8;

            try
            {
                // Take care of client side first
                if (Directory.Exists(ambrosiaClientLogDir))
                {
                    DirectoryInfo d = new DirectoryInfo(@ambrosiaClientLogDir);
                    FileInfo[] files = d.GetFiles().OrderBy(p => p.CreationTime).ToArray();
                    int i = 0;
                    foreach (FileInfo file in files)
                    {

                        string currentFile = file.Name;
                        i++;

                        // Since it is sorted by creation date - want to delete everyone but last files
                        if (i < files.Length - numberOfFilesToKeep - 1)
                        {
                            file.Delete();
                        }
                    }
                }

                // Take care of Server side now
                if (Directory.Exists(ambrosiaServerLogDir))
                {
                    DirectoryInfo d = new DirectoryInfo(ambrosiaServerLogDir);
                    FileInfo[] files = d.GetFiles().OrderBy(p => p.CreationTime).ToArray();
                    int i = 0;
                    foreach (FileInfo file in files)
                    {

                        string currentFile = file.Name;
                        i++;

                        // Since it is sorted by creation date - want to delete everyone but last files
                        if (i < files.Length - numberOfFilesToKeep - 1)
                        {
                            file.Delete();
                        }
                    }
                }

            }
            catch (Exception e)
            {
                // If log clean up fails ... probably not enough to stop the test but log it
                string logInfo = "<TruncateAmbrosiaLogDir> Exception:" + e.Message;
                LogDebugInfo(logInfo);
            }
        }



        // ****************************************************************
        // in MTF test, the log files can vary a bit in terms of what gets passed through
        // in a given amount of time. The key is make sure the server and client both get the same amount
        // ****************************************************************
        public void VerifyBytesRecievedInTwoLogFiles(string logFile1, string logFile2)
        {
            // Log file location
            string firstLogFile = ConfigurationManager.AppSettings["TestLogOutputDirectory"] + "\\" + logFile1;
            string secondLogFile = ConfigurationManager.AppSettings["TestLogOutputDirectory"] + "\\" + logFile2;

            try
            {
                // set default to something different so if not existent, then know it fails
                string bytesReceivedFile1 = "0";
                string bytesReceivedFile2 = "1";

                using (var streamReader = File.OpenText(firstLogFile))
                {
                    var lines = streamReader.ReadToEnd().Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        // Look for specific line
                        if (line.Contains("Bytes received:"))
                        {
                            bytesReceivedFile1 = line.Substring(16);
                        }
                    }
                }
                using (var streamReader2 = File.OpenText(secondLogFile))
                {
                    var lines = streamReader2.ReadToEnd().Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        // Look for specific line
                        if (line.Contains("Bytes received:"))
                        {
                            bytesReceivedFile2 = line.Substring(16);
                        }
                    }
                }

                // Make sure has bytes recieved in it
                if (bytesReceivedFile1 == "0")
                {
                    FailureSupport("");
                    Assert.Fail("Could not find 'Bytes received' in log file:" + logFile1);
                }
                if (bytesReceivedFile2 == "1")
                {
                    FailureSupport("");
                    Assert.Fail("Could not find 'Bytes received' in log file:" + logFile2);
                }

                // Now do final check to make sure they are the same
                if (Convert.ToInt64(bytesReceivedFile1) != Convert.ToInt64(bytesReceivedFile2))
                {
                    FailureSupport("");
                    Assert.Fail("'Bytes received' did not match up. Log:" + logFile1 + " had:" + bytesReceivedFile1 + " and Log:" + logFile2 + " had:" + bytesReceivedFile2);
                }
            }
            catch (Exception e)
            {
                FailureSupport("");
                Assert.Fail("<VerifyBytesRecievedInTwoLogFiles> Exception happened:" + e.Message);
            }
        }

        //** Separate from TestCleanup as want it to be as quick as possible
        public void UnitTestCleanup()
        {
            Utilities MyUtils = new Utilities();

            // If failures in queue then do not want to do anything (init, run test, clean up) 
            if (MyUtils.CheckStopQueueFlag())
            {
                return;
            }

            // Kill all ImmortalCoordinators, Job and Server exes
            MyUtils.KillProcessByName("ImmortalCoordinator");
            MyUtils.KillProcessByName("Job");
            MyUtils.KillProcessByName("Server");
            MyUtils.KillProcessByName("Ambrosia");
            MyUtils.KillProcessByName("MSBuild");
            //MyUtils.KillProcessByName("cmd");  // sometimes processes hang

            // Give it a few second to clean things up a bit more
            Thread.Sleep(2000);

            // Clean up Azure - this is called after each test so put all test names in for azure tables
            MyUtils.CleanupAzureTables("unitendtoendtest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("unitendtoendrestarttest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("unittestactiveactivekillprimary");
            Thread.Sleep(2000);
        }


        public void TestCleanup()
        {
            Utilities MyUtils = new Utilities();

            // If failures in queue then do not want to do anything (init, run test, clean up) 
            if (MyUtils.CheckStopQueueFlag())
            {
                return;
            }

            // Kill all ImmortalCoordinators, Job and Server exes
            MyUtils.KillProcessByName("ImmortalCoordinator");
            MyUtils.KillProcessByName("Job");
            MyUtils.KillProcessByName("Server");
            MyUtils.KillProcessByName("Ambrosia");
            MyUtils.KillProcessByName("MSBuild");
            MyUtils.KillProcessByName("dotnet");
            //MyUtils.KillProcessByName("cmd");  // sometimes processes hang

            // Give it a few second to clean things up a bit more
            Thread.Sleep(5000);

            // Clean up Azure - this is called after each test so put all test names in for azure tables
            MyUtils.CleanupAzureTables("killjobtest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("basictest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("killservertest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("giantmessagetest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("doublekilljob");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("doublekillserver");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("mtfnokill");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("mtfnokillpersist");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("mtfkillpersist");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("activeactiveaddnotekillprimary");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("activeactivekillprimary");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("activeactivekillcheckpoint");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("activeactivekillsecondary");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("activeactivekillsecondaryandcheckpoint");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("activeactivekillclientandserver");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("activeactivekillall");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("startimmcoordlasttest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("actactaddnotekillprimary");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("upgradeserverafterserverdone");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("upgradeserverbeforeserverdone");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("upgradeserverbeforestarts");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("upgradeactiveactiveprimaryonly");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("multipleclientsperserver");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("giantcheckpointtest");

            // Give it a few second to clean things up a bit more
            Thread.Sleep(5000);
        }

        public void AsyncTestCleanup()
        {
            Utilities MyUtils = new Utilities();

            // If failures in queue then do not want to do anything (init, run test, clean up) 
            if (MyUtils.CheckStopQueueFlag())
            {
                return;
            }

            // Kill all ImmortalCoordinators, Job and Server exes
            MyUtils.KillProcessByName("ImmortalCoordinator");
            MyUtils.KillProcessByName("Job");
            MyUtils.KillProcessByName("Server");
            MyUtils.KillProcessByName("Ambrosia");
            MyUtils.KillProcessByName("MSBuild");
            MyUtils.KillProcessByName("dotnet");
            //MyUtils.KillProcessByName("cmd");  // sometimes processes hang

            // Give it a few second to clean things up a bit more
            Thread.Sleep(5000);

            // Clean up Azure - this is called after each test so put all test names in for azure tables
            MyUtils.CleanupAzureTables("asyncbasic");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("asynckilljobtest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("asynckillservertest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("asyncreplaylatest");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("asyncactiveactivebasic");
            Thread.Sleep(2000);
            MyUtils.CleanupAzureTables("asyncactiveactivekillall");
            Thread.Sleep(2000);

            // Give it a few second to clean things up a bit more
            Thread.Sleep(5000);
        }


        public void TestInitialize()
        {

            Utilities MyUtils = new Utilities();

            // If failures in queue then do not want to do anything (init, run test, clean up) 
            if (MyUtils.CheckStopQueueFlag())
            {
                Assert.Fail("Queue Stopped due to previous test failure. This test not run.");
                return;
            }

            // Verify environment
            MyUtils.VerifyTestEnvironment();

            // Make sure azure tables etc are cleaned up - there is a lag when cleaning up Azure so could cause issues with test
            //            Cleanup();

            // make sure log files cleaned up
            MyUtils.CleanupAmbrosiaLogFiles();

            // Give it a few seconds to truly init everything - on 8 min test - 3 seconds is no biggie
            Thread.Sleep(3000);
        }

        // ****************************
        // * All the things that happen on Failure that isn't part of cleanup
        // * 1) Set Stop Queue flag
        // * 2) Log status of system
        // ****************************
        public void FailureSupport(string objectName)
        {
            // Set flag to stop queue so nothing else runs
            SetStopQueueFlag();

            // Logs which processes are running and what is in the Azure files
            LogAmbrosiaCurrentStatus(objectName);
        }


        // ****************************
        // * Sets the "flag" to basically stop queue on failure so environment stays untouched
        // * No known way to stop queue so hack
        // * by creating a file and if file exists then won't run things (tests, clean up etc)
        // ****************************
        public void SetStopQueueFlag()
        {
            string stopQueueFile = ConfigurationManager.AppSettings["TestLogOutputDirectory"] + "\\StopQueue.txt";

            // Have variable at top of file just so makes it easier to set and not set
            if (StopQueueOnFail)
            {
                File.Create(stopQueueFile).Dispose();
            }
        }

        public bool CheckStopQueueFlag()
        {
            string stopQueueFile = ConfigurationManager.AppSettings["TestLogOutputDirectory"] + "\\StopQueue.txt";

            // If file exists (meaning stop queue flag is on), then return true
            if (File.Exists(stopQueueFile))
            {
                return true;
            }
            else
            {
                return false;
            }
        }


    }
}

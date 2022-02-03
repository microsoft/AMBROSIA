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
        // when = true, the test will run under the assumption that .Net Framework files in AmbrosiaTest\bin\x64\debug (or release) directory (from net461 directory)
        // when = false, the test will run under the assumption that .Net Core files in AmbrosiaTest\bin\x64\debug (or release) directory (from netcoreapp3.1 directory)
        // .NET CORE only has DLLs, so no AMB exe so run by using "dotnet"
        // The two strings (NetFramework and NetCoreFramework) are part of the path when calling PTI and PT - called in helper functions
        // NOTE: Changing this setting also sets the JS tests to use NetCore
        //*********
        public bool NetFrameworkTestRun = true;
        public string NetFramework = "net461";
        public string NetCoreFramework = "netcoreapp3.1";

        //*********
        // LogType
        // This is type \ location of the logs.. "Files" or "Blobs" in the ImmortalCoordinator
        //*********
        public string logTypeFiles = "files";
        public string logTypeBlobs = "blobs";

        //*********
        // DeployMode
        // This is the mode on whether IC call is part of client and server or on its own (-d paramter in PTI job.exe and server.exe)
        //*********
        public string deployModeSecondProc = "secondproc"; // original design where need IC in separate process
        public string deployModeInProc = "inprocdeploy"; // No longer need rp and sp ports since we are using pipes instead of TCP
        public string deployModeInProcManual = "inprocmanual";  // this is the TCP port call where need rp & sp but still in single proc per job or server
        public string deployModeInProcTimeTravel = "inproctimetravel";  // Used by Client and Server of PTI for time travel debugging

        //*********
        // Base of Ambrosia paths
        // Base test path for Ambrosia that all the test paths will be based on  -- if source code is at c:\Ambrosia\AmbrosiaTest etc, this will be c:\Ambrosia\ and is used as the base for all the paths in App.Config
        // The value for this variable is defined in TestInitialize() which is called at beginning of every test
        // ** NOTE -- this path has a "\" on it already so don't add it when appending to path in App.Config
        //*********
        public string baseAmbrosiaPath = "";

        // Since every test uses this, set the base directory in constructor
        public Utilities()
        {
            // Get base path for Ambrosia
            string currentDir = Directory.GetCurrentDirectory();
            int AmbrosiaTestLoc = currentDir.IndexOf("AmbrosiaTest");
            baseAmbrosiaPath = currentDir.Substring(0, AmbrosiaTestLoc);  
        }


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

            try
            {

                string TestLogDir = baseAmbrosiaPath + ConfigurationManager.AppSettings["TestLogOutputDirectory"];
                string LogOutputDirFileName = TestLogDir + "\\" + testOutputLogFile;

                int processID = 999;

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

                // Start cmd.exe process that launches proper exe
                Process process = Process.Start(startInfo);

                if (waitForExit)
                    process.WaitForExit();

                // Give it a second to completely start
                Thread.Sleep(2000);

                if (startInfo.Arguments.Contains("dotnet Ambrosia.dll") == false)
                {

                    //Figure out the process ID for the program ... process id from process.start is the process ID for cmd.exe
                    Process[] processesforapp = Process.GetProcessesByName(fileToExecute.Remove(fileToExecute.Length - 4));

                    // Gets proper process ID and returns it -- just warn that it didn't find it right away as it might have been too fast
                    if (processesforapp.Length == 0)
                    {
                        LogDebugInfo("*** <LaunchProcess> WARNING - Process for:" + fileName + " was not found. Maybe stopped before actually shown as running.");
                    }
                    else
                    {

                        processID = processesforapp[0].Id;
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
                    }

                    //*** DEBUG *** LogDebugInfo("<LaunchProcess> Kill parent cmd.exe Process: " + process.Id.ToString());


                    // Kill the process id for the cmd that launched the window so it isn't lingering
                    KillProcess(process.Id);
                }

                //*** DEBUG *** LogDebugInfo("<LaunchProcess> Return " + fileName + " Process ID: " + processID.ToString());

                return processID;

            }
            catch (Exception e)
            {
                FailureSupport("EmptyProcess");
                Assert.Fail("<LaunchProcess> Failure! Exception:" + e.Message);
                return 0;
            }
        }

        // timing mechanism to see when a process finishes. It uses a trigger string ("DONE") and will delay until that string
        // is hit or until maxDelay (mins) is hit it also can determine if the extraStringToFind is part of it as well.
        public bool WaitForProcessToFinish(string logFile, string extraStringToFind, int maxDelay, bool truncateAmbrosiaLogs, string testName, bool assertOnFalseReturn, bool checkForDoneString = true)
        {
            int timeCheckInterval = 10000;  // 10 seconds
            int maxTimeLoops = (maxDelay * 60000) / timeCheckInterval;
            string doneString = "DONE";
            bool foundExtraString = false;
            bool foundDoneString = false;
            logFile = baseAmbrosiaPath + ConfigurationManager.AppSettings["TestLogOutputDirectory"] + "\\" + logFile;

            for (int i = 0; i < maxTimeLoops; i++)
            {

                // This file is being written to when this is called so need to do it a bit fancier
                FileStream logFileStream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                StreamReader logFileReader = new StreamReader(logFileStream);

                while (!logFileReader.EndOfStream)
                {
                    string line = logFileReader.ReadLine();

                    // Looking for "DONE"
                    if (line.Contains(doneString))
                    {
                        foundDoneString = true;
                    }

                    // Looking for extra string (usually byte size or some extra message in output)
                    if (line.Contains(extraStringToFind))
                    {
                        foundExtraString = true;

                        // since not looking for done, can close things down here
                        if (checkForDoneString == false)
                        {
                            logFileReader.Close();
                            logFileStream.Close();
                            return true;
                        }
                    }

                    // kick out because had success only if doneString is found AND the extra string is found 
                    if ((foundDoneString) && (foundExtraString))
                    {
                        logFileReader.Close();
                        logFileStream.Close();
                        return true; 
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

            // made it here so we know it either DONE was not found or the DONE was found but the extra string was not found
            // only pop assert if asked to do that
            if (assertOnFalseReturn == true)
            {
                FailureSupport(testName);

                // If times out without string hit - then pop exception
                if (checkForDoneString)
                {

                    Assert.Fail("<WaitForProcessToFinish> Failure! Looking for '" + doneString + "' string AND the extra string:" + extraStringToFind + " in log file:" + logFile + " but did not find one or both after waiting:" + maxDelay.ToString() + " minutes. ");
                }
                else
                {
                    Assert.Fail("<WaitForProcessToFinish> Failure! Looking for string:" + extraStringToFind + " in log file:" + logFile + " but did not find it after waiting:" + maxDelay.ToString() + " minutes. ");
                }
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
                string scriptWorkingDir = baseAmbrosiaPath + ConfigurationManager.AppSettings["TestRootDirectory"];
                string fileName = "pwsh.exe";
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
//                string scriptWorkingDir = @"..\..\..\..\..\AmbrosiaTest\AmbrosiaTest";
  //              string fileName = "powershell.exe";
      //          string parameters = "-file CheckAmbrosiaStatus.ps1 " + nameOfObjects + "*";
    //            bool waitForExit = false;
        //        string testOutputLogFile = "AmbrosiaStatus_" + nameOfObjects + ".log";

                //*#*# -- remove this for now as it gets stuck and it hasn't been used
                //*#*#   int powerShell_PID = LaunchProcess(scriptWorkingDir, fileName, parameters, waitForExit, testOutputLogFile);
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
                string currentDir = Directory.GetCurrentDirectory();

                // If failures in queue then do not want to do anything (init, run test, clean up) 
                if (CheckStopQueueFlag())
                {
                    return;
                }

                string ambrosiaLogDir = baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
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

                // Clean up the InProc files now. 
                string PTIAmbrosiaLogDir = baseAmbrosiaPath+ConfigurationManager.AppSettings["AmbrosiaLogDirectory"];
                if (Directory.Exists(PTIAmbrosiaLogDir))
                {
                    Directory.Delete(PTIAmbrosiaLogDir, true);
                }

                // Clean up the InProc IC output files from Job and Server
                string InProcICOutputFile = "ICOutput*.txt";
                string CurrentFramework = NetFramework;
                if (NetFrameworkTestRun == false)
                {
                    CurrentFramework = NetCoreFramework;
                }

                // job IC output file and any blob log files
                string PTI_Job_Dir = baseAmbrosiaPath+ConfigurationManager.AppSettings["PerfTestJobExeWorkingDirectory"]+ CurrentFramework;
                if (Directory.Exists(PTI_Job_Dir))
                {
                    var jobdir = new DirectoryInfo(PTI_Job_Dir);
                    foreach (var file in jobdir.EnumerateFiles(InProcICOutputFile))
                    {
                        file.Delete();
                    }
                    // Delete the folders from inproc
                    DeleteDirectoryUsingWildCard(PTI_Job_Dir, "job_");
                }

                // server IC output file and any blob log files 
                string PTI_Server_Dir = baseAmbrosiaPath + ConfigurationManager.AppSettings["PerfTestServerExeWorkingDirectory"] + CurrentFramework;
                if (Directory.Exists(PTI_Server_Dir))
                {
                    var serverdir = new DirectoryInfo(PTI_Server_Dir);
                    foreach (var file in serverdir.EnumerateFiles(InProcICOutputFile))
                    {
                        file.Delete();
                    }
                    // Delete the folders from inproc 
                    DeleteDirectoryUsingWildCard(PTI_Server_Dir, "server_");
                }

                // Give it a second to make sure - had timing issues where wasn't fully deleted by time got here
                Thread.Sleep(1000);

                // Double check to make sure it is deleted and not locked by something else
                if (Directory.Exists(PTIAmbrosiaLogDir))
                {
                    FailureSupport("");
                    Assert.Fail("<CleanupAmbrosiaLogFiles> Unable to delete PTI Log Dir:" + PTIAmbrosiaLogDir);
                }


            }
            catch (Exception e)
            {
                FailureSupport("");
                Assert.Fail("<CleanupAmbrosiaLogFiles> Unable to clean up log files. Error:" + e.Message);
            }
        }

        // Helper function for cleaning up log files where don't know full name of folder to delete
        public void DeleteDirectoryUsingWildCard(string rootpath, string substringtomatch)
        {
            try
            {
                List<string> dirs = new List<string>(Directory.EnumerateDirectories(rootpath));

                foreach (var dir in dirs)
                {
                    string currentDir = dir;
                    if (dir.Contains(substringtomatch))
                    {
                        Directory.Delete(dir, true);
                    }
                }
            }
            catch (Exception e)
            {
                // If log clean up fails ... probably not enough to stop the test but log it
                string logInfo = "<DeleteDirectoryUsingWildCard> Exception:" + e.Message;
                LogDebugInfo(logInfo);
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
                string logInfo = "<KillProcess> WARNING:" + e.Message;
                LogDebugInfo(logInfo);
            }
        }

        //*********************************************************************
        //* Makes sure all dependent files exist as well as connection strings etc
        //*
        //* Have a flag on whether JS test or not as require different PTI checks for JS vs C# LB tests
        //*
        //*********************************************************************
        public void VerifyTestEnvironment(bool JSTest = false)
        {

            // used in PT and PTI - set here by default and change below if need to
            string current_framework = NetFramework;
            string currentDir = Directory.GetCurrentDirectory();

            // Verify logging directory ... if doesn't exist, create it
            string testLogDir = baseAmbrosiaPath+ConfigurationManager.AppSettings["TestLogOutputDirectory"];
            if (Directory.Exists(testLogDir) == false)
            {
                Directory.CreateDirectory(testLogDir);
            }

            string cmpLogDir = baseAmbrosiaPath + ConfigurationManager.AppSettings["TestCMPDirectory"];
            if (Directory.Exists(cmpLogDir) == false)
                Assert.Fail("<VerifyTestEnvironment> Cmp directory does not exist. Expecting:" + cmpLogDir);


            // Verify needed Ambrosia components 
            if (NetFrameworkTestRun)
            {
                // File is in same directory as test because part of AMB build
                string ImmCoordExe = currentDir+"\\" + NetFramework + "\\ImmortalCoordinator.exe";
                if (File.Exists(ImmCoordExe) == false)
                    Assert.Fail("<VerifyTestEnvironment> Missing ImmortalCoordinator.exe. Expecting:" + ImmCoordExe);

                // File is in same directory as test 
                string AMBExe = currentDir + "\\" + NetFramework + "\\Ambrosia.exe";
                if (File.Exists(AMBExe) == false)
                    Assert.Fail("<VerifyTestEnvironment> Missing AMB exe. Expecting:" + AMBExe);
            }
            else  // .net core only has dll ...
            {
                // File is in same directory as test because part of AMB build
                string ImmCoordExe = currentDir + "\\" +NetCoreFramework+"\\ImmortalCoordinator.dll";
                if (File.Exists(ImmCoordExe) == false)
                    Assert.Fail("<VerifyTestEnvironment> Missing ImmortalCoordinator.dll. Expecting:" + ImmCoordExe);

                // File is in same directory as test 
                string AMBExe = currentDir + "\\" + NetCoreFramework + "\\Ambrosia.dll";
                if (File.Exists(AMBExe) == false)
                    Assert.Fail("<VerifyTestEnvironment> Missing AMB dll. Expecting:" + AMBExe);

                // used in PTI and PT calls 
                current_framework = NetCoreFramework;
            }

            // PTI Verfication 
            if (JSTest)
            {
                string perfTestJSFile = baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaJSPTIDirectory"] + "//App//Out//main.js";
                if (File.Exists(perfTestJSFile) == false)
                    Assert.Fail("<VerifyTestEnvironment> Missing JS PTI main.js Expecting:" + perfTestJSFile);
            }
            else
            {
                string perfTestJobFile = baseAmbrosiaPath + ConfigurationManager.AppSettings["PerfTestJobExeWorkingDirectory"] + current_framework + "\\job.exe";
                if (File.Exists(perfTestJobFile) == false)
                    Assert.Fail("<VerifyTestEnvironment> Missing PTI job.exe. Expecting:" + perfTestJobFile);

                string perfTestServerFile = baseAmbrosiaPath + ConfigurationManager.AppSettings["PerfTestServerExeWorkingDirectory"] + current_framework + "\\server.exe";
                if (File.Exists(perfTestServerFile) == false)
                    Assert.Fail("<VerifyTestEnvironment> Missing PTI server.exe. Expecting:" + perfTestServerFile);
            }

            string connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONN_STRING");
            if (connectionString == null)
                Assert.Fail("<VerifyTestEnvironment> Missing Connection String environment variable 'AZURE_STORAGE_CONN_STRING'");
        }


        //*********************************************************************
        // This takes the log file and compares it to the associated .CMP file
        // NOTE: Has a feature if a line in cmp file has *X* then that line will not be used in comparison - useful for dates or debug messages
        //
        // Optional parameter is for Javascript LB tests. There are different locations for Log files and CMP files for JS LB tests
        //
        // Assumption:  Test Output logs are .log and the cmp is the same file name but with .cmp extension
        //*********************************************************************
        public void VerifyTestOutputFileToCmpFile(string testOutputLogFile, bool JSTest = false, bool TTDTest = false, string originalTestName = "")
        {

            // Give it a second to get all ready to be verified - helps timing issues
            Thread.Sleep(1000);

            string testLogDir = baseAmbrosiaPath + ConfigurationManager.AppSettings["TestLogOutputDirectory"];
            string logOutputDirFileName = testLogDir + "\\" + testOutputLogFile;
            string cmpLogDir = baseAmbrosiaPath + ConfigurationManager.AppSettings["TestCMPDirectory"];
            string cmpFile = testOutputLogFile.Replace(".log", ".cmp");

            // special case where unit tests are creating unique test names
            if (originalTestName != "")
                cmpFile = cmpFile.Remove(originalTestName.Length, 3);  // removes the 3 digit unique number

            string cmpDirFile = cmpLogDir + "\\" + cmpFile;



            // TTD tests have different files so need modify file to do proper match
            if (TTDTest)
            {
               cmpDirFile = cmpDirFile.Replace("_TTD_Verify", "_Verify");
            }

            // Javascript tests 
            if (JSTest)
            {
                // Test Log Output
                testLogDir = baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaJSTestDirectory"];
                logOutputDirFileName = testLogDir +"\\"+ testOutputLogFile;  
                cmpLogDir = baseAmbrosiaPath + ConfigurationManager.AppSettings["TestCMPDirectory"] + "\\JS_CodeGen_Cmp";
                cmpDirFile = cmpLogDir + "\\" + testOutputLogFile +".cmp";
            }

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
            string errorMessage = "Log file vs Cmp file failed! Log file: " + testOutputLogFile + ". Elements are in the filtered list where *X* is ignored.";

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
        // using the log file (using Ambrosia). It also does Time Travel Debugging (using PTI).
        // 
        // checkCMPFile: is flag set because MTF change from run to run which would make invalid cmp files so don't check cmp files there
        // startWithFirstFile: this is what determines verify ... if log files haven't been truncated then use first log file. In some cases (long MTF) use most recent
        //                     an extra way of testing things out. 
        //
        // Assumption:  Test Output logs are .log and the cmp is the same file name but with .cmp extension
        //*********************************************************************
        public void VerifyAmbrosiaLogFile(string testName, long numBytes, bool checkCmpFile, bool startWithFirstFile, string CurrentVersion, string optionalNumberOfClient = "", bool asyncTest = false, bool checkForDoneString = true, string ambrosiaLogDir = "", string originalTestName = "")
        {
            string currentDir = Directory.GetCurrentDirectory();

            // Doing this for multi client situations
            string optionalMultiClientStartingPoint = "";
            if (optionalNumberOfClient == "")
            {
                optionalNumberOfClient = "1";
            }
            else
            {
                optionalMultiClientStartingPoint = "0";
            }

            // Used for Unit Tests where unique names are used to avoid collision
            if (originalTestName == "")
                 originalTestName = testName;


            string clientJobName = testName + "clientjob" + optionalMultiClientStartingPoint;
            string serverName = testName + "server";
            string ambrosiaLogDirFromPTI;
            string ambServiceLogPath;


            // allows for using different ambrosia log directory
            if (ambrosiaLogDir == "")
            {
                ambrosiaLogDir = baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"];  // don't put + "\\" on end as mess up location .. need append in Ambrosia call though
                ambrosiaLogDirFromPTI = baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\";
                ambServiceLogPath = ambrosiaLogDir + "\\";

            }
            else
            {
                ambServiceLogPath = "..\\"+ambrosiaLogDir + "\\";
                ambrosiaLogDirFromPTI = "..\\..\\"+ambrosiaLogDir +"\\"; 
            }

            // if not in standard log place, then must be in InProc log location which is relative to PTI - safe assumption
            // used to get log file
            string ambrosiaClientLogDir = ambrosiaLogDir + "\\" + testName + "clientjob" + optionalMultiClientStartingPoint + "_0";  // client is always 0 so don't use + CurrentVersion;
            string ambrosiaServerLogDir = ambrosiaLogDir + "\\" + testName + "server_" + CurrentVersion;

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
                Assert.Fail("<VerifyAmbrosiaLogFile> Unable to find Client Log directory: " + ambrosiaClientLogDir);
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
            else
            {
                Assert.Fail("<VerifyAmbrosiaLogFile> Unable to find Server Log directory: " + ambrosiaClientLogDir);
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
                AMB_ServiceLogPath = ambServiceLogPath,
                AMB_StartingCheckPointNum = startingClientChkPtVersionNumber,
                AMB_Version = "0",   // always 0 CurrentVersion.ToString(),
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
                AMB_ServiceLogPath = ambServiceLogPath,
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
            bool pass = WaitForProcessToFinish(logOutputFileName_ClientJob_Verify, numBytes.ToString(), 15, false, originalTestName, true, checkForDoneString);
            pass = WaitForProcessToFinish(logOutputFileName_Server_Verify, numBytes.ToString(), 15, false, originalTestName, true, checkForDoneString);
            

            // MTFs don't check cmp files because they change from run to run 
            if (checkCmpFile)
            {
                // verify new log files to cmp files
                if (originalTestName == testName)
                {
                    VerifyTestOutputFileToCmpFile(logOutputFileName_Server_Verify);
                    VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob_Verify);
                }
                else
                {
                    VerifyTestOutputFileToCmpFile(logOutputFileName_Server_Verify, false, false, originalTestName);
                    VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob_Verify, false, false, originalTestName);
                }
            }

            // Test Time Travel Debugging on the Log Files from PTI job and PTI server - don't do for MTF as not needed for TTD handled by other tests also cmp files change too much
            VerifyTimeTravelDebugging(originalTestName, numBytes, clientJobName, serverName, ambrosiaLogDirFromPTI, startingClientChkPtVersionNumber, startingServerChkPtVersionNumber, optionalNumberOfClient, CurrentVersion, checkCmpFile, checkForDoneString);

        }

        //*********************************************************************
        // Basically same as VerifyAmbrosiaLogFile but instead of using Ambrosia.exe to verify log, this uses
        // job.exe and server.exe to verify it. Porbably easiest to call from VerifyAmbrosiaLogFile since that does
        // all the work to get the log files and checkpoint numbers
        // Assumption that this is called at the end of a test where Ambrosia.exe was already called to register for this test
        //*********************************************************************
        public void VerifyTimeTravelDebugging(string testName, long numBytes, string clientJobName, string serverName, string ambrosiaLogDir, string startingClientChkPtVersionNumber, string startingServerChkPtVersionNumber, string optionalNumberOfClient = "", string currentVersion = "", bool checkCmpFile = true, bool checkForDoneString = true)
        {

            // Basically doing this for multi client stuff
            if (optionalNumberOfClient == "")
            {
                optionalNumberOfClient = "1";
            }

            // Job call
            string logOutputFileName_ClientJob_TTD_Verify = testName + "_ClientJob_TTD_Verify.log";
            int clientJobProcessID = StartPerfClientJob("1001", "1000", clientJobName, serverName, "65536", "13", logOutputFileName_ClientJob_TTD_Verify, deployModeInProcTimeTravel,"", ambrosiaLogDir, startingClientChkPtVersionNumber);

            //Server Call
            string logOutputFileName_Server_TTD_Verify = testName + "_Server_TTD_Verify.log";
            int serverProcessID = StartPerfServer("2001", "2000", clientJobName, serverName, logOutputFileName_Server_TTD_Verify, Convert.ToInt32(optionalNumberOfClient), false,0, deployModeInProcTimeTravel,"", ambrosiaLogDir, startingServerChkPtVersionNumber,currentVersion);

            // wait until done running
            bool pass = WaitForProcessToFinish(logOutputFileName_Server_TTD_Verify, numBytes.ToString(), 20, false, testName, true, checkForDoneString);
            pass = WaitForProcessToFinish(logOutputFileName_ClientJob_TTD_Verify, numBytes.ToString(), 15, false, testName, true, checkForDoneString);

            // With Meantime to Failure tests don't check cmp files because they change from run to run 
            if (checkCmpFile)
            {
                // verify TTD files to cmp files
                VerifyTestOutputFileToCmpFile(logOutputFileName_Server_TTD_Verify, false, true);
                VerifyTestOutputFileToCmpFile(logOutputFileName_ClientJob_TTD_Verify, false, true);
            }
        }

        public int StartImmCoord(string ImmCoordName, int portImmCoordListensAMB, string testOutputLogFile, bool ActiveActive = false, int replicaNum = 9999, int overRideReceivePort = 0, int overRideSendPort = 0, string overRideLogLoc = "", string overRideIPAddr = "", string logToType = "")
        {

            // Launch the AMB process with these values
            string currentDir = Directory.GetCurrentDirectory();
            string workingDir = currentDir + "\\" + NetFramework + "\\";
            string fileNameExe = "ImmortalCoordinator.exe";

            if (NetFrameworkTestRun == false)
            {
                workingDir = currentDir + "\\" + NetCoreFramework + "\\";
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

            // If the override values sent through, then over ride existing ports, Log loc or IP
            if (overRideReceivePort != 0)
            {
                argString = argString + " -rp=" + overRideReceivePort.ToString();
            }
            if (overRideSendPort != 0)
            {
                argString = argString + " -sp=" + overRideSendPort.ToString();
            }
            if (overRideLogLoc != "")
            {
                argString = argString + " -l=" + overRideLogLoc;
            }
            if (overRideIPAddr != "")
            {
                argString = argString + " -ip=" + overRideIPAddr;
            }
            if (logToType != "")  // could make boolean but made it string so could pass "" to test default
            {
                argString = argString + " -lst="+ logToType;
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
            string currentDir = Directory.GetCurrentDirectory();
            string workingDir = currentDir + "\\" + NetFramework + "\\";
            string fileNameExe = "Ambrosia.exe";

            if (NetFrameworkTestRun == false)
            {
                workingDir = currentDir + "\\" + NetCoreFramework + "\\";
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
        public int StartPerfServer(string receivePort, string sendPort, string perfJobName, string perfServerName, string testOutputLogFile, int NumClients, bool upgrade, long optionalMemoryAllocat = 0, string deployMode = "", string ICPort = "", string TTDLog = "", string TTDCheckpointNum = "", string currentVersion = "", string biDirectional="" )
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

            // Launch the server process with these values based on deploy mode
            string workingDir = baseAmbrosiaPath + ConfigurationManager.AppSettings["PerfTestServerExeWorkingDirectory"] + current_framework;
            string fileNameExe = "Server.exe";
            string argString = "";

            // Determine the arg based on deployMode
            // Original & default method where need separate ImmCoord call
            if ((deployMode == "") || (deployMode == deployModeSecondProc))
            {
                argString = "-j=" + perfJobName + " -s=" + perfServerName + " -rp=" + receivePort + " -sp=" + sendPort
                          + " -n=" + NumClients.ToString() + " -m=" + optionalMemoryAllocat.ToString() + " -c";

                if (deployMode != "")
                {
                    argString = argString + " -d=" + deployModeSecondProc;
                }
            }

            // In proc using Pipe - No longer need rp and sp ports since we are using pipes instead of TCP. ImmCoord port is used - more commonly used in proc scenario
            if (deployMode == deployModeInProc)
            {
                argString = "-j=" + perfJobName + " -s=" + perfServerName 
                          + " -n=" + NumClients.ToString() + " -m=" + optionalMemoryAllocat.ToString() + " -c"
                          + " -d=" + deployModeInProc + " -icp=" + ICPort;
            }
            
            // In proc using TCP - this is the TCP port call where need rp & sp but still in single proc per job or server
            if (deployMode == deployModeInProcManual)
            {
                argString = "-j=" + perfJobName + " -s=" + perfServerName + " -rp=" + receivePort + " -sp=" + sendPort
                        + " -n=" + NumClients.ToString() + " -m=" + optionalMemoryAllocat.ToString() + " -c"
                        + " -d=" + deployModeInProcManual + " -icp=" + ICPort;
            }

            // If starting in Time Travel debugger mode, then add the TTD parameters
            if (deployMode == deployModeInProcTimeTravel)
            {
                // removed " -icp=" + ICPort
                argString = "-j=" + perfJobName + " -s=" + perfServerName
                          + " -n=" + NumClients.ToString() + " -m=" + optionalMemoryAllocat.ToString() + " -c"
                          + " -d=" + deployModeInProcTimeTravel 
                          + " -l=" + TTDLog + " -ch=" + TTDCheckpointNum;

                // The version # used to time travel debug (ignored otherwise).
                if (currentVersion != "")
                {
                    argString = argString + " -cv=" + currentVersion;
                }
            }

            // add upgrade switch if upgrading
            if (upgradeString != null && upgradeString != "N")
                argString = argString + " -u";


            // Disable bidirectional communication
            if (biDirectional != "")
            {
                argString = argString + " -nbd";
            }

            int processID = LaunchProcess(workingDir, fileNameExe, argString, false, testOutputLogFile);
            if (processID <= 0)
            {
                FailureSupport("");
                Assert.Fail("<StartPerfServer> Perf Server was not started.  ProcessID <=0 ");
            }

            // Give it a few seconds to start -- give extra time if starting IC as part of this too
            if (ICPort != "")
            {
                Thread.Sleep(6000);
            }
            Thread.Sleep(3000);
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
            string workingDir = baseAmbrosiaPath + ConfigurationManager.AppSettings["AsyncPerfTestServerExeWorkingDirectory"] + current_framework;
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


        // Perf Client from PerformanceTestInterruptible 
        public int StartPerfClientJob(string receivePort, string sendPort, string perfJobName, string perfServerName, string perfMessageSize, string perfNumberRounds, string testOutputLogFile, string deployMode="", string ICPort="", string TTDLog="", string TTDCheckpointNum="", string NonDescending="")
        {

            // Set path by using proper framework
            string current_framework = NetCoreFramework;
            if (NetFrameworkTestRun)
                current_framework = NetFramework;

            // Set defaults here and can modify based on deploy mode
            string workingDir = baseAmbrosiaPath + ConfigurationManager.AppSettings["PerfTestJobExeWorkingDirectory"] + current_framework;
            string fileNameExe = "Job.exe";
            string argString = "";

            // Determine the arg based on deployMode
            // Original & default method where need separate ImmCoord call
            if ((deployMode=="") || (deployMode== deployModeSecondProc))
            {
                argString = "-j=" + perfJobName + " -s=" + perfServerName + " -rp=" + receivePort + " -sp=" + sendPort
                    + " -mms=" + perfMessageSize + " -n=" + perfNumberRounds + " -c";

                if (deployMode!="")
                {
                    argString = argString + " -d=" + deployModeSecondProc;
                }
            }

            // In proc using Pipe - No longer need rp and sp ports since we are using pipes instead of TCP. ImmCoord port is used - more commonly used in proc scenario
            if (deployMode == deployModeInProc)
            {
                argString = "-j=" + perfJobName + " -s=" + perfServerName + " -mms=" + perfMessageSize + " -n=" + perfNumberRounds + " -c"
                    + " -d=" + deployModeInProc + " -icp=" + ICPort;
            }

            // In proc using TCP - this is the TCP port call where need rp & sp but still in single proc per job or server
            if (deployMode == deployModeInProcManual)
            {
                argString = "-j=" + perfJobName + " -s=" + perfServerName + " -rp=" + receivePort + " -sp=" + sendPort
                    + " -mms=" + perfMessageSize + " -n=" + perfNumberRounds + " -c" + " -d=" + deployModeInProcManual + " -icp=" + ICPort;
            }

            // If starting in Time Travel debugger mode, then add the TTD parameters
            if (deployMode == deployModeInProcTimeTravel)
            {
                // removed " -icp=" + ICPort
                argString = "-j=" + perfJobName + " -s=" + perfServerName + " -rp=" + receivePort + " -sp=" + sendPort
                    + " -mms=" + perfMessageSize + " -n=" + perfNumberRounds + " -c" + " -d=" + deployModeInProcTimeTravel  
                    + " -l=" + TTDLog + " -ch=" + TTDCheckpointNum;
            }

            // Disable message descending size - basically makes it a fixed size message
            if (NonDescending != "")
            {
                argString = argString + " -nds";
            }

            // Start process
            int processID = LaunchProcess(workingDir, fileNameExe, argString, false, testOutputLogFile);
            if (processID <= 0)
            {
                FailureSupport("");
                Assert.Fail("<StartPerfClientJob> Perf Client was not started.  ProcessID <=0 ");
            }

            // Give it a few seconds to start -- give extra time if starting IC as part of this too
            if (ICPort != "")
            {
                Thread.Sleep(6000);
            }
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
            string workingDir = baseAmbrosiaPath + ConfigurationManager.AppSettings["AsyncPerfTestJobExeWorkingDirectory"] + current_framework;
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
            string logDir = baseAmbrosiaPath + ConfigurationManager.AppSettings["TestLogOutputDirectory"];

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
            string ambrosiaClientLogDir = baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\" + testName + "clientjob_0";
            string ambrosiaServerLogDir = baseAmbrosiaPath + ConfigurationManager.AppSettings["AmbrosiaLogDirectory"] + "\\" + testName + "server_0";
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
            string firstLogFile = baseAmbrosiaPath + ConfigurationManager.AppSettings["TestLogOutputDirectory"] + "\\" + logFile1;
            string secondLogFile = baseAmbrosiaPath + ConfigurationManager.AppSettings["TestLogOutputDirectory"] + "\\" + logFile2;

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

        //*******************************************************************
        //* Separate from TestCleanup as want it to be as quick as possible
        //*
        //* NOTE: Unit tests are different than other tests as they run as part of Azure Dev Ops CI. 
        //*     Because of that, the name needs to be unique on each machine. Can't have Linux test CI and Windows CI using same name as
        //*     they could delete the other test meta data in Azure. Use a 4 digit random number for each test run
        //*     and put the unique number at the beginning of the test name so the clean up can just delete all with that unique number
        //*******************************************************************
        public void UnitTestCleanup(string uniqueTestIdentifier)
        {
            // If failures in queue then do not want to do anything (init, run test, clean up) 
            if (CheckStopQueueFlag())
            {
                return;
            }

            // Stop all running processes that hung or were left behind
            StopAllAmbrosiaProcesses();

            // Clean up Azure - this is called after each test so put all test names in for azure tables
            //            CleanupAzureTables("unitendtoend"); // all end to end tests
            //          Thread.Sleep(2000);
            //        CleanupAzureTables("unittest"); // all unit tests
            //      Thread.Sleep(2000);

            CleanupAzureTables("unitendtoendtest"+uniqueTestIdentifier); 
            Thread.Sleep(2000);
            CleanupAzureTables("unitendtoendrestarttest" + uniqueTestIdentifier); 
            Thread.Sleep(2000);
            CleanupAzureTables("unittestinproctcp" + uniqueTestIdentifier); 
            Thread.Sleep(2000);
            CleanupAzureTables("unittestactiveactivekillprimary"); 
            Thread.Sleep(2000);
            CleanupAzureTables("unittestinprocpipe"); 
            Thread.Sleep(2000);
            CleanupAzureTables("VssAdministrator"); // Azure Dev Ops left overs
            Thread.Sleep(2000);
        }


        public void TestCleanup()
        {

            // If failures in queue then do not want to do anything (init, run test, clean up) 
            if (CheckStopQueueFlag())
            {
                return;
            }

            // Stop all running processes that hung or were left behind
            StopAllAmbrosiaProcesses();

            // Clean up Azure - this is called after each test so put all test names in for azure tables
            CleanupAzureTables("killjobtest");
            Thread.Sleep(2000);
            CleanupAzureTables("basictest");
            Thread.Sleep(2000);
            CleanupAzureTables("killservertest");
            Thread.Sleep(2000);
            CleanupAzureTables("giant"); // all giant tests
            Thread.Sleep(2000);
            CleanupAzureTables("doublekill");  // all double kill tests
            Thread.Sleep(2000);
            CleanupAzureTables("mtf"); // all mtf tests
            Thread.Sleep(2000);
            CleanupAzureTables("activeactive"); // all active active tests
            Thread.Sleep(2000);
            CleanupAzureTables("startimmcoordlasttest");
            Thread.Sleep(2000);
            CleanupAzureTables("actactaddnotekillprimary");
            Thread.Sleep(2000);
            CleanupAzureTables("upgrade"); // all upgrade tests
            Thread.Sleep(2000);
            CleanupAzureTables("migrateclient");
            Thread.Sleep(2000);
            CleanupAzureTables("multipleclientsperserver");
            Thread.Sleep(2000);
            CleanupAzureTables("overrideoptions");
            Thread.Sleep(2000);
            CleanupAzureTables("savelogto");  // all save log to ... tests
            Thread.Sleep(2000);
            CleanupAzureTables("fixedmessagetest");
            Thread.Sleep(2000);
            CleanupAzureTables("nobiditest");

            // Give it a few second to clean things up a bit more
            Thread.Sleep(5000);
        }

        public void InProcPipeTestCleanup()
        {

            // If failures in queue then do not want to do anything (init, run test, clean up) 
            if (CheckStopQueueFlag())
            {
                return;
            }

            // Stop all running processes that hung or were left behind
            StopAllAmbrosiaProcesses();

            // Clean up Azure - this is called after each test so put all test names in for azure tables
            CleanupAzureTables("inprocpipe"); // all inproc pipe
            Thread.Sleep(2000);
            CleanupAzureTables("inprocbasictest");
            Thread.Sleep(2000);
            CleanupAzureTables("inprocgiant"); // in proc giant
            Thread.Sleep(2000);
            CleanupAzureTables("inprocdoublekill"); // double kill tests
            Thread.Sleep(2000);
            CleanupAzureTables("inprockill"); // kill tests
            Thread.Sleep(2000);
            CleanupAzureTables("inprocmultipleclientsperserver");
            Thread.Sleep(2000);
            CleanupAzureTables("inprocblob");
            Thread.Sleep(2000);
            CleanupAzureTables("inprocfileblob");
            Thread.Sleep(2000);
            CleanupAzureTables("inprocmigrateclient");
            Thread.Sleep(2000);
            CleanupAzureTables("inprocupgrade"); // upgrade
            Thread.Sleep(2000);

            // Give it a few second to clean things up a bit more
            Thread.Sleep(5000);
        }


        public void InProcTCPTestCleanup()
        {

            // If failures in queue then do not want to do anything (init, run test, clean up) 
            if (CheckStopQueueFlag())
            {
                return;
            }

            // Stop all running processes that hung or were left behind
            StopAllAmbrosiaProcesses();

            // Clean up Azure - this is called after each test so put all test names in for azure tables
            CleanupAzureTables("inproctcpclientonly");
            Thread.Sleep(2000);
            CleanupAzureTables("inproctcpserveronly");
            Thread.Sleep(2000);
            CleanupAzureTables("inprocclient"); // tcp client tests
            Thread.Sleep(2000);
            CleanupAzureTables("inproctcpkill");  // tcp kill tests
            Thread.Sleep(2000);
            CleanupAzureTables("inproctcpfileblob");
            Thread.Sleep(2000);
            CleanupAzureTables("inproctcpblob");
            Thread.Sleep(2000);
            CleanupAzureTables("inproctcpupgradeserver");
            Thread.Sleep(2000);
            CleanupAzureTables("inproctcpmigrateclient");
            Thread.Sleep(2000);

            // Give it a few second to clean things up a bit more
            Thread.Sleep(5000);
        }

        public void StopAllAmbrosiaProcesses()
        {

            // If failures in queue then do not want to do anything (init, run test, clean up) 
            if (CheckStopQueueFlag())
            {
                return;
            }

            // Kill all ImmortalCoordinators, Job and Server exes
            KillProcessByName("Job");
            KillProcessByName("Server");
            KillProcessByName("ImmortalCoordinator");
            KillProcessByName("Ambrosia");
            KillProcessByName("MSBuild");
            KillProcessByName("dotnet");
            //KillProcessByName("cmd");  // sometimes processes hang
            //KillProcessByName("node");  // Azure Dev Ops uses Node so killing it here affects that


            // Give it a few second to clean things up a bit more
            Thread.Sleep(5000);
        }


        // ****************************
        // * Basic Init called at beginning of each Test that is Ambrosia related
        //*
        //* Have parameter for JS tests as they have their own Test Evironment to verify
        //* 
        // ****************************
        public void TestInitialize(bool JSTest = false)
        {

            // If failures in queue then do not want to do anything (init, run test, clean up) 
            if (CheckStopQueueFlag())
            {
                Assert.Fail("Queue Stopped due to previous test failure. This test not run.");
                return;
            }

            // Verify environment
            VerifyTestEnvironment(JSTest);

            // Make sure azure tables etc are cleaned up - there is a lag when cleaning up Azure so could cause issues with tests if do it before every test so don't do
            //            Cleanup();

            // Make sure nothing running from previous test
            StopAllAmbrosiaProcesses();

            // make sure log files cleaned up
            CleanupAmbrosiaLogFiles();

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
            string stopQueueFile = baseAmbrosiaPath + ConfigurationManager.AppSettings["TestLogOutputDirectory"] + "\\StopQueue.txt";

            // Have variable at top of file just so makes it easier to set and not set
            if (StopQueueOnFail)
            {
                File.Create(stopQueueFile).Dispose();
            }
        }

        public bool CheckStopQueueFlag()
        {
            string stopQueueFile = baseAmbrosiaPath + ConfigurationManager.AppSettings["TestLogOutputDirectory"] + "\\StopQueue.txt";

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

## Ambrosia Test Automation

The test environment used to test all aspects of Ambrosia has been developed and refined over the years. It is a fairly complex system with a lot of features that test aspects of Ambrosia core, C# language binding and Node.JS language binding. The full test run of all tests takes 12+ hours to run.

You can run tests in 2 modes: interactively/selectively using Visual Studio, or by batch using the test queue (which runs all tests). Using VS is easier, but to run the full test suite consumes a lot of memory, and if VS stops/crashes (eg. due to Windows Update, power outage etc) there’s no persistent log of how far it got through the tests.  

&nbsp;
### **To run tests in Visual Studio:**
1. All tests are under Ambrosia\AmbrosiaTest folder 
2. **(C# Testing)** 
   1. Copy Ambrosia binaries: From [Releases · microsoft/AMBROSIA · GitHub](https://github.com/microsoft/AMBROSIA/releases) to \Ambrosia\AmbrosiaTest\AmbrosiaTest\bin\x64\release\ 
   1. Make sure to copy both net461 and netcoreapp3.1 folders and put folder in the \release\ folder 
3. **(Node.js Testing)** 
   1. Copy the ambrosia-node.2.0.1.tgz (or latest version) to \Ambrosia\AmbrosiaTest\JSTest
   1. From the JSTest directory *npm install .\ambrosia-node.2.0.1.tgz* 
   1. Rebuild PTI – in \Ambrosia\Clients\AmbrosiaJS\PTI-Node\App folder. *npx tsc –p .\tsconfig.json ‘--incremental false’*
   1. Rebuild code gen test app: \Ambrosia\AmbrosiaTest\JSTest *npx tsc –p tsconfig.json*
4. NOTE: Check the “Config \ Notes \ Trouble Shooting” section to make sure proper configuration (especially app.config and connection env var) 
5. Make sure the **PTI project** (\Ambrosia\InternalImmortals\PerformanceTestInterruptible) is built release (don’t forget to do code gen on it) 
6. Open **AmbrosiaTest.sln** 
7. **Build** Release 
8. Test -> Windows -> **Test Explorer** 
10. Output (pass \ fail; info etc) from the test is in the Test Explorer at the bottom – also shows “green check” by the test on pass 
11. **\*\*WARNING \*\*** - tests are registered in Azure and the clean up code is based on test names. DO NOT run the queue on one machine while it is running on another machine. One machine could be deleting Azure data which affects tests running on the other machine etc.  

&nbsp;
### **To run test queue from batch file:**
Most likely you won’t need to do, but there are advantages of running queue but 99% of the time it is easiest to run in VS. Advantages of running in batch: 1) The output is logged to a file so if machine is rebooted, you see the results 2) All apps (Visual Studio etc) except for a cmd prompt can be closed freeing up resources for the testing and help with any timing issues 
1. **Copy files:** From [Releases · microsoft/AMBROSIA · GitHub](https://github.com/microsoft/AMBROSIA/releases) copy the net461 and netcoreapp3.1 folders to \Ambrosia\AmbrosiaTest\AmbrosiaTest\bin\x64\release\ 
1. Open **AmbrosiaTest sln** and build the “Release” version – this creates AmbrosiaTest.dll which contains the tests 
1. I like to run a unit test or two at this point **in VS** just to make sure the environment is all set up and running fine (unit tests: UnitTest\_\*) 
1. Open **Cmd Prompt** with Admin privileges 
1. Go to Ambrosia\AmbrosiaTest\AmbrosiaTest 
1. Run the “**LaunchTests.bat**” file   -- this queue takes about 12+ hours (there is a LaunchUnitTests.bat as well) 
1. The LaunchTests.bat **output** is showing the directory where the results can be found … C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\IDE\CommonExtensions\Microsoft\TestWindow  
1. Open “**AmbrosiaTestResults.txt**” from the TestWindow directory which will show Pass\Fail results 
1. NOTE – **Ambrosia Logs** are located in Ambrosia\AmbrosiaTest\AmbrosiaTest\AmbrosiaLogs  (configurable in app.config of AmbrosiaTest sln) 
1. NOTE – **Test Logs** (output from test exes) is located at C:\AmbrosiaTest\Log  (configurable in app.config of AmbrosiaTest sln) 

&nbsp;
### **Node.js Language Binding Test details:**
**Notes:**  
- Two types of Node.JS tests
  - Code Gen tests - Tests the code generation aspect of Node.JS by using various test .ts files that get generated into .js files
  - JS PTI tests - This mirrors a subset of the tests that were done for C# Language Binding tests using the internal app called Performance Test Interruptible (PTI).
- Paths used for JS configs are in the app.config file 

**JS Troubleshooting:** 
- Run “npm outdated” and see if it reports anything.  Can run "npm update" 
  - Make sure TypeScript is installed

&nbsp;
### **Config \ Notes\ Troubleshooting:**
- **AZURE\_STORAGE\_CONN\_STRING**  -- make sure this Environment var is set with connection string to Azure 
- **App.config** in Ambrosia\AmbrosiaTest\AmbrosiaTest has absolute path configurations that you will want to modify (eg. TestLogOutputDirectory) 
- **Test Settings** In Visual Studio – AmbrosiaTest sln 
  - Test -> Default Processor Architecture  --- set to “x64”  - if don’t have this set, can get in situation where you click run on a test and nothing happens.
  - Test -> Configure Run Settings  --- make sure “Auto Detect Run Settings File” is “unchecked”  If you were doing code coverage, you would choose the .runsetting in AmbrosiaTest 
- **Log files** 
  - "AmbrosiaLogDirectory" (\AmbrosiaTest\AmbrosiaLogs) is where log files from Ambrosia are written – uses GBs of space so need enough hard drive space (100 GB should be sufficient) 
  - “TestLogOutputDirectory” (C:\AmbrosiaTest\Log) is the output from the EXEs of Ambrosia (ImmortalCoordinator, Ambrosia, Client [PTI], Server [PTI]) 
- To run **.netcore build** – (Default test run is “net46”) 
  - Open AmbrosiaTest\Utilities.cs – change NetFrameworkTestRun constant to “false” 
  - Rebuild and run tests like normal 
- Make sure **dependent programs** are built (PTI etc) … there is code at beginning of the test that checks to make sure all the needed files are there. If they aren’t it will fail out right away 
- To **stop queue on failure** - By default, if a test fails in the queue, it will just go to next – there is a flag in utilities.cs (line 46) that can be set to “true” and it will make it stop and not run any other tests if there is a fail 
- **Note** – tests just launch processes so if you stop the test, there will be processes (job.exe etc) still active which might mess up or prevent access \ deletion of old log files.
- **Note** – the full queue includes the JS Node Language Binding tests 
- **Note** – machine speeds can affect test results as timing issues can be introduced. There are cases where processes finish before being killed \ failed over due to a fast machine. There are also cases where a slow machine will timeout before a test is completed.
- **Note** – Seen cases where a test will fail with “access a socket in a way forbidden by its access permissions”. Not sure why but have seen where a specific machine will block sockets. Might need to change which socket that specific test uses to get around it. To see what socket ranges are blocked, run this powershell script: “netsh interface ipv4 show excludedportrange protocol=tcp”


&nbsp;
### **Generic Test "template":**
1.  Verify test environment – check to make sure PTI files etc are there 
1.  Clean up existing Ambrosia log files 
1.  Launch all exes (Imm Coord, client, server) 
1.  For active \ active or recovery … you will see a wait of 5-10 seconds then process kill and new process restarted (test log file will then show \_restarted) 
1.  Wait for Process to Finish – looking for “DONE” in the test log file. One parameter is max wait time until determining failed and killing it with a Fail  
1.  Kill all processes 
1.  Verify log to cmp 
1.  Compare the test log files to the corresponding compare file (.cmp files in ambrosiatest\ambrosiatest\cmp directory) 
1.  Any line with \*X\* at the start is NOT part of the comparison 
1.  Verify integrity of Ambrosia Log files 
1.  Replay the Ambrosia Log file by sending through Ambrosia.exe using .DebugInstance parameter 
1.  Run the Log File through Time Travel Debugger. This is runs log file through PTI instead of through Ambrosia.exe.
1.  Clean up the test 

&nbsp;
### **Cleanup info for each test:**
- All Ambrosia processes are killed (“ImmortalCoordinator.exe” etc) 
- “CleanupAzure.ps1”  is ran. This cleans up data from the Azure CRA folders for this test and other tests as well. 
- The Ambrosia log files are NOT deleted to allow debugging 
- Note – Before a test is ran, the processes are killed and  the log files are deleted 
- If a test is stopped half way before the clean up is reached, then the Azure files, log files and processes need to be cleaned up manually. 


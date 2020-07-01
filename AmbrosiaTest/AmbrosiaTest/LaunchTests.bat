rem ****************************
rem * Batch file to launch Ambrosia tests
rem * This takes Visual Studio out of the equation
rem * Keeps it simple. 
rem * To use this .bat file you need TestAgent to be installed:
rem * https://www.visualstudio.com/downloads/?q=agents
rem * 
rem * To run this .bat file, make sure to build the AmbrosiaTest or AmbrosiaTest_Local solution (in VS) which will
rem * build AmbrosiaTest.dll and put it in the bin directory.
rem *
rem ****************************

set "testdir=%cd%"
c:
cd\"Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\IDE\CommonExtensions\Microsoft\TestWindow"
vstest.console.exe %testdir%\bin\x64\Release\AmbrosiaTest.dll > AmbrosiaTestResults.txt
rem vstest.console.exe %testdir%\AmbrosiaTest.dll /Tests:AMB_KillServer_Test

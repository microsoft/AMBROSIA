rem ****************************
rem * Batch file to launch Ambrosia unit tests
rem * This takes Visual Studio out of the equation
rem * Keeps it simple. 
rem * To use this .bat file you need TestAgent to be installed:
rem * https://www.visualstudio.com/downloads/?q=agents
rem * 
rem ******************************"

set "testdir=%cd%"
c:
cd\"Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\IDE\CommonExtensions\Microsoft\TestWindow"
vstest.console.exe %testdir%\AmbrosiaTest.dll /Tests:UnitTest_BasicEndtoEnd_Test,UnitTest_BasicActiveActive_KillPrimary_Test,UnitTest_BasicRestartEndtoEnd_Test

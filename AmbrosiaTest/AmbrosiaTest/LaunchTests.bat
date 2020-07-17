echo "****************************""
echo "* Batch file to launch Ambrosia tests"
echo "* This takes Visual Studio out of the equation"
echo "* Keeps it simple. "
echo "* To use this .bat file you need TestAgent to be installed:"
echo "* https://www.visualstudio.com/downloads/?q=agents"
echo "* "
echo "* To run this .bat file, make sure to build the AmbrosiaTest or AmbrosiaTest_Local solution (in VS) which will"
echo "* build AmbrosiaTest.dll and put it in the bin directory."
echo "****************************""

set "testdir=%cd%"
c:
cd\"Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\Common7\IDE\CommonExtensions\Microsoft\TestWindow"
vstest.console.exe %testdir%\bin\x64\Release\AmbrosiaTest.dll > AmbrosiaTestResults.txt
echo vstest.console.exe %testdir%\AmbrosiaTest.dll /Tests:AMB_KillServer_Test

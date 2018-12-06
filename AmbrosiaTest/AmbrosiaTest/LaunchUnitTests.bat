echo "****************************""
echo "* Batch file to launch Ambrosia unit tests"
echo "* This takes Visual Studio out of the equation"
echo "* Keeps it simple. "
echo "* To use this .bat file you need TestAgent to be installed:"
echo "* https://www.visualstudio.com/downloads/?q=agents"
echo "* "
echo "****************************""

set "testdir=%cd%"
c:
cd\"Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\IDE\CommonExtensions\Microsoft\TestWindow"
vstest.console.exe %testdir%\AmbrosiaTest.dll /Tests:UnitTest_BasicEndtoEnd_Test,UnitTest_BasicActiveActive_KillPrimary_Test,UnitTest_BasicRestartEndtoEnd_Test

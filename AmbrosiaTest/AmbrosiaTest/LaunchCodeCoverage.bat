echo "****************************""
echo "* Batch file to do to code coverage of Ambrosia and ImmCoord"
echo "* To use this .bat file you need TestAgent to be installed:"
echo "* https://www.visualstudio.com/downloads/?q=agents"
echo "* "
echo "* To run this .bat file, make sure to build the AmbrosiaTest  solution (in VS) which will"
echo "* build AmbrosiaTest.dll and put it in the bin directory."
echo "* "
echo "* Need the file CodeCoverage.runsettings in the same directory as all exes and dlls"
echo "*"
echo "* After the run, import the .coverage file into Visual Studio (just open the .coverage file in VS). This file is found in TestResults in the "
echo "* directory ...\CommonExtensions\Microsoft\TestWindow\TestResults"
echo "*****************************"

set "testdir=%cd%"
c:
cd\"Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\IDE\CommonExtensions\Microsoft\TestWindow"
vstest.console.exe %testdir%\AmbrosiaTest.dll /EnableCodeCoverage /Settings:%testdir%\CodeCoverage.runsettings /logger:trx




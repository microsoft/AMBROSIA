rem ****************************""
rem * Batch file to do to code coverage of Ambrosia and ImmCoord
rem * To use this .bat file you need TestAgent to be installed:
rem * https://www.visualstudio.com/downloads/?q=agents
rem * 
rem * To run this .bat file, make sure to build the AmbrosiaTest  solution (in VS) which will
rem * build AmbrosiaTest.dll and put it in the bin directory.
rem * 
rem * Need the file CodeCoverage.runsettings in the same directory as all exes and dlls
rem *
rem * After the run, import the .coverage file into Visual Studio (just open the .coverage file in VS). This file is found in TestResults in the 
rem * directory ...\CommonExtensions\Microsoft\TestWindow\TestResults
rem *****************************

set "testdir=%cd%"
c:
cd\"Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\Common7\IDE\CommonExtensions\Microsoft\TestWindow"
vstest.console.exe %testdir%\AmbrosiaTest.dll /EnableCodeCoverage /Settings:%testdir%\CodeCoverage.runsettings /logger:trx




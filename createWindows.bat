echo off

if "%1" == "" goto errorEnd
if "%1" == "?" goto errorEnd
if "%1" == "/" goto errorEnd
if "%1" == "/?" goto errorEnd
if "%1" == "-?" goto errorEnd
if "%1" == "help" goto errorEnd

set EXAMPLE=%1
start %comspec% /k "cd LocalAmbrosiaRuntime\LocalAmbrosiaRuntime\bin\Debug"
rem start %comspec% /k "cd ImmortalCoordinator\bin\Debug"
rem start %comspec% /k "cd ImmortalCoordinator\bin\Debug"
rem start %comspec% /k "cd Examples\%EXAMPLE%\Client\bin\Debug"
rem start %comspec% /k "cd Examples\%EXAMPLE%\Server\bin\Debug"
goto End

:errorEnd
echo Need to specify directory

:End

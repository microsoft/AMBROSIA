# A basic sanity check before committing.
# Sets up a performance test between processes on the local machine.
# Requires an internet connection for accessing Azure Table Storage.

param (
    # The below switch enables super-aggressive cleaning with
    # "git clean".  Be careful you don't have un-added files!
    [switch]$distclean = $false,

    # Skip the build phase if you want to go right to testing.
    [switch]$nobuild = $false,

    # Skip the removing of previous service registrations from Azure table.
    [switch]$noremove = $false,
    
    # Skip removing and adding stuff to Azure,
    [switch]$noupload = $false,
    
    # Skip the test-running phase, meaning this is only useful for
    # setting variables, registering and uploading, and printing hints.
    [switch]$notest = $false,
    
    # Be chatty
    [switch]$debug = $false

)

$ErrorActionPreference = "Stop"

# Configuration
# ------------------------------------------------------------

# Binary locations:
# The fact that we're using TargetFrameworkVersion 4.6.1 is specified in the csproj files:
$localruntime  = "Ambrosia\bin\x64\Release\net46\LocalAmbrosiaRuntime.exe"
$craworker     = "ImmortalCoordinator\bin\x64\Release\net46\ImmortalCoordinator.exe"
$removeservice = "Tools\RemoveService\bin\Release\RemoveService.exe"

# Allow the caller to provide their own AZURE_STORAGE_CONN_STRING if desired:
if (! $env:AZURE_STORAGE_CONN_STRING) {
    Throw "ERROR: env var AZURE_STORAGE_CONN_STRING must be set."
} else {
    Write-Host "Heads up: using this value of AZURE_STORAGE_CONN_STRING:"
    Write-Host "$env:AZURE_STORAGE_CONN_STRING"
}

Write-Host "Using Azure Storage connection string: $env:AZURE_STORAGE_CONN_STRING"

# Set a suffix to avoid interfering with other runs:
# $UNIQ = (Get-Date -UFormat "%s_") + (Get-Random)
# $UNIQ = (Get-Date -UFormat "_%s")
# $UNIQ = (Get-Random).ToString()
$UNIQ = ""
Write-Host "Using Uniq suffix: $UNIQ"

if ($debug) { Set-PSDebug -Strict -Trace 1 }

# TODO: Add unique suffix:
# $jobservice     = "validateJobServ"
# $jobinstance    = "validateJobInst"
# $serverservice  = "validateServServ"
# $serverinstance = "validateServInst"
# $binstag        = "validateBins"

$jobservice     = "rrnjob$UNIQ"
$serverservice  = "rrnserver$UNIQ"
$jobinstance    = "$jobservice"
$serverinstance = "$serverservice"
#$jobinstance    = "rnjobmachine"
#$serverinstance = "rnservermachine"
$binsserver     = "rrnbinaries$UNIQ"
$binsjob        = "rrnbinaries$UNIQ"

# TODO: make this cross platform:
$logsdir        = "c:\logs\validate_$UNIQ\"
# $logsdir        = "./logs"


# Execution
# ------------------------------------------------------------

# (0) 
Write-Host "First, blow away logs that might interfere with our test."
if (Test-Path $logsdir) { rm $logsdir -r -fo }
New-Item -ItemType Directory -Force -Path $logsdir | Out-Null

# (1) Clean if needed.
# (2) Build everything we need for the tests.
if ($nobuild) {
    Write-Host "Skipping build, straight to testing..."
} else {
  if ($distclean) {
      git clean -fxd
      { cd deps\CRA; git clean -fxd }
      .\CmdLine-FreshBuild.ps1 -noclean
  } else {      
      .\CmdLine-FreshBuild.ps1
  }
}

# (3) Remove services from any previous run.  This had better be idempotent.
if (-not ($noremove)) {
    Write-Host "Now we require internet access to remove previously registered services"
    & $removeservice $jobservice    $jobinstance
    & $removeservice $serverservice $serverinstance
    Write-Host "Validate job/server unregistered."
}

$ptestdir = "InternalImmortals\PerformanceTestInterruptible"


# RRN: FIXME - looks like the CLI has changed for LAR:
# Ambrosiaruntime <ServiceName ServiceLogPath StartingCheckpointNum Version(0/*) TestingUpgrade(N/n/*) LocalServiceReceivePort(1000/*) LocalServiceSendToPort(1001/*) >
# OR
# Ambrosiaruntime <CoralInstance LocalServiceReceivePort LocalServiceSendToPort ServiceName ServiceLogPath AmbrosiaBinariesLocation CreateService(N/n/*) PauseAtStart(N/n/*) PersistLogs(N/n/*) NewLogTriggerSize(in MB, 0 for no periodic checkpointing) ActiveActive(Y/y/*) Optional: CurrentVersion(0/*) UpgradeToVersion(0/*)>

Write-Host "To reproduce the below test by hand, run the following commands:"
Write-Host " (C1) $localruntime  $serverinstance 2000 2001 $serverservice $logsdir $binsserver a n y 1000 n 0 0"
Write-Host " (C2) $localruntime  $jobinstance    1000 1001 $jobservice $logsdir $binsjob a n y 1000 n 0 0"
Write-Host " (C3) $craworker $jobinstance 1500"
Write-Host " (C4) $craworker $serverinstance 2500"
Write-Host " (C5) .\$ptestdir\Client\bin\x64\Release\net46\Job.exe    1001 1000 $jobservice $serverservice"
Write-Host " (C6) .\$ptestdir\Server\bin\x64\Release\net46\Server.exe 2001 2000 $jobservice $serverservice"

# (4) 
if (-not ($noupload)) {
    Write-Host "Now register and upload binaries (C1,C2)"
    Start-Process -NoNewWindow -Wait -FilePath "$localruntime" -ArgumentList "$jobinstance    1000 1001 $jobservice    $logsdir $binsjob    a n y 1000 n 0 0"
    Start-Process -NoNewWindow -Wait -FilePath "$localruntime" -ArgumentList "$serverinstance 2000 2001 $serverservice $logsdir $binsserver a n y 1000 n 0 0"
}
# You can check that the above worked like so:
#   az storage table list --connection-string $AZURE_STORAGE_CONN_STRING  --output table | grep $UNIQ
# (This takes many seconds for me. -RRN)

if (-not ($notest))
{
    # (5) 
    Write-Host "Bring up the CRA worker processes (C3,C4)"
    $c1 = Start-Process -NoNewWindow -PassThru -FilePath $craworker -ArgumentList "$jobinstance 1500";
    $c2 = Start-Process -NoNewWindow -PassThru -FilePath $craworker -ArgumentList "$serverinstance 2500"
    # RN: ^ oddity, if I allow NewWindow then an extra process gets thrown
    # in the middle that interferes with things.

    # TODO: could read CRA worker outputs until they say "Ready".
    Start-Sleep -Seconds 20

    # (5B) Bring up the Ambrosia service processes and test.
    # $env:AMBROSIA_JOB_INSTANCE    = $jobinstance
    # $env:AMBROSIA_SERVER_INSTANCE = $serverinstance
    $p1 = Start-Process -NoNewWindow -PassThru -WorkingDirectory "$ptestdir\Client" -FilePath "$ptestdir\Client\bin\x64\Release\net46\Job.exe"    -ArgumentList "1001 1000 $jobservice $serverservice" 
    $p2 = Start-Process -NoNewWindow -PassThru -WorkingDirectory "$ptestdir\Server" -FilePath "$ptestdir\Server\bin\x64\Release\net46\Server.exe" -ArgumentList "2001 2000 $jobservice $serverservice" 

    $c1.WaitForExit()
    $c2.WaitForExit()
    $p1.WaitForExit()
    $p2.WaitForExit()
    # Write-Host "Exit codes: " $c1.ExitCode $c2.ExitCode
    # if ( $c1.ExitCode -ne 0 ) {
    #     Throw Format("{0} child process exited with return code: {1}", $c1.ProcessName, $c1.ExitCode)
    # }

    Write-Host "Passed rudimentary testing."
}

# (6) Repeat step (3) from above.
if (-not ($noremove)) {
    Write-Host "Cleanup: Finally, remove services we just uploaded to avoid pollution."
    & $removeservice $jobservice    $jobinstance
    & $removeservice $serverservice $serverinstance
    Write-Host "Validate job/server unregistered."
}

Write-Host "Validate script finished."

if ($debug) { Set-PSDebug -Trace 0 }


# Ambrosia\bin\x64\Release\net46\LocalAmbrosiaRuntime.exe validateServInst 2000 2001 validateServServ c:\logs\ validateBins a n y 1000 n 0 0

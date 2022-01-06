<#
.SYNOPSIS
This script builds the ambrosia-node package (eg. ambrosia-node-2.0.0.tgz).

.DESCRIPTION
This script builds the ambrosia-node package (eg. ambrosia-node-2.0.0.tgz).
You should do a "git pull" before running this.
It compiles the TypeScript source, runs unit tests, [optionally] increases the 'patch' part of the package
version, archives any old ambrosia-node*.tgz files (to .\buildArchive), then finally creates a new .tgz.

.PARAMETER bumpVersion
<String> value can be one of "patch", "minor" or "major" to indicate which part of the package version number to increase (by 1). Alias is "-bv".
Note that if "minor" is specified, then the patch number will reset to 0, and if "major" is specified then both the minor and patch numbers will reset to 0.

.PARAMETER basicUnitTestsOnly
If specified, only the basic unit tests will run (not the full unit test suite, which includes CodeGen and DataFormat tests). Alias is "-bt".
#>

# Note: Help syntax can be displayed with the command "get-help .\build.ps1" (but only if using PowerShell 7 or later)
param([Alias("bv")] [ValidateSet("patch", "minor", "major")] [string]$bumpVersion,
      [Alias("bt")] [switch] $basicUnitTestsOnly)

# Valid parameters are automatically removed from $args, so if $args is not empty then an invalid parameter was supplied
if ($args.length -gt 0)
{
    get-help .\build.ps1 -Detailed
    Exit 4
}

$step = 0
$tscInstalled = $False
$currDirName = ""
$preBuildError = ""
$currentBranchName = ""
$branchStatus = ""
$isBranchClean = $False
$deletedFileCount = 0
$tgzCount = 0
$isWindowsOS = ([System.Environment]::OSVersion.Platform -eq "Win32NT")
$isUnixOS = ([System.Environment]::OSVersion.Platform -eq "Unix")

function report
{
    param([string]$prefix, [string]$msg, [boolean]$showDelimiterLines = $True)
    $separator = ($prefix.length -eq 0) ? "" : ": "
    $line = ($prefix + $separator + $msg).trim()
    $parameters = @{ Object = $line }
    
    if ($prefix -eq "Error")
    { 
        $parameters.add("ForegroundColor", "Red") 
    } 
    else
    {
        if ($line.IndexOf("Warning") -ne -1)
        { 
            $parameters.add("ForegroundColor", "Yellow") 
        } 
    }

    $delimiterLine = new-object System.String -ArgumentList ("-", $line.length)
    if ($showDelimiterLines) { Write-Host $delimiterLine }
    Write-Host @parameters # // Note: We are "Splatting" parameters here
    if ($showDelimiterLines) { Write-Host $delimiterLine }
}

function onStepError
{
    param([int]$errorNumber)
    $line = "Step #$step failed (error $errorNumber)"
    report "Error" $line
    Exit 2
}

function runTests
{
    param([string]$testType)

    Write-Host "Running $testType tests..."

    $testOutputLines = (npm run unittests testType=$testType)
    $lastLine = ($testOutputLines[$testOutputLines.length - 1])
    $lastLine = $lastLine.Substring(0, $lastLine.length - 4) # // Trim trailing "reset-color" control chars (\x1b[0m)
    $summary = $lastLine.Substring($lastLine.IndexOf("SUMMARY: ") + 9)
    if ($lastLine.IndexOf("passed (100%)") -eq -1)
    {
        report "Error" ("Not all $testType tests passed: " + $summary)
        forEach ($line in $testOutputLines)
        {
            if ($line.indexOf(": FAILED") -gt 0)
            {
                Write-Host $line.Substring(30) # // Trim leading color control chars and timestamp
            }
        }
        Write-Host ""
        Exit 2
    }
    else
    {
        Write-Host "All $testType tests passed: $summary"
    }
}

# // Get the current folder which can be different from $MyInvocation.MyCommand.Path (which will be something like C:\src\Git\AMBROSIA\Clients\AmbrosiaJS\Ambrosia-Node\build.ps1)
$currPath = Get-Location 
$currDirName = [System.IO.Path]::GetFileName($currPath)

$tscFile = [System.IO.Path]::Combine($currPath, "node_modules", ".bin", "tsc")
if ([System.IO.File]::Exists($tscFile)) { $tscInstalled = $True }
$currentBranchName = ((git rev-parse --abbrev-ref HEAD) | Out-String).replace("`r", "").replace("`n", "")
$branchStatus = (git status) | Out-String

if ($branchStatus.IndexOf("nothing to commit, working tree clean") -gt 0)
{
    $branchStatus = "branch status: Clean"
    $isBranchClean = $True
} 
else
{
    $branchStatus = "Warning: Unstaged or uncommitted changes exist"
}

# // Pre-build check: Is the current directory 'Ambrosia-Node'?
if ($currDirName -ne "Ambrosia-Node")
{
    report "Error" "The current directory (`"$currDirName`") is not `"Ambrosia-Node`" as expected."
    Exit 1
}

# // Pre-build check: Is TypeScript installed locally?
if (!$tscInstalled) 
{
    report "Error" "The TypeScript compiler is not installed locally (have you run `"npm install`"?)"
    # // Report additional details to help debug the problem
    Write-Host "Installed npm packages:"
    npm list --depth=0
    Write-Host "Looked for file:" $tscFile
    Get-ChildItem ./node_modules/.bin    
    Exit 1
}

# // Gather pre-build reporting data
# // Note: npx will first look in ./node_modules/.bin, then search the PATH environment variable.
# //       If we allowed npx to install a missing package, it would get cached in C:\Users\[UserName]\AppData\Roaming\npm-cache\_npx
$typescriptVersion = (npx --no-install tsc -version)
$directoryInfo = new-object System.IO.DirectoryInfo -ArgumentList $currPath
$tgzList = $directoryInfo.GetFileSystemInfos() | where-object Name -Match "^ambrosia-node.*\.tgz$" | sort-object LastWriteTime
$tgzCount = $tgzList.count
if ($tgzCount -gt 0) { $currentPackageName = $tgzList[$tgzList.count - 1].Name } else { $currentPackageName = "[None]" }

report -msg "Starting build (latest package: $currentPackageName)..."
Write-Host "Operating System:" ([System.Environment]::OSVersion.Platform)
Write-Host ("PowerShell Version: " + $PSVersionTable.PSVersion + " (" + $PSVersionTable.PSEdition + ")")
Write-Host "Npm Version:" (npm -v)
Write-Host "Git Version:" (git --version)
report -msg "Current Git branch: '$currentBranchName' ($branchStatus)" -showDelimiterLines $False
if (!$isBranchClean)
{
    git status
}

$step = 1
Write-Host "[1 of 6] Cleaning 'lib' folder..."
New-Item -Path $currPath -Name "lib" -ItemType "directory" -Force | Out-Null # // Silently creates the folder (if needed)
$fileList = [System.IO.Directory]::GetFiles($currPath, "lib\*.d.ts", [System.IO.SearchOption]::AllDirectories)
$fileList += [System.IO.Directory]::GetFiles($currPath, "lib\*.js", [System.IO.SearchOption]::AllDirectories)
$fileList += [System.IO.Directory]::GetFiles($currPath, "lib\*.map", [System.IO.SearchOption]::AllDirectories)
$deletedFileCount = $fileList.length
if ($deletedFileCount -gt 100)
{
    report "Warning" "Step #$step would delete $deletedFileCount files, which is unexpectedly high: further investigation is required" $False
    Exit 3
}
forEach ($fileName in $fileList)
{
    Remove-Item $fileName
}
Write-Host $deletedFileCount files deleted

$step = 2
if ($bumpVersion.length -gt 0)
{
    Write-Host "[2 of 6] Bumping package version..."
    $currentVersion = (node --print "require('./package.json').version")
    if ($LASTEXITCODE -ne 0) { onStepError $LASTEXITCODE }

    $newVersion = (npm --no-git-tag-version version $bumpVersion).replace("v", "")
    if ($LASTEXITCODE -ne 0) { onStepError $LASTEXITCODE }

    $currentVersionStr = "(""" + $currentVersion + """);"
    $newVersionStr = "(""" + $newVersion + """);"
    $targetFile = [System.IO.Path]::Combine($currPath, "src/AmbrosiaRoot.ts")
    if (([System.IO.File]::ReadAllText($targetFile).IndexOf($currentVersionStr)) -ne -1)
    {
        $modifiedFileContent = [System.IO.File]::ReadAllText($targetFile).replace($currentVersionStr, $newVersionStr)
        [System.IO.File]::WriteAllText($targetFile, $modifiedFileContent)
    }
    else
    {
        report "Error" ($targetFile + " does not contain the current version '" + $currentVersionStr + "'. The automated changes to package.json and package-lock.json MUST BE REVERTED!") $False
        Exit 2
    }
    Write-Host "New version:" $newVersion
} 
else
{ 
    Write-Host "[2 of 6] Step skipped (no package version bump)"
}

$step = 3
Write-Host "[3 of 6] Compiling TypeScript (full rebuild) using TS compiler" $typescriptVersion.ToLower() "..."
# // Note: We MUST turn off incremental building here because we just cleaned the 'lib' folder
npx --no-install tsc -p ./tsconfig.json --incremental false
if ($LASTEXITCODE -ne 0) { onStepError $LASTEXITCODE }

# // Post-build check: Did all unit tests pass?
# // Note: Must always be done as a POST-build check because [automated] file changes may have occurred as a result of bumping the package version
$step = 4
Write-Host "[4 of 6] Performing$($basicUnitTestsOnly ? ' basic-only' : '') tests..."
runTests Unit
if (!$basicUnitTestsOnly)
{
    runTests CodeGen
    runTests DataFormat
}

$step = 5
Write-Host "[5 of 6] Archiving $tgzCount prior .tgz file(s)..."
New-Item -Path $currPath -Name "buildArchive" -ItemType "directory" -Force | Out-Null # // Silently creates the folder (if needed)
forEach ($fileInfo in $tgzList)
{
    Move-Item $fileInfo.Name ".\buildArchive" -Force # // Note: Overwrites the file if it already exists
    if ($LASTEXITCODE -ne 0) { onStepError $LASTEXITCODE }
}

$step = 6
Write-Host "[6 of 6] Creating package tarball..."
# // Note: "npm pack" (on Windows) writes to strErr even when successful, which causes the AzureDevOps build-pipeline (CI) to 
# //       conclude that the script has failed. So to solve this we redirect strErr to stdOut.
if ($isWindowsOS) { npm pack 2>&1 } else { npm pack }

if ($LASTEXITCODE -ne 0) { onStepError $LASTEXITCODE }

$directoryInfo = new-object System.IO.DirectoryInfo -ArgumentList $currPath
$tgzList = $directoryInfo.GetFileSystemInfos() | where-object Name -Match "^ambrosia-node.*\.tgz$" | sort-object LastWriteTime
if ($tgzList.count -gt 0) { $newPackageName = $tgzList[$tgzList.count - 1].Name } else { $newPackageName = "[None]" }

report -msg "Build complete (new package: $newPackageName)"
if ($bumpVersion) { Write-Host "Warning: Remember to commit/push the updated package.json, package-lock.json and AmbrosiaRoot.ts files." -ForegroundColor Yellow }
Exit 0
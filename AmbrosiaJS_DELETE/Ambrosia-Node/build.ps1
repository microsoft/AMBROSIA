# SYNTAX: ".\build.ps1 [bumpVersion]"
# This batch file builds the ambrosia-node package (eg. ambrosia-node-0.0.71.tgz).
# You should do a "git pull" before running this.
# It compiles the TypeScript source, [optionally] increases the 'patch' part of the package version,
# archives any old ambrosia-node*.tgz files (to .\buildArchive), then finally creates a new one.

$step = 0
$tscInstalled = 0
$bumpVersion = 0
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
    $separator = ": "
    if ($prefix.length -eq 0) { $separator = ""}
    $line = ($prefix + $separator + $msg).trim()
    $parameters = @{ Object = $line }
    if ($prefix -eq "Error") { 
        $parameters.add("ForegroundColor", "Red") 
    } else {
        if ($line.IndexOf("Warning") -ne -1) { 
            $parameters.add("ForegroundColor", "Yellow") 
        } 
    }

    $delimiterLine = new-object System.String -ArgumentList ("-", $line.length)
    if ($showDelimiterLines) { Write-Host $delimiterLine }
    Write-Host @parameters # Note: We are "Splatting" parameters here
    if ($showDelimiterLines) { Write-Host $delimiterLine }
}

function onStepError
{
    param([int]$errorNumber)
    $line = "Step #$step failed (error $errorNumber)"
    report "Error" $line
    Exit 2
}

# Get the current folder which can be different from $MyInvocation.MyCommand.Path (which will be something like C:\src\Git\Franklin\AmbrosiaJS\Ambrosia-Node\build.ps1)
$currPath = Get-Location 
$currDirName = [System.IO.Path]::GetFileName($currPath)

$tscFile = [System.IO.Path]::Combine($currPath, "node_modules/.bin/tsc")
if ([System.IO.File]::Exists($tscFile)) { $tscInstalled = 1 }
$currentBranchName = ((git rev-parse --abbrev-ref HEAD) | Out-String).replace("`r", "").replace("`n", "")
$branchStatus = (git status) | Out-String

if ($branchStatus.IndexOf("nothing to commit, working tree clean") -gt 0) {
    $branchStatus = "branch status: Clean"
    $isBranchClean = $True
} else {
    $branchStatus = "Warning: Unstaged or uncommitted changes exist"
}

# // Pre-build check: Is the current directory 'Ambrosia-Node'?
if ($currDirName -ne "Ambrosia-Node")
{
    report "Error" "The current directory (`"$currDirName`") is not `"Ambrosia-Node`" as expected."
    Exit 1
}

# // Pre-build check: Is TypeScript installed?
if ($tscInstalled -eq 0) 
{
    report "Error" "The TypeScript compiler is not installed (have you run `"npm install`"?)"
    # // Report additional details to help debug the problem
    Write-Host "Installed npm packages:"
    npm list --depth=0
    Write-Host "Looked for file:" $tscFile
    Get-ChildItem ./node_modules/.bin    
    Exit 1
}

# // Parse the command-line
if ($Args[0] -eq "bumpVersion") { $bumpVersion = 1 }

# // Gather pre-build reporting data
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
Write-Host "[1 of 5] Cleaning 'lib' folder..."
New-Item -Path $currPath -Name "lib" -ItemType "directory" -Force | Out-Null # Silently creates the folder (if needed)
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
if ($bumpVersion -eq 1)
{
    Write-Host "[2 of 5] Bumping package version..."
    $newVersion = (npm --no-git-tag-version version patch).replace("v", "")
    if ($LASTEXITCODE -ne 0) { onStepError $LASTEXITCODE }

    $currentVersion = $newVersion.split(".")[0] + "." + $newVersion.split(".")[1] + "." + ([System.Int32]::Parse($newVersion.split(".")[2]) - 1)
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
 } else { 
    Write-Host "[2 of 5] Step skipped (no package version bump)"
 }

$step = 3
Write-Host "[3 of 5] Compiling TypeScript..."
& "npx" "tsc" "-p" ".\tsconfig.json"
if ($LASTEXITCODE -ne 0) { onStepError $LASTEXITCODE }

$step = 4
Write-Host "[4 of 5] Archiving $tgzCount prior .tgz file(s)..."
New-Item -Path $currPath -Name "buildArchive" -ItemType "directory" -Force | Out-Null # Silently creates the folder (if needed)
forEach ($fileInfo in $tgzList)
{
    Move-Item $fileInfo.Name ".\buildArchive" -Force # Note: Overwrites the file if it already exists
    if ($LASTEXITCODE -ne 0) { onStepError $LASTEXITCODE }
}

$step = 5
Write-Host "[5 of 5] Creating package tarball..."
# // Note: "npm pack" (on Windows) writes to strErr even when successful, which causes the AzureDevOps build-pipeline (CI) to 
# //       conclude that the script has failed. So to solve this we redirect strErr to stdOut.
if ($isWindowsOS) { & "npm" "pack" "2>&1" } else { & "npm" "pack" }

if ($LASTEXITCODE -ne 0) { onStepError $LASTEXITCODE }

$directoryInfo = new-object System.IO.DirectoryInfo -ArgumentList $currPath
$tgzList = $directoryInfo.GetFileSystemInfos() | where-object Name -Match "^ambrosia-node.*\.tgz$" | sort-object LastWriteTime
if ($tgzList.count -gt 0) { $newPackageName = $tgzList[$tgzList.count - 1].Name } else { $newPackageName = "[None]" }

report -msg "Build complete (new package: $newPackageName)"
if ($bumpVersion -eq 1) { Write-Host "Warning: Remember to commit/push the updated package.json, package-lock.json and AmbrosiaRoot.ts files." -ForegroundColor Yellow }
Exit 0
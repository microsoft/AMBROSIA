# In addition to invoking the CodeGen program, this script also copies the generated source files
# into a UWP clone of the generated project. This additional step is necessary because the CodeGen
# program generates a project with .NET Framework and .NET Core targets, but UWP apps can only
# reference UWP or .NET Standard projects.

# Delete any old versions of the generated source files in GeneratedSources, so that we can
# identify the newly generated source files
If (Test-Path GeneratedSourceFiles)
{
    Get-ChildItem -Path GeneratedSourceFiles | Remove-Item -Recurse
}

# Generate the assembly and source files
$ambrosiaExe = "..\..\Clients\CSharp\AmbrosiaCS\bin\x64\Debug\net46\AmbrosiaCS.exe"
$inputAssembly = "GraphicalImmortalAPI\bin\x64\Debug\net46\GraphicalImmortalAPI.dll"
If (!(Test-Path $ambrosiaExe))
{
    Write-Output "Codegen failure: Ambrosia EXE is missing."
    exit
}
If (!(Test-Path $inputAssembly))
{
    Write-Output "Codegen failure: input assembly is missing (should be located at $inputAssembly)."
    exit
}
Invoke-Expression "$ambrosiaExe CodeGen -a=`"$inputAssembly`" -o=`"GraphicalImmortalAPIGenerated`""

# Copy the source files into the GraphicalImmortalAPIGeneratedUWP project
If (!(Test-Path GeneratedSourceFiles))
{
    Write-Output "Codegen failure: GeneratedSourceFiles missing (should have been created by Ambrosia.exe)."
    exit
}
$sourceDir = "GeneratedSourceFiles\GraphicalImmortalAPIGenerated\latest"
If (!$sourceDir)
{
    Write-Output "Codegen failure: generated source directory missing (should have been created inside of GeneratedSourceFiles by Ambrosia.exe)."
    exit
}
$sourceFiles = Get-ChildItem -Path $sourceDir -Filter "*.cs"
Foreach ($file in $sourceFiles)
{
    Copy-Item $file.FullName -Destination GraphicalImmortalAPIGeneratedUWP
}
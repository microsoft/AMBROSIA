'use strict'
const fs = require("fs");
const path = require("path");

// This script is run when the 'ambrosia-node' package is installed.
// It copies the "sample" ambrosiaConfig.json file [after renaming an existing version to ".old"] - and its schema [which is always copied] - to
// the installation folder (ie. the folder where 'npm install' was run from). This package.json technique ("postinstall": "node postInstall.js")
// of using node.exe to do the file copy enables the package.json "postinstall" script to work cross-platform (Windows, Unix, etc.) without 
// having to worry about what the 'npm script-shell' setting is.
// Note that process.env.INIT_CWD is only set when this script is run via "npm install". 
// Further, note that if "npm install" is run on the package.json (rather than the .tgz) then the sourceFolder and destinationFolder will
// be the same, and the script will do nothing.

if (process.env.INIT_CWD)
{
    let sourceFolder = process.cwd();
    let destinationFolder = process.env.INIT_CWD;
    let errorOccurred = false;

    if (sourceFolder !== destinationFolder)
    {
        try
        {
            let localJsonFile = path.join(destinationFolder, "ambrosiaConfig.json");
            let localFileRenamed = false;
            if (fs.existsSync(localJsonFile))
            {
                fs.copyFileSync(localJsonFile, localJsonFile + ".old");
                localFileRenamed = true;
            }

            fs.copyFileSync("ambrosiaConfig.json", path.join(destinationFolder, "ambrosiaConfig.json")); 
            console.log("ATTENTION: Either use the 'autoRegister' setting, or edit the ambrosiaConfig.json file to match your Ambrosia instance registration.");
            console.log("           See https://github.com/microsoft/AMBROSIA/blob/master/Samples/HelloWorld/HOWTO-WINDOWS-TwoProc.md#registering-the-immortal-instances for more details.");
            console.log(localFileRenamed ? "           Note: Your existing ambrosiaConfig.json file was renamed to ambrosiaConfig.json.old.\n" : "");
        }
        catch (error)
        {
            console.log("[postInstall] Error: " + error.message);
            errorOccurred = true;
        }

        try
        {
            // This will overwrite the file if it's already present. The file is "static" in nature, but it may change
            // with each new version of the 'ambrosia-node' package, so overwriting it is the correct behavior.
            fs.copyFileSync("ambrosiaConfig-schema.json", path.join(destinationFolder, "ambrosiaConfig-schema.json")); 
        }
        catch (error)
        {
            console.log("[postInstall] Error: " + error.message);
            errorOccurred = true;
        }

        if (errorOccurred)
        {
            console.log("[postInstall] Current (source) folder: " + sourceFolder);
            console.log("[postInstall] Installation (destination) folder: " + destinationFolder);
            console.log("");
        }
    }
}
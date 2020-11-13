// Note: The "ambrosia-node" package was installed using "npm install ..\Ambrosia-Node\ambrosia-node-0.0.7.tgz", 
//       which also installed all the required [production] package dependencies (eg. azure-storage).
import Ambrosia = require("ambrosia-node"); 
import Utils = Ambrosia.Utils;
import Meta = Ambrosia.Meta;
import IC = Ambrosia.IC;
import Configuration = Ambrosia.Configuration;
import * as Framework from "./PublisherFramework.g"; // This is a generated file

main();
// codeGen();

// A "bootstrap" program that code-gen's the publisher/consumer TypeScript files.
async function codeGen()
{
    try
    {
        await Ambrosia.initializeAsync(Ambrosia.LBInitMode.CodeGen);
        let sourceFile: string = Utils.getCommandLineArg("sourceFile");
        let codeGenKind: Meta.CodeGenFileKind = Meta.CodeGenFileKind[Utils.getCommandLineArg("codeGenKind", "All")] ?? Meta.CodeGenFileKind.All;
        let mergeType: Meta.FileMergeType = Meta.FileMergeType[Utils.getCommandLineArg("mergeType", "None")] ?? Meta.FileMergeType.None;
        Meta.emitTypeScriptFileFromSource(sourceFile, { fileKind: codeGenKind, mergeType: mergeType });
    }
    catch (error)
    {
        Utils.tryLog(error);
    }
}

async function main()
{
    try
    {
        await Ambrosia.initializeAsync();

        // Run the generated test app
        let config: Configuration.AmbrosiaConfig = new Configuration.AmbrosiaConfig(Framework.messageDispatcher, Framework.checkpointProducer, Framework.checkpointConsumer, Framework.onICError);
        IC.start(config, Framework._appState);

        // To run the built-in test "app", use this instead of IC.start()
        // Ambrosia.ICTest.startTest();
    }
    catch (error)
    {
        Utils.tryLog(error);
    }
}
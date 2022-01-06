// Note: The "ambrosia-node" package was installed using "npm install ..\Ambrosia-Node\ambrosia-node-2.0.0.tgz", 
//       which also installed all the required [production] package dependencies (eg. azure-storage).
import Ambrosia = require("ambrosia-node"); 
import Utils = Ambrosia.Utils;
import Meta = Ambrosia.Meta;
import IC = Ambrosia.IC;
import Configuration = Ambrosia.Configuration;
import * as Framework from "./PublisherFramework.g"; // This is a generated file
import * as Self from "./ConsumerInterface.g"; // This is a generated file

main();
// codeGen();

// A "bootstrap" program that code-gen's the publisher/consumer TypeScript files.
async function codeGen()
{
    try
    {
        await Ambrosia.initializeAsync(Ambrosia.LBInitMode.CodeGen);
        let sourceFile: string = Utils.getCommandLineArg("sourceFile");
        let fileKind: Meta.GeneratedFileKind = Meta.GeneratedFileKind[Utils.getCommandLineArg("codeGenKind", "All") as keyof typeof Meta.GeneratedFileKind] ?? Meta.GeneratedFileKind.All;
        let mergeType: Meta.FileMergeType = Meta.FileMergeType[Utils.getCommandLineArg("mergeType", "None") as keyof typeof Meta.FileMergeType] ?? Meta.FileMergeType.None;
        Meta.emitTypeScriptFileFromSource(sourceFile, { apiName: "TestApp", fileKind: fileKind, mergeType: mergeType });

        // Mimic Darren's test code...
        // let testName: string = "TS_GenType1";
        // let sourceFile: string = `C:/src/Git/AMBROSIA/AmbrosiaTest/JSCodeGen/JS_CodeGen_TestFiles/${testName}.ts`;
        // let generatedFileName: string = `${testName}_Generated`;
        // Meta.emitTypeScriptFileFromSource(sourceFile, { fileKind: Meta.GeneratedFileKind.Consumer, mergeType: Meta.FileMergeType.None, emitGeneratedTime: false, generatedFileName: generatedFileName + "_Consumer" });
        // Meta.emitTypeScriptFileFromSource(sourceFile, { fileKind: Meta.GeneratedFileKind.Publisher, mergeType: Meta.FileMergeType.None, emitGeneratedTime: false, generatedFileName: generatedFileName + "_Publisher" });
    }
    catch (error: unknown)
    {
        Utils.tryLog(Utils.makeError(error));
    }
}

async function main()
{
    try
    {
        await Ambrosia.initializeAsync();

        // Run the generated test app
        let config: Configuration.AmbrosiaConfig = new Configuration.AmbrosiaConfig(Framework.messageDispatcher, Framework.checkpointProducer, Framework.checkpointConsumer, Self.postResultDispatcher);
        Framework.State._appState = IC.start(config, Framework.State.AppState);

        // To run the built-in test "app", use this instead of IC.start()
        // Ambrosia.ICTest.startTest();
    }
    catch (error: unknown)
    {
        Utils.tryLog(Utils.makeError(error));
    }
}
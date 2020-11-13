// Note: The "ambrosia-node" package was installed using "npm install ..\Ambrosia-Node\ambrosia-node-0.0.7.tgz", 
//       which also installed all the required [production] package dependencies (eg. azure-storage).
import Ambrosia = require("ambrosia-node"); 
import Utils = Ambrosia.Utils;
import Meta = Ambrosia.Meta;
import IC = Ambrosia.IC;
import Configuration = Ambrosia.Configuration;
//import * as Framework from "./PublisherFramework.g"; // This is a generated file

//main();
codeGen();

//******  AMBROSIA ******

/***** TO DO (from RH email)
* TS Format - test file where functions are all over the place in terms of formatting - Input TS format: Comments (before/after/inline/multi-line/JSDoc), newlines, white space
* Code gen options: file type, merge type, other flags (basically, all the parameter of Meta.emitTypeScriptFileFromSource())
* TS namespaces: nested, co-mingled with non-namespace scoped entities, faithfully carried over to the generated ConsumerInterface.g.ts.
* While emitTypeScriptFileFromSource() should be the subject of the majority of testing [because I expected it will be the most used technique], it would also be good to test emitTypeScriptFile() too. This can be accomplished by calling Meta.publishFromSource() beforehand, which will enable you to leverage your earlier investment in input .ts files
*/

// A "bootstrap" program that code-gen's the publisher/consumer TypeScript files.
async function codeGen()
{
    try
    {
        await Ambrosia.initializeAsync(Ambrosia.LBInitMode.CodeGen);
        let sourceFile: string = Utils.getCommandLineArg("sourceFile");
        let codeGenKind: Meta.CodeGenFileKind = Meta.CodeGenFileKind[Utils.getCommandLineArg("codeGenKind", "All")] ?? Meta.CodeGenFileKind.All;
        let mergeType: Meta.FileMergeType = Meta.FileMergeType[Utils.getCommandLineArg("mergeType", "None")] ?? Meta.FileMergeType.None;
        let generatedFileName: string = Utils.getCommandLineArg("generatedFileName", "TestOutput") ?? "TestOutput";

        Meta.emitTypeScriptFileFromSource(sourceFile, { fileKind: Meta.CodeGenFileKind.Consumer, mergeType: Meta.FileMergeType.None, emitGeneratedTime: false, generatedFileName: generatedFileName+"_Consumer" });
        Meta.emitTypeScriptFileFromSource(sourceFile, { fileKind: Meta.CodeGenFileKind.Publisher, mergeType: Meta.FileMergeType.None, emitGeneratedTime: false, generatedFileName: generatedFileName+"_Publisher" });

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
//        let config: Configuration.AmbrosiaConfig = new Configuration.AmbrosiaConfig(Framework.messageDispatcher, Framework.checkpointProducer, Framework.checkpointConsumer, Framework.onICError);
        //IC.start(config, Framework._appState);

        // To run the built-in test "app", use this instead of IC.start()
        // Ambrosia.ICTest.startTest();
    }
    catch (error)
    {
        Utils.tryLog(error);
    }
}
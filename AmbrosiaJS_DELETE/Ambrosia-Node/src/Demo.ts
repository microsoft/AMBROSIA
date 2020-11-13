// This is the built-in "demo/test app" (which uses the Ambrosia "module").
// While developing Ambrosia in VSCode, in launch.json specify "program": "${workspaceFolder}/lib/Demo.js"
// This allows Ambrosia to be tested without the need for a separate host app.
import Process = require("process");
import Ambrosia = require("./Ambrosia");
import Utils = Ambrosia.Utils;
import Meta = Ambrosia.Meta;

// A "bootstrap" program that code-gen's the publisher/consumer TypeScript files.
async function codeGen(args: string[])
{
    try
    {
        await Ambrosia.initializeAsync(Ambrosia.LBInitMode.CodeGen);

        // [1] Implicit (automatic) code-gen from an annotated source file
        // Meta.publishFromSource("ASTTest.ts");
        // Meta.publishFromSource("PI.ts");
        // Meta.emitTypeScriptFileFromSource("PI.ts", { fileKind: Meta.CodeGenFileKind.Consumer, mergeType: Meta.FileMergeType.None, generatedFileName: "CustomNamedConsumer" });
        // Meta.emitTypeScriptFileFromSource("PI.ts", { fileKind: Meta.CodeGenFileKind.Publisher, mergeType: Meta.FileMergeType.None, generatedFileName: "CustomNamedPublisher" });
        Meta.emitTypeScriptFileFromSource("PI.ts", { fileKind: Meta.CodeGenFileKind.All, mergeType: Meta.FileMergeType.None });
        return;

        // [2] Explicit (manual) code-gen
        Meta.publishType("Digits", "{ count: number }");
        Meta.publishPostMethod("ComputePI", 1, ["digits?: Digits"], "number");
        Meta.emitTypeScriptFile({ fileKind: Meta.CodeGenFileKind.All, mergeType: Meta.FileMergeType.None });
    }
    catch (error)
    {
        Utils.tryLog(error);
    }
}

async function main(args: string[])
{
    try
    {
        await Ambrosia.initializeAsync();
        Ambrosia.ICTest.startTest();
    }
    catch (error)
    {
        Utils.tryLog(error);
    }
}

main(Process.argv);
// codeGen(Process.argv);
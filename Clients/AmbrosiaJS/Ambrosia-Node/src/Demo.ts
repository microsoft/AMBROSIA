// This is the built-in "demo/test app" (which uses the Ambrosia "module").
// While developing Ambrosia in VSCode, in launch.json specify "program": "${workspaceFolder}/lib/Demo.js"
// This allows Ambrosia to be tested without the need for a separate host app.
import Process = require("process");
import Ambrosia = require("./Ambrosia");
import Utils = Ambrosia.Utils;
import Meta = Ambrosia.Meta; // For code-gen

main(Process.argv);
// codeGen(Process.argv);

// A "bootstrap" program that code-gen's the publisher/consumer TypeScript files.
async function codeGen(args: string[])
{
    try
    {
        await Ambrosia.initializeAsync(Ambrosia.LBInitMode.CodeGen);

        // [1] Implicit (automatic) code-gen from an annotated source file
        // Meta.publishFromSource("./test/ASTTest.ts");
        // Meta.publishFromSource("./test/PI.ts");
        // Meta.emitTypeScriptFileFromSource("./test/PI.ts", { fileKind: Meta.GeneratedFileKind.Consumer, mergeType: Meta.FileMergeType.None, generatedFileName: "CustomNamedConsumer" });
        // Meta.emitTypeScriptFileFromSource("./test/PI.ts", { fileKind: Meta.GeneratedFileKind.Publisher, mergeType: Meta.FileMergeType.None, generatedFileName: "CustomNamedPublisher" });
        const fileGenOptions: Meta.FileGenOptions = { apiName: "PI", fileKind: Meta.GeneratedFileKind.All, mergeType: Meta.FileMergeType.None, haltOnError: true };
        Meta.emitTypeScriptFileFromSource("./test/PI.ts", fileGenOptions);
        return;

        // [2] Explicit (manual) code-gen
        Meta.publishType("Digits", "{ count: number }");
        Meta.publishPostMethod("ComputePI", 1, ["digits?: Digits"], "number");
        Meta.emitTypeScriptFile({ apiName: "PI", fileKind: Meta.GeneratedFileKind.All, mergeType: Meta.FileMergeType.None });
    }
    catch (error: unknown)
    {
        Utils.tryLog(Utils.makeError(error));
    }
}

async function main(args: string[])
{
    try
    {
        await Ambrosia.initializeAsync();
        Ambrosia.ICTest.startTest();
    }
    catch (error: unknown)
    {
        Utils.tryLog(Utils.makeError(error));
        Process.exit(-2);
    }
}
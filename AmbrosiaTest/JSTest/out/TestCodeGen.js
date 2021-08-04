"use strict";
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
Object.defineProperty(exports, "__esModule", { value: true });
// Note: Build the ambrosia-node*.tgz in \AmbrosiaJS\Ambrosia-Node\build.ps1
//    The "ambrosia-node" package was installed using "npm install ..\Ambrosia-Node\ambrosia-node-0.0.73.tgz", 
//       which also installed all the required [production] package dependencies (eg. azure-storage).
const Ambrosia = require("ambrosia-node");
var Utils = Ambrosia.Utils;
var Meta = Ambrosia.Meta;
const Path = require("path");
main();
/***** TO DO
*  Code gen options: file type, merge type, other flags (basically, all the parameter of Meta.emitTypeScriptFileFromSource())
* TS namespaces: nested, co-mingled with non-namespace scoped entities, faithfully carried over to the generated ConsumerInterface.g.ts.
* While emitTypeScriptFileFromSource() should be the subject of the majority of testing [because I expected it will be the most used technique], it would also be good to test emitTypeScriptFile() too. This can be accomplished by calling Meta.publishFromSource() beforehand, which will enable you to leverage your earlier investment in input .ts files
*
* Another possible TO DO: want to run publisher side if the consumer side fails? Maybe not ... since this is ran for neg tests too
*/
// A "bootstrap" program that code-gen's the publisher/consumer TypeScript files.
function main() {
    var _a;
    return __awaiter(this, void 0, void 0, function* () {
        try {
            yield Ambrosia.initializeAsync(Ambrosia.LBInitMode.CodeGen);
            let sourceFile = Utils.getCommandLineArg("sourceFile");
            let generatedFileName = (_a = Utils.getCommandLineArg("generatedFileName", "TestOutput")) !== null && _a !== void 0 ? _a : "TestOutput";
            let apiName = Path.basename(generatedFileName).replace(Path.extname(generatedFileName), "");
            // If want to run as separate generation steps for consumer and publisher
            //Meta.emitTypeScriptFileFromSource(sourceFile, { fileKind: Meta.GeneratedFileKind.Consumer, mergeType: Meta.FileMergeType.None, emitGeneratedTime: false, generatedFileName: generatedFileName+"_Consumer" });
            //Meta.emitTypeScriptFileFromSource(sourceFile, { fileKind: Meta.GeneratedFileKind.Publisher, mergeType: Meta.FileMergeType.None, emitGeneratedTime: false, generatedFileName: generatedFileName+"_Publisher" });
            // Use this for single call to generate both consumer and publisher
            Meta.emitTypeScriptFileFromSource(sourceFile, { apiName: apiName, fileKind: Meta.GeneratedFileKind.All, mergeType: Meta.FileMergeType.None, emitGeneratedTime: false, generatedFilePrefix: generatedFileName, strictCompilerChecks: true });
            // Something like this instead of just running them both
            //        if (Meta.emitTypeScriptFileFromSource(sourceFile, { fileKind: Meta.GeneratedFileKind.Consumer, mergeType: Meta.FileMergeType.None, emitGeneratedTime: false, generatedFileName: generatedFileName+"_Consumer" }) > 0)
            //      {
            //        Meta.emitTypeScriptFileFromSource(sourceFile, { fileKind: Meta.emitTypeScriptFileFromSource(sourceFile, { fileKind: Meta.GeneratedFileKind.Publisher, mergeType: Meta.FileMergeType.None, emitGeneratedTime: false, generatedFileName: generatedFileName+"_Publisher" });
            //  }
        }
        catch (error) {
            Utils.tryLog(error);
        }
    });
}
//# sourceMappingURL=TestCodeGen.js.map
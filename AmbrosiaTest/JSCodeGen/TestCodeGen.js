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
var __generator = (this && this.__generator) || function (thisArg, body) {
    var _ = { label: 0, sent: function() { if (t[0] & 1) throw t[1]; return t[1]; }, trys: [], ops: [] }, f, y, t, g;
    return g = { next: verb(0), "throw": verb(1), "return": verb(2) }, typeof Symbol === "function" && (g[Symbol.iterator] = function() { return this; }), g;
    function verb(n) { return function (v) { return step([n, v]); }; }
    function step(op) {
        if (f) throw new TypeError("Generator is already executing.");
        while (_) try {
            if (f = 1, y && (t = op[0] & 2 ? y["return"] : op[0] ? y["throw"] || ((t = y["return"]) && t.call(y), 0) : y.next) && !(t = t.call(y, op[1])).done) return t;
            if (y = 0, t) op = [op[0] & 2, t.value];
            switch (op[0]) {
                case 0: case 1: t = op; break;
                case 4: _.label++; return { value: op[1], done: false };
                case 5: _.label++; y = op[1]; op = [0]; continue;
                case 7: op = _.ops.pop(); _.trys.pop(); continue;
                default:
                    if (!(t = _.trys, t = t.length > 0 && t[t.length - 1]) && (op[0] === 6 || op[0] === 2)) { _ = 0; continue; }
                    if (op[0] === 3 && (!t || (op[1] > t[0] && op[1] < t[3]))) { _.label = op[1]; break; }
                    if (op[0] === 6 && _.label < t[1]) { _.label = t[1]; t = op; break; }
                    if (t && _.label < t[2]) { _.label = t[2]; _.ops.push(op); break; }
                    if (t[2]) _.ops.pop();
                    _.trys.pop(); continue;
            }
            op = body.call(thisArg, _);
        } catch (e) { op = [6, e]; y = 0; } finally { f = t = 0; }
        if (op[0] & 5) throw op[1]; return { value: op[0] ? op[1] : void 0, done: true };
    }
};
Object.defineProperty(exports, "__esModule", { value: true });
// Note: Build the ambrosia-node*.tgz in \AmbrosiaJS\Ambrosia-Node\build.ps1
//    The "ambrosia-node" package was installed using "npm install ..\Ambrosia-Node\ambrosia-node-0.0.73.tgz", 
//       which also installed all the required [production] package dependencies (eg. azure-storage).
var Ambrosia = require("ambrosia-node");
var Utils = Ambrosia.Utils;
var Meta = Ambrosia.Meta;
var Path = require("path");
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
    return __awaiter(this, void 0, void 0, function () {
        var sourceFile, generatedFileName, apiName, error_1;
        return __generator(this, function (_b) {
            switch (_b.label) {
                case 0:
                    _b.trys.push([0, 2, , 3]);
                    return [4 /*yield*/, Ambrosia.initializeAsync(Ambrosia.LBInitMode.CodeGen)];
                case 1:
                    _b.sent();
                    sourceFile = Utils.getCommandLineArg("sourceFile");
                    generatedFileName = (_a = Utils.getCommandLineArg("generatedFileName", "TestOutput")) !== null && _a !== void 0 ? _a : "TestOutput";
                    apiName = Path.basename(generatedFileName).replace(Path.extname(generatedFileName), "");
                    // If want to run as separate generation steps for consumer and publisher
                    //Meta.emitTypeScriptFileFromSource(sourceFile, { fileKind: Meta.GeneratedFileKind.Consumer, mergeType: Meta.FileMergeType.None, emitGeneratedTime: false, generatedFileName: generatedFileName+"_Consumer" });
                    //Meta.emitTypeScriptFileFromSource(sourceFile, { fileKind: Meta.GeneratedFileKind.Publisher, mergeType: Meta.FileMergeType.None, emitGeneratedTime: false, generatedFileName: generatedFileName+"_Publisher" });
                    // Use this for single call to generate both consumer and publisher
                    Meta.emitTypeScriptFileFromSource(sourceFile, { apiName: apiName, fileKind: Meta.GeneratedFileKind.All, mergeType: Meta.FileMergeType.None, emitGeneratedTime: false, generatedFilePrefix: generatedFileName });
                    return [3 /*break*/, 3];
                case 2:
                    error_1 = _b.sent();
                    Utils.tryLog(error_1);
                    return [3 /*break*/, 3];
                case 3: return [2 /*return*/];
            }
        });
    });
}
//# sourceMappingURL=TestCodeGen.js.map